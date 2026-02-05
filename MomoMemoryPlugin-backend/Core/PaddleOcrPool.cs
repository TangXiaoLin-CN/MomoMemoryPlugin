using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace MomoBackend.Core;

/// <summary>
/// OCR 实例 - 封装单个 PaddleOCR 引擎
/// </summary>
public class OcrInstance : IDisposable
{
    private PaddleOcrAll? _engine;
    private readonly object _lock = new();
    private bool _isInitialized;
    private string _initError = "";
    private int _consecutiveFailures = 0;
    private const int MaxConsecutiveFailures = 2;
    private int _usageCount = 0; // 使用计数，用于负载均衡

    public int Id { get; }
    public bool IsAvailable => _isInitialized && _engine != null;
    public int UsageCount => _usageCount;

    public OcrInstance(int id)
    {
        Id = id;
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            Console.WriteLine($"[OcrPool] 实例 #{Id}: 初始化中...");

            var model = LocalFullModels.ChineseV4;
            _engine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,
                Enable180Classification = false
            };

            _isInitialized = true;
            _initError = "";
            Console.WriteLine($"[OcrPool] 实例 #{Id}: 初始化成功");
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            _initError = ex.Message;
            Console.WriteLine($"[OcrPool] 实例 #{Id}: 初始化失败 - {ex.Message}");
        }
    }

    public OcrResult Recognize(Bitmap bitmap)
    {
        var result = new OcrResult();

        lock (_lock)
        {
            _usageCount++;
            
            // 检查是否需要重新初始化
            if (_consecutiveFailures >= MaxConsecutiveFailures)
            {
                Console.WriteLine($"[OcrPool] 实例 #{Id}: 连续失败 {_consecutiveFailures} 次，重新初始化...");
                Reset();
            }

            if (!_isInitialized || _engine == null)
            {
                result.Success = false;
                result.ErrorMessage = string.IsNullOrEmpty(_initError)
                    ? $"实例 #{Id} 未初始化"
                    : $"实例 #{Id} 初始化失败: {_initError}";
                return result;
            }

            // 尝试执行 OCR，失败则重试一次
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var mat = BitmapToMat(bitmap);

                    if (mat.Empty())
                    {
                        result.Success = false;
                        result.ErrorMessage = "图像转换失败";
                        return result;
                    }

                    var ocrResult = _engine.Run(mat);

                    result.Success = true;
                    result.Text = (ocrResult.Text ?? "").Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

                    var confidence = ocrResult.Regions.Any() ? ocrResult.Regions.Average(r => r.Score) : 0;
                    result.Confidence = double.IsNaN(confidence) || double.IsInfinity(confidence) ? 0 : confidence;

                    if (ocrResult.Regions != null && ocrResult.Regions.Length > 0)
                    {
                        result.Lines = ocrResult.Regions.Select(region => new OcrLine
                        {
                            Text = region.Text ?? "",
                            Words = new List<OcrWord>
                            {
                                new OcrWord
                                {
                                    Text = region.Text ?? "",
                                    BoundingRect = new Rectangle(
                                        (int)region.Rect.Center.X - (int)(region.Rect.Size.Width / 2),
                                        (int)region.Rect.Center.Y - (int)(region.Rect.Size.Height / 2),
                                        (int)region.Rect.Size.Width,
                                        (int)region.Rect.Size.Height
                                    )
                                }
                            }
                        }).ToList();
                    }

                    _consecutiveFailures = 0;
                    return result;
                }
                catch (Exception ex)
                {
                    if (attempt == 0)
                    {
                        Console.WriteLine($"[OcrPool] 实例 #{Id}: OCR 失败，重试中... ({ex.Message})");
                        Thread.Sleep(100);
                        continue;
                    }

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _consecutiveFailures++;
                    Console.WriteLine($"[OcrPool] 实例 #{Id}: OCR 失败 (连续第 {_consecutiveFailures} 次)");
                }
            }
        }

        return result;
    }

    private void Reset()
    {
        try
        {
            _engine?.Dispose();
            _engine = null;
            _isInitialized = false;
            _consecutiveFailures = 0;
            _usageCount = 0;
            Thread.Sleep(50);
            Initialize();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OcrPool] 实例 #{Id}: 重置失败 - {ex.Message}");
        }
    }

    private static Mat BitmapToMat(Bitmap bitmap)
    {
        try
        {
            if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
            {
                var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(converted))
                {
                    g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                }
                var mat = BitmapConverter.ToMat(converted);
                converted.Dispose();
                return mat;
            }
            return BitmapConverter.ToMat(bitmap);
        }
        catch
        {
            return new Mat();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _engine?.Dispose();
            _engine = null;
            _isInitialized = false;
        }
    }
}

