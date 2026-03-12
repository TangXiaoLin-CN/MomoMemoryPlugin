using System.Drawing;
using System.Drawing.Imaging;
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
                        Console.WriteLine($"[OcrPool] 实例 #{Id}: OCR 失败，立即重试... ({ex.Message})");
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
/// 使用固定池大小，避免动态扩缩容导致的竞态条件
/// </summary>
public class PaddleOcrPool : IDisposable
{
    private static PaddleOcrPool? _instance;
    private static readonly object _instanceLock = new();

    private readonly List<OcrInstance> _instances = new();
    private readonly int _poolSize;
    private int _nextInstanceId = 0; // 轮询索引

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
        // 固定池大小：基于 CPU 核心数，最少 2 个，最多 8 个
        _poolSize = Math.Min(Math.Max(2, Environment.ProcessorCount - 1), 8);
        Console.WriteLine($"[OcrPool] CPU 核心数: {Environment.ProcessorCount}, 固定池大小: {_poolSize}");

        for (int i = 0; i < _poolSize; i++)
        {
            _instances.Add(new OcrInstance(i));
        }
    }

    /// <summary>
    /// 获取当前池大小
    /// </summary>
    public int PoolSize => _poolSize;

    /// <summary>
    /// 使用轮询负载均衡执行单个 OCR
    /// </summary>
    public OcrResult Recognize(Bitmap bitmap)
    {
        int instanceId;
        lock (_instanceLock)
        {
            instanceId = _nextInstanceId;
            _nextInstanceId = (_nextInstanceId + 1) % _poolSize;
        }
        return _instances[instanceId].Recognize(bitmap);
    }

    /// <summary>
    /// 异步使用负载均衡执行单个 OCR
    /// </summary>
    public Task<OcrResult> RecognizeAsync(Bitmap bitmap)
    {
        return Task.Run(() => Recognize(bitmap));
    }

    /// <summary>
    /// 批量并行执行 OCR
    /// </summary>
    public async Task<List<OcrResult>> RecognizeBatchAsync(List<Bitmap> bitmaps)
    {
        var tasks = new List<Task<OcrResult>>();

        for (int i = 0; i < bitmaps.Count; i++)
        {
            var instanceId = i % _poolSize;
            var bitmap = bitmaps[i];
            tasks.Add(Task.Run(() => _instances[instanceId].Recognize(bitmap)));
        }

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// 获取池状态
    /// </summary>
    public string GetStatus()
    {
        var available = _instances.Count(i => i.IsAvailable);
        var usageCounts = string.Join(", ", _instances.Select(i => $"#{i.Id}:{i.UsageCount}"));
        return $"OcrPool: {available}/{_poolSize} 实例可用 | 使用计数: [{usageCounts}]";
    }

    public void Dispose()
    {
        lock (_instanceLock)
        {
            foreach (var instance in _instances)
            {
                instance.Dispose();
            }
            _instances.Clear();
        }
    }
}
