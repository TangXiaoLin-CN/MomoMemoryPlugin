using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace MomoBackend.Core;

/// <summary>
/// OCR 识别服务 - 使用 Windows.Media.Ocr
/// </summary>
public class OcrService
{
    private OcrEngine? _chineseEngine;
    private OcrEngine? _englishEngine;

    public OcrService()
    {
        InitializeEngines();
    }

    private void InitializeEngines()
    {
        // 尝试创建中文引擎
        try
        {
            var chineseLanguage = new Windows.Globalization.Language("zh-Hans-CN");
            if (OcrEngine.IsLanguageSupported(chineseLanguage))
            {
                _chineseEngine = OcrEngine.TryCreateFromLanguage(chineseLanguage);
            }
        }
        catch { }

        // 尝试创建英文引擎
        try
        {
            var englishLanguage = new Windows.Globalization.Language("en-US");
            if (OcrEngine.IsLanguageSupported(englishLanguage))
            {
                _englishEngine = OcrEngine.TryCreateFromLanguage(englishLanguage);
            }
        }
        catch { }

        // 如果都失败，使用系统默认语言
        if (_chineseEngine == null && _englishEngine == null)
        {
            _chineseEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            _englishEngine = _chineseEngine;
        }
    }

    /// <summary>
    /// 获取可用的 OCR 语言列表
    /// </summary>
    public List<string> GetAvailableLanguages()
    {
        var languages = new List<string>();
        foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
        {
            languages.Add($"{lang.DisplayName} ({lang.LanguageTag})");
        }
        return languages;
    }

    /// <summary>
    /// 识别图像中的文字
    /// </summary>
    public async Task<OcrResult> RecognizeAsync(Bitmap bitmap, string language = "auto")
    {
        var result = new OcrResult();

        try
        {
            // 选择引擎
            OcrEngine? engine = language.ToLower() switch
            {
                "zh" or "chinese" or "zh-hans" => _chineseEngine,
                "en" or "english" => _englishEngine,
                _ => _chineseEngine ?? _englishEngine // auto: 优先中文
            };

            if (engine == null)
            {
                result.Success = false;
                result.ErrorMessage = "OCR 引擎未初始化。请确保系统已安装相应的语言包。";
                return result;
            }

            // 将 Bitmap 转换为 SoftwareBitmap
            using var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmap);
            if (softwareBitmap == null)
            {
                result.Success = false;
                result.ErrorMessage = "图像转换失败";
                return result;
            }

            // 执行 OCR
            var ocrResult = await engine.RecognizeAsync(softwareBitmap);

            result.Success = true;
            result.Text = ocrResult.Text;
            result.Lines = ocrResult.Lines.Select(l => new OcrLine
            {
                Text = l.Text,
                Words = l.Words.Select(w => new OcrWord
                {
                    Text = w.Text,
                    BoundingRect = new Rectangle(
                        (int)w.BoundingRect.X,
                        (int)w.BoundingRect.Y,
                        (int)w.BoundingRect.Width,
                        (int)w.BoundingRect.Height)
                }).ToList()
            }).ToList();

            // 计算置信度（Windows OCR 不直接提供，根据识别结果估算）
            result.Confidence = string.IsNullOrEmpty(result.Text) ? 0 : 0.9f;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 将 System.Drawing.Bitmap 转换为 SoftwareBitmap
    /// </summary>
    private async Task<SoftwareBitmap?> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();

            // 保存为 PNG 到内存流
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var bytes = ms.ToArray();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
            }

            // 解码为 SoftwareBitmap
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            return softwareBitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查 OCR 是否可用
    /// </summary>
    public bool IsAvailable => _chineseEngine != null || _englishEngine != null;

    /// <summary>
    /// 获取引擎状态信息
    /// </summary>
    public string GetStatus()
    {
        var status = new List<string>();

        if (_chineseEngine != null)
            status.Add("中文: ✓");
        else
            status.Add("中文: ✗ (需要安装语言包)");

        if (_englishEngine != null)
            status.Add("英文: ✓");
        else
            status.Add("英文: ✗");

        return string.Join(", ", status);
    }
}

/// <summary>
/// OCR 识别结果
/// </summary>
public class OcrResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public float Confidence { get; set; }
    public List<OcrLine> Lines { get; set; } = new();
}

/// <summary>
/// OCR 识别行
/// </summary>
public class OcrLine
{
    public string Text { get; set; } = "";
    public List<OcrWord> Words { get; set; } = new();
}

/// <summary>
/// OCR 识别词
/// </summary>
public class OcrWord
{
    public string Text { get; set; } = "";
    public Rectangle BoundingRect { get; set; }
}
