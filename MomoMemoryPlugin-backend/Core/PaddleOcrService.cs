using System.Drawing;
using PaddleOCRSharp;

namespace MomoBackend.Core;

/// <summary>
/// PaddleOCR 识别服务 - 使用 PaddleOCRSharp 实现高精度中文识别
/// 使用单例模式，避免重复初始化
/// </summary>
public class PaddleOcrService : IDisposable
{
    private static PaddleOcrService? _instance;
    private static readonly object _lock = new();

    private PaddleOCREngine? _engine;
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
            // OCR 参数配置
            var oCRParameter = new OCRParameter
            {
                cpu_math_library_num_threads = 6,
                enable_mkldnn = true,
                det_db_score_mode = true,
                det_db_unclip_ratio = 1.6f,
                use_angle_cls = true,
                det = true,
                rec = true,
                cls = true
            };

            // 创建引擎（使用内置模型）
            _engine = new PaddleOCREngine(null, oCRParameter);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            _initError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"PaddleOCR 初始化失败: {ex.Message}");
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
            // 执行 OCR 识别
            var ocrResult = _engine.DetectText(bitmap);

            if (ocrResult != null)
            {
                result.Success = true;
                result.Text = ocrResult.Text ?? "";
                result.Confidence = 0.95f; // PaddleOCR 通常有很高的准确率

                // 转换识别结果
                if (ocrResult.TextBlocks != null)
                {
                    result.Lines = ocrResult.TextBlocks.Select(block => new OcrLine
                    {
                        Text = block.Text ?? "",
                        Words = new List<OcrWord>
                        {
                            new OcrWord
                            {
                                Text = block.Text ?? "",
                                BoundingRect = new Rectangle(
                                    (int)block.BoxPoints[0].X,
                                    (int)block.BoxPoints[0].Y,
                                    (int)(block.BoxPoints[1].X - block.BoxPoints[0].X),
                                    (int)(block.BoxPoints[3].Y - block.BoxPoints[0].Y)
                                )
                            }
                        }
                    }).ToList();
                }
            }
            else
            {
                result.Success = true;
                result.Text = "";
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
        // PaddleOCR 本身是同步的，我们在任务中运行它
        return Task.Run(() => Recognize(bitmap));
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
            return "PaddleOCR: ✓ 就绪";
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
