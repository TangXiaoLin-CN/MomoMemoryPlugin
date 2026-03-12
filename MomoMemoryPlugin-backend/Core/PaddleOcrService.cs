using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace MomoBackend.Core;

/// <summary>
/// PaddleOCR 识别服务 - 使用 Sdcb.PaddleOCR 实现高精度中英文识别
/// 中文使用 ChineseV4 模型，英文使用 EnglishV4 模型
/// 使用单例模式，避免重复初始化
/// </summary>
public class PaddleOcrService : IDisposable
{
    private static PaddleOcrService? _instance;
    private static readonly object _instanceLock = new();
    private static readonly object _chineseLock = new();
    private static readonly object _englishLock = new();

    private PaddleOcrAll? _chineseEngine;
    private PaddleOcrAll? _englishEngine;
    private bool _chineseInitialized;
    private bool _englishInitialized;
    private string _chineseInitError = "";
    private string _englishInitError = "";
    private int _chineseConsecutiveFailures = 0;
    private int _englishConsecutiveFailures = 0;
    private const int MaxConsecutiveFailures = 2;

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

    private PaddleOcrService()
    {
        InitializeChinese();
        InitializeEnglish();
    }

    public static void Warmup()
    {
        _ = Instance;
    }

    private void InitializeChinese()
    {
        if (_chineseInitialized) return;

        try
        {
            Console.WriteLine("[PaddleOCR] 初始化中文引擎 (ChineseV4)...");
            var model = LocalFullModels.ChineseV4;
            _chineseEngine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,
                Enable180Classification = false
            };
            _chineseInitialized = true;
            _chineseInitError = "";
            Console.WriteLine("[PaddleOCR] 中文引擎初始化成功");
        }
        catch (Exception ex)
        {
            _chineseInitialized = false;
            _chineseInitError = ex.Message;
            Console.WriteLine($"[PaddleOCR] 中文引擎初始化失败: {ex.Message}");
        }
    }

    private void InitializeEnglish()
    {
        if (_englishInitialized) return;

        try
        {
            Console.WriteLine("[PaddleOCR] 初始化英文引擎 (EnglishV4)...");
            var model = LocalFullModels.EnglishV4;
            _englishEngine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,
                Enable180Classification = false
            };
            _englishInitialized = true;
            _englishInitError = "";
            Console.WriteLine("[PaddleOCR] 英文引擎初始化成功");
        }
        catch (Exception ex)
        {
            _englishInitialized = false;
            _englishInitError = ex.Message;
            Console.WriteLine($"[PaddleOCR] 英文引擎初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 识别图像中的文字，根据语言选择引擎
    /// </summary>
    public OcrResult Recognize(Bitmap bitmap, string language = "auto")
    {
        var lang = language.ToLower();
        if (lang == "en" || lang == "english")
        {
            return RecognizeWithEngine(_englishEngine, _englishInitialized, _englishInitError,
                ref _englishConsecutiveFailures, _englishLock, "English", bitmap,
                () => { ResetEngine(ref _englishEngine, ref _englishInitialized, ref _englishConsecutiveFailures, InitializeEnglish); });
        }
        else
        {
            return RecognizeWithEngine(_chineseEngine, _chineseInitialized, _chineseInitError,
                ref _chineseConsecutiveFailures, _chineseLock, "Chinese", bitmap,
                () => { ResetEngine(ref _chineseEngine, ref _chineseInitialized, ref _chineseConsecutiveFailures, InitializeChinese); });
        }
    }

    private OcrResult RecognizeWithEngine(PaddleOcrAll? engine, bool initialized, string initError,
        ref int consecutiveFailures, object lockObj, string engineName, Bitmap bitmap, Action resetAction)
    {
        var result = new OcrResult();

        lock (lockObj)
        {
            if (consecutiveFailures >= MaxConsecutiveFailures)
            {
                Console.WriteLine($"[PaddleOCR-{engineName}] 连续失败 {consecutiveFailures} 次，重新初始化...");
                resetAction();
                // Re-read after reset
                engine = engineName == "English" ? _englishEngine : _chineseEngine;
                initialized = engineName == "English" ? _englishInitialized : _chineseInitialized;
                initError = engineName == "English" ? _englishInitError : _chineseInitError;
            }

            if (!initialized || engine == null)
            {
                result.Success = false;
                result.ErrorMessage = string.IsNullOrEmpty(initError)
                    ? $"PaddleOCR {engineName} 引擎未初始化"
                    : $"PaddleOCR 引擎初始化失败: {initError}";
                return result;
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var mat = BitmapToMat(bitmap);
                    if (mat.Empty())
                    {
                        result.Success = false;
                        result.ErrorMessage = "图像转换失败：Mat 为空";
                        return result;
                    }

                    var ocrResult = engine.Run(mat);

                    result.Success = true;
                    result.Text = (ocrResult.Text ?? "").Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

                    var confidence = ocrResult.Regions.Any() ? ocrResult.Regions.Average(r => r.Score) : 0;
                    result.Confidence = double.IsNaN(confidence) || double.IsInfinity(confidence) ? 0 : (float)confidence;

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

                    consecutiveFailures = 0;
                    return result;
                }
                catch (Exception ex)
                {
                    if (attempt == 0)
                    {
                        Console.WriteLine($"[PaddleOCR-{engineName}] 识别失败，立即重试: {ex.Message}");
                        continue;
                    }

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    consecutiveFailures++;
                    Console.WriteLine($"[PaddleOCR-{engineName}] 识别失败 (连续第 {consecutiveFailures} 次): {ex.Message}");
                }
            }
        }

        return result;
    }

    private void ResetEngine(ref PaddleOcrAll? engine, ref bool initialized, ref int failures, Action initAction)
    {
        try
        {
            engine?.Dispose();
            engine = null;
            initialized = false;
            failures = 0;
            initAction();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PaddleOCR] 重置引擎失败: {ex.Message}");
        }
    }

    public Task<OcrResult> RecognizeAsync(Bitmap bitmap, string language = "auto")
    {
        return Task.Run(() => Recognize(bitmap, language));
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

    public bool IsAvailable => _chineseInitialized && _chineseEngine != null;

    public string GetStatus()
    {
        var cn = _chineseInitialized ? "就绪" : $"错误({_chineseInitError})";
        var en = _englishInitialized ? "就绪" : $"错误({_englishInitError})";
        return $"PaddleOCR: 中文={cn}, 英文={en}";
    }

    public void Dispose()
    {
        lock (_chineseLock) { _chineseEngine?.Dispose(); _chineseEngine = null; _chineseInitialized = false; }
        lock (_englishLock) { _englishEngine?.Dispose(); _englishEngine = null; _englishInitialized = false; }
    }
}
