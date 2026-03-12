using System.Drawing;

namespace MomoBackend.Core;

/// <summary>
/// OCR 识别服务 - 统一使用 PaddleOCR 处理中英文
/// PaddleOCR V4 中文模型同时支持英文识别
/// </summary>
public class OcrService : IDisposable
{
    public OcrService() { }

    /// <summary>
    /// 识别图像中的文字（所有语言统一使用 PaddleOCR）
    /// </summary>
    public Task<OcrResult> RecognizeAsync(Bitmap bitmap, string language = "auto")
    {
        return PaddleOcrService.Instance.RecognizeAsync(bitmap, language);
    }

    public bool IsAvailable => PaddleOcrService.Instance.IsAvailable;

    public string GetStatus()
    {
        return $"OCR Engine: PaddleOCR V4 (all languages)";
    }

    public void Dispose() { }
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
