using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace MomoBackend.Core;

/// <summary>
/// PaddleOCR 识别服务 - 使用 Sdcb.PaddleOCR 实现高精度中文识别
/// 使用单例模式，避免重复初始化
///
/// 已从 PaddleOCRSharp 迁移到 Sdcb.PaddleOCR (更可信的开源库)
/// GitHub: https://github.com/sdcb/PaddleSharp
/// </summary>
public class PaddleOcrService : IDisposable
{
    private static PaddleOcrService? _instance;
    private static readonly object _instanceLock = new();
    private static readonly object _ocrLock = new(); // 用于 OCR 操作的锁

    private PaddleOcrAll? _engine;
    private bool _isInitialized;
    private string _initError = "";
    private int _consecutiveFailures = 0;
    private const int MaxConsecutiveFailures = 2; // 连续失败2次后重新初始化
    private DateTime _lastOcrTime = DateTime.MinValue;
    private const int OcrCooldownMs = 300; // OCR 调用之间的冷却时间（毫秒）- 增加到300ms提高稳定性
    private DateTime _lastSuccessTime = DateTime.MinValue;
    private const int EngineIdleResetMs = 10000; // 引擎空闲超过10秒后预防性重置

    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static PaddleOcrService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new PaddleOcrService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 私有构造函数，使用单例模式
    /// 同步初始化引擎，确保完全就绪后才能使用
    /// </summary>
    private PaddleOcrService()
    {
        Initialize();
    }

    /// <summary>
    /// 预热引擎（可在程序启动时调用）
    /// </summary>
    public static void Warmup()
    {
        // 访问 Instance 会触发初始化
        _ = Instance;
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            Console.WriteLine("[PaddleOCR] 开始初始化 Sdcb.PaddleOCR 引擎...");

            // 使用中文 V4 模型（本地模型，无需下载）
            var model = LocalFullModels.ChineseV4;

            _engine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,    // 允许识别有角度的文字
                Enable180Classification = false  // 不启用180度分类（提高速度）
            };

            _isInitialized = true;
            _initError = "";
            Console.WriteLine("[PaddleOCR] Sdcb.PaddleOCR 引擎初始化成功 (ChineseV4 模型)");
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            _initError = ex.Message;
            Console.WriteLine($"[PaddleOCR] 初始化失败: {ex.Message}");
            Console.WriteLine($"[PaddleOCR] 堆栈: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 识别图像中的文字
    /// </summary>
    public OcrResult Recognize(Bitmap bitmap)
    {
        var result = new OcrResult();

        // 使用锁确保 OCR 操作是线程安全的
        lock (_ocrLock)
        {
            // 等待冷却时间，避免调用太频繁
            var timeSinceLastOcr = (DateTime.Now - _lastOcrTime).TotalMilliseconds;
            if (timeSinceLastOcr < OcrCooldownMs)
            {
                var waitTime = (int)(OcrCooldownMs - timeSinceLastOcr);
                Thread.Sleep(waitTime);
            }

            // 预防性重置：如果引擎空闲太久，可能需要重新热身
            var timeSinceLastSuccess = (DateTime.Now - _lastSuccessTime).TotalMilliseconds;
            if (_lastSuccessTime != DateTime.MinValue && timeSinceLastSuccess > EngineIdleResetMs)
            {
                Console.WriteLine($"[PaddleOCR] 引擎空闲 {timeSinceLastSuccess:F0}ms，预防性重置...");
                ResetEngine();
            }

            // 检查是否需要重新初始化（连续失败后）
            if (_consecutiveFailures >= MaxConsecutiveFailures)
            {
                Console.WriteLine($"[PaddleOCR] 连续失败 {_consecutiveFailures} 次，尝试重新初始化引擎...");
                ResetEngine();
            }

            if (!_isInitialized || _engine == null)
            {
                result.Success = false;
                result.ErrorMessage = string.IsNullOrEmpty(_initError)
                    ? "PaddleOCR 引擎未初始化"
                    : $"PaddleOCR 引擎初始化失败: {_initError}";
                return result;
            }

            // 尝试执行 OCR，失败则立即重试一次
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    // 将 Bitmap 转换为 OpenCV Mat
                    using var mat = BitmapToMat(bitmap);

                    if (mat.Empty())
                    {
                        result.Success = false;
                        result.ErrorMessage = "图像转换失败：Mat 为空";
                        _lastOcrTime = DateTime.Now;
                        return result;
                    }

                    // 执行 OCR 识别
                    var ocrResult = _engine.Run(mat);

                    result.Success = true;
                    // 将换行符替换为空格
                    result.Text = (ocrResult.Text ?? "").Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

                    // 计算置信度，确保不会产生 Infinity/NaN
                    var confidence = ocrResult.Regions.Any() ? ocrResult.Regions.Average(r => r.Score) : 0;
                    result.Confidence = double.IsNaN(confidence) || double.IsInfinity(confidence) ? 0 : confidence;

                    // 转换识别结果为 Lines 格式
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

                    // 成功后重置失败计数
                    _consecutiveFailures = 0;
                    _lastOcrTime = DateTime.Now;
                    _lastSuccessTime = DateTime.Now;
                    return result;
                }
                catch (Exception ex)
                {
                    if (attempt == 0)
                    {
                        // 第一次失败，等待更长时间再重试
                        Console.WriteLine($"[PaddleOCR] OCR 识别失败，等待 200ms 后重试: {ex.Message}");
                        Thread.Sleep(200);
                        continue;
                    }

                    // 第二次也失败了
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _consecutiveFailures++;
                    Console.WriteLine($"[PaddleOCR] OCR 识别失败 (连续第 {_consecutiveFailures} 次): {ex.Message}");
                }
            }

            _lastOcrTime = DateTime.Now;
        }

        return result;
    }

    /// <summary>
    /// 重置并重新初始化引擎
    /// </summary>
    private void ResetEngine()
    {
        try
        {
            Console.WriteLine("[PaddleOCR] 正在重置引擎...");
            _engine?.Dispose();
            _engine = null;
            _isInitialized = false;
            _consecutiveFailures = 0;

            // 等待一小段时间让资源释放
            Thread.Sleep(100);

            // 重新初始化
            Initialize();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PaddleOCR] 重置引擎失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 异步识别图像中的文字
    /// </summary>
    public Task<OcrResult> RecognizeAsync(Bitmap bitmap, string language = "auto")
    {
        return Task.Run(() => Recognize(bitmap));
    }

    /// <summary>
    /// 将 System.Drawing.Bitmap 转换为 OpenCvSharp.Mat
    /// </summary>
    private static Mat BitmapToMat(Bitmap bitmap)
    {
        try
        {
            // 确保是 24bpp RGB 格式
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
        catch (Exception ex)
        {
            Console.WriteLine($"[PaddleOCR] Bitmap 转 Mat 失败: {ex.Message}");
            return new Mat();
        }
    }

    /// <summary>
    /// 检查 OCR 是否可用
    /// </summary>
    public bool IsAvailable => _isInitialized && _engine != null;

    /// <summary>
    /// 获取引擎状态信息
    /// </summary>
    public string GetStatus()
    {
        if (_isInitialized && _engine != null)
        {
            return "PaddleOCR: 就绪 (Sdcb v3)";
        }
        else if (!string.IsNullOrEmpty(_initError))
        {
            return $"PaddleOCR: 错误 ({_initError})";
        }
        else
        {
            return "PaddleOCR: 未初始化";
        }
    }

    public void Dispose()
    {
        lock (_ocrLock)
        {
            _engine?.Dispose();
            _engine = null;
            _isInitialized = false;
        }
    }
}