/// <summary>
/// PaddleOCR 实例池 - 管理多个独立的 OCR 实例，支持并行处理
/// </summary>
public class PaddleOcrPool : IDisposable
{
    private static PaddleOcrPool? _instance;
    private static readonly object _instanceLock = new();

    private readonly List<OcrInstance> _instances = new();
    private int _poolSize;
    private int _nextInstanceId = 0; // 轮询索引
    private readonly object _loadBalanceLock = new(); // 负载均衡锁
    
    // 动态扩缩容相关
    private System.Threading.Timer? _scalingTimer;
    private int _requestCount = 0;
    private DateTime _lastScalingTime = DateTime.Now;
    private const int ScalingIntervalMs = 2000; // 扩缩容检查间隔（毫秒）
    private const int MinPoolSize = 2; // 最小池大小
    private const int MaxPoolSize = 16; // 最大池大小
    private const int HighLoadThreshold = 8; // 高负载阈值（每秒请求数）
    private const int LowLoadThreshold = 3; // 低负载阈值（每秒请求数）
    private const int ScaleUpIncrement = 2; // 扩容增量
    private const int ScaleDownDecrement = 1; // 缩容减量

    public static PaddleOcrPool Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new PaddleOcrPool();
                }
            }
            return _instance;
        }
    }

    private PaddleOcrPool()
    {
        // 根据 CPU 核心数设置默认池大小，最少 2 个实例，最多不超过 MaxPoolSize
        int defaultSize = Math.Min(Math.Max(2, Environment.ProcessorCount - 1), MaxPoolSize);
        Console.WriteLine($"[OcrPool] 根据 CPU 核心数 ({Environment.ProcessorCount}) 设置默认池大小: {defaultSize} (限制在 {MinPoolSize}-{MaxPoolSize} 之间)");
        EnsurePoolSize(defaultSize);
        
        // 启动动态扩缩容定时器
        _scalingTimer = new System.Threading.Timer(
            (state) => CheckScaling(),
            null,
            ScalingIntervalMs,
            ScalingIntervalMs
        );
    }

    /// <summary>
    /// 确保池中至少有指定数量的实例
    /// </summary>
    public void EnsurePoolSize(int size)
    {
        lock (_instanceLock)
        {
            // 限制池大小不超过最大值
            size = Math.Min(size, MaxPoolSize);
            
            if (size <= _poolSize) return;

            Console.WriteLine($"[OcrPool] 扩展实例池: {_poolSize} -> {size}");

            for (int i = _poolSize; i < size; i++)
            {
                _instances.Add(new OcrInstance(i));
            }
            _poolSize = size;
        }
    }

    /// <summary>
    /// 获取当前池大小
    /// </summary>
    public int PoolSize => _poolSize;

    /// <summary>
    /// 使用指定实例执行单个 OCR
    /// </summary>
    public OcrResult Recognize(int instanceId, Bitmap bitmap)
    {
        if (instanceId < 0 || instanceId >= _poolSize)
        {
            return new OcrResult { Success = false, ErrorMessage = $"无效的实例ID: {instanceId}" };
        }

        return _instances[instanceId].Recognize(bitmap);
    }

    /// <summary>
    /// 使用负载均衡执行单个 OCR
    /// </summary>
    public OcrResult Recognize(Bitmap bitmap)
    {
        // 增加请求计数
        Interlocked.Increment(ref _requestCount);
        
        // 轮询选择实例
        int instanceId = GetNextInstanceId();
        return Recognize(instanceId, bitmap);
    }

    /// <summary>
    /// 异步使用负载均衡执行单个 OCR
    /// </summary>
    public Task<OcrResult> RecognizeAsync(Bitmap bitmap)
    {
        // 增加请求计数
        Interlocked.Increment(ref _requestCount);
        
        return Task.Run(() => Recognize(bitmap));
    }

    /// <summary>
    /// 批量并行执行 OCR
    /// </summary>
    public async Task<List<OcrResult>> RecognizeBatchAsync(List<Bitmap> bitmaps)
    {
        // 确保池大小足够
        EnsurePoolSize(bitmaps.Count);

        var tasks = new List<Task<OcrResult>>();

        for (int i = 0; i < bitmaps.Count; i++)
        {
            var instanceId = i % _poolSize; // 循环使用实例
            var bitmap = bitmaps[i];

            tasks.Add(Task.Run(() => _instances[instanceId].Recognize(bitmap)));
        }

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// 获取下一个实例 ID（轮询策略）
    /// </summary>
    private int GetNextInstanceId()
    {
        lock (_loadBalanceLock)
        {
            int instanceId = _nextInstanceId;
            _nextInstanceId = (_nextInstanceId + 1) % _poolSize;
            return instanceId;
        }
    }

    /// <summary>
    /// 获取最少使用的实例 ID（最少使用策略）
    /// </summary>
    private int GetLeastUsedInstanceId()
    {
        lock (_loadBalanceLock)
        {
            int minUsage = int.MaxValue;
            int bestInstanceId = 0;

            for (int i = 0; i < _poolSize; i++)
            {
                if (_instances[i].IsAvailable && _instances[i].UsageCount < minUsage)
                {
                    minUsage = _instances[i].UsageCount;
                    bestInstanceId = i;
                }
            }

            return bestInstanceId;
        }
    }

    /// <summary>
    /// 获取池状态
    /// </summary>
    public string GetStatus()
    {
        var available = _instances.Count(i => i.IsAvailable);
        var usageCounts = string.Join(", ", _instances.Select(i => $"#{i.Id}:{i.UsageCount}"));
        double requestRate = CalculateRequestRate();
        return $"OcrPool: {available}/{_poolSize} 实例可用 | 使用计数: [{usageCounts}] | 请求率: {requestRate:F2}/s";
    }

    /// <summary>
    /// 检查并执行动态扩缩容
    /// </summary>
    private void CheckScaling()
    {
        lock (_instanceLock)
        {
            double requestRate = CalculateRequestRate();
            var now = DateTime.Now;
            
            // 重置请求计数
            Interlocked.Exchange(ref _requestCount, 0);
            _lastScalingTime = now;
            
            Console.WriteLine($"[OcrPool] 负载检查: 请求率 {requestRate:F2}/s, 当前池大小 {_poolSize}");
            
            // 扩容逻辑
            if (requestRate > HighLoadThreshold && _poolSize < MaxPoolSize)
            {
                int newSize = Math.Min(_poolSize + ScaleUpIncrement, MaxPoolSize);
                Console.WriteLine($"[OcrPool] 高负载检测到，扩容: {_poolSize} -> {newSize}");
                EnsurePoolSize(newSize);
            }
            // 缩容逻辑
            else if (requestRate < LowLoadThreshold && _poolSize > MinPoolSize)
            {
                int newSize = Math.Max(_poolSize - ScaleDownDecrement, MinPoolSize);
                Console.WriteLine($"[OcrPool] 低负载检测到，缩容: {_poolSize} -> {newSize}");
                // 缩容实现（需要安全移除实例）
                ReducePoolSize(newSize);
            }
            else if (_poolSize > MaxPoolSize)
            {
                // 紧急缩容：如果池大小超过最大值，强制缩容
                Console.WriteLine($"[OcrPool] 池大小超过最大值，紧急缩容: {_poolSize} -> {MaxPoolSize}");
                ReducePoolSize(MaxPoolSize);
            }
        }
    }

    /// <summary>
    /// 计算当前请求率（每秒请求数）
    /// </summary>
    private double CalculateRequestRate()
    {
        var now = DateTime.Now;
        var elapsedSeconds = (now - _lastScalingTime).TotalSeconds;
        if (elapsedSeconds < 0.1) // 避免除以零
            return 0;
        
        int currentRequests = Interlocked.CompareExchange(ref _requestCount, 0, 0);
        return currentRequests / elapsedSeconds;
    }

    /// <summary>
    /// 减少池大小
    /// </summary>
    private void ReducePoolSize(int targetSize)
    {
        if (targetSize >= _poolSize || targetSize < MinPoolSize)
            return;
        
        Console.WriteLine($"[OcrPool] 缩容到 {targetSize} 个实例");
        
        // 按使用计数排序，移除使用最少的实例
        var instancesToRemove = _instances
            .OrderBy(i => i.UsageCount)
            .Take(_poolSize - targetSize)
            .ToList();
        
        foreach (var instance in instancesToRemove)
        {
            Console.WriteLine($"[OcrPool] 移除实例 #{instance.Id}");
            instance.Dispose();
            _instances.Remove(instance);
        }
        
        _poolSize = targetSize;
        Console.WriteLine($"[OcrPool] 缩容完成，当前池大小: {_poolSize}");
    }

    public void Dispose()
    {
        lock (_instanceLock)
        {
            _scalingTimer?.Dispose();
            
            foreach (var instance in _instances)
            {
                instance.Dispose();
            }
            _instances.Clear();
            _poolSize = 0;
        }
    }
}
