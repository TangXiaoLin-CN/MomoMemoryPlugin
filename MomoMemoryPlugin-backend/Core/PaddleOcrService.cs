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
    private static readonly object _lock = new();

    private PaddleOcrAll? _engine;
    private bool _isInitialized;
    private bool _isInitializing;
    private string _initError = "";

    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static PaddleOcrService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new PaddleOcrService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 私有构造函数，使用单例模式
    /// </summary>
    private PaddleOcrService()
    {
        // 延迟初始化，在后台线程中进行
        Task.Run(() => Initialize());
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
        if (_isInitialized || _isInitializing) return;

        _isInitializing = true;
        try
        {
            // 使用中文 V4 模型（本地模型，无需下载）
            var model = LocalFullModels.ChineseV4;

            _engine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,    // 允许识别有角度的文字
                Enable180Classification = false  // 不启用180度分类（提高速度）
            };

            _isInitialized = true;
            _initError = "";
            System.Diagnostics.Debug.WriteLine("[PaddleOCR] Sdcb.PaddleOCR 引擎初始化成功 (ChineseV4 模型)");
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            _initError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[PaddleOCR] 初始化失败: {ex.Message}");
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// 识别图像中的文字
    /// </summary>
    public OcrResult Recognize(Bitmap bitmap)
    {
        var result = new OcrResult();

        // 等待初始化完成（最多等待10秒）
        var waitCount = 0;
        while (_isInitializing && waitCount < 100)
        {
            Thread.Sleep(100);
            waitCount++;
        }

        if (!_isInitialized || _engine == null)
        {
            result.Success = false;
            result.ErrorMessage = _isInitializing
                ? "PaddleOCR 引擎正在初始化，请稍候..."
                : $"PaddleOCR 引擎未初始化: {_initError}";
            return result;
        }

        try
        {
            // 将 Bitmap 转换为 OpenCV Mat
            using var mat = BitmapToMat(bitmap);

            // 执行 OCR 识别
            var ocrResult = _engine.Run(mat);

            result.Success = true;
            // 将换行符替换为空格
            result.Text = (ocrResult.Text ?? "").Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            result.Confidence = ocrResult.Regions.Any() ? ocrResult.Regions.Average(r => r.Score) : 0;

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
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
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
        // 确保是 24bpp RGB 格式
        if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
        {
            var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(converted))
            {
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
            }
            return BitmapConverter.ToMat(converted);
        }

        return BitmapConverter.ToMat(bitmap);
    }

    /// <summary>
    /// 检查 OCR 是否可用
    /// </summary>
    public bool IsAvailable => _isInitialized;

    /// <summary>
    /// 获取引擎状态信息
    /// </summary>
    public string GetStatus()
    {
        if (_isInitialized)
        {
            return "PaddleOCR: ✓ 就绪 (Sdcb v3)";
        }
        else if (_isInitializing)
        {
            return "PaddleOCR: ⏳ 初始化中...";
        }
        else
        {
            return $"PaddleOCR: ✗ ({_initError})";
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
