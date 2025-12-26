namespace MomoBackend.Models;

public class WindowInfo
{
    public long Hwnd { get; set; }
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public WindowRect? Rect { get; set; }
}

public class WindowRect

{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class CoordinatePoint
{
    public string Alias { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public long? Hwnd { get; set; }
}

public class ClickRequest
{
    public long Hwnd { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string? Mode { get; set; }  // stealth, quick_switch, post_message, send_message
    public string? Button { get; set; } // left, right
}

public class ClickResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class CursorPosition
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
/// OCR 识别区域配置
/// </summary>
public class OcrRegion
{
    /// <summary>
    /// 区域别名（用于显示和识别）
    /// </summary>
    public string Alias { get; set; } = "";

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Language { get; set; } = "auto"; // auto, zh, en
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// OCR 配置
/// </summary>
public class OcrConfig
{
    public List<OcrRegion> Regions { get; set; } = new();
    public int RefreshInterval { get; set; } = 3000; // 毫秒
    public bool AutoRefresh { get; set; } = false;
}

/// <summary>
/// 点击区域配置
/// </summary>
public class ClickPoint
{
    public string Alias { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string ClickMode { get; set; } = "fast_background";
    public string Button { get; set; } = "left";
}

/// <summary>
/// 应用程序配置（供 VS Code/Cursor 插件读取）
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 目标窗口标题（支持部分匹配）
    /// </summary>
    public string TargetWindowTitle { get; set; } = "";

    /// <summary>
    /// 目标进程名（如 "HuaweiMultiScreenCollaboration.exe"）
    /// </summary>
    public string TargetProcessName { get; set; } = "";

    /// <summary>
    /// 点击区域列表
    /// </summary>
    public List<ClickPoint> ClickPoints { get; set; } = new();

    /// <summary>
    /// OCR 区域列表
    /// </summary>
    public List<OcrRegion> OcrRegions { get; set; } = new();

    /// <summary>
    /// OCR 自动刷新间隔（毫秒）
    /// </summary>
    public int OcrRefreshInterval { get; set; } = 3000;

    /// <summary>
    /// 是否启用 OCR 自动刷新
    /// </summary>
    public bool OcrAutoRefresh { get; set; } = false;

    /// <summary>
    /// OCR 引擎类型: "windows" 或 "paddle"
    /// </summary>
    public string OcrEngine { get; set; } = "paddle";

    /// <summary>
    /// fast_background 点击模式参数
    /// </summary>
    public FastBackgroundSettings FastBackground { get; set; } = new();
}

/// <summary>
/// fast_background 点击模式的可调参数
/// </summary>
public class FastBackgroundSettings
{
    /// <summary>
    /// 目标窗口透明度 (0-255, 0=完全透明, 255=不透明)
    /// 建议值: 1-10 (近乎不可见但仍可点击)
    /// </summary>
    public byte WindowAlpha { get; set; } = 3;

    /// <summary>
    /// 窗口恢复后等待时间（毫秒）
    /// </summary>
    public int DelayAfterRestore { get; set; } = 30;

    /// <summary>
    /// 窗口激活后、点击前等待时间（毫秒）
    /// </summary>
    public int DelayBeforeClick { get; set; } = 20;

    /// <summary>
    /// 鼠标移动到目标位置后、点击前等待时间（毫秒）
    /// </summary>
    public int DelayAfterMove { get; set; } = 10;

    /// <summary>
    /// 点击后等待时间（毫秒），等待应用响应
    /// </summary>
    public int DelayAfterClick { get; set; } = 30;

    /// <summary>
    /// 切换回原窗口前等待时间（毫秒）
    /// </summary>
    public int DelayBeforeRestore { get; set; } = 20;

    /// <summary>
    /// 点击完成后是否最小化目标窗口
    /// </summary>
    public bool MinimizeAfterClick { get; set; } = true;

    /// <summary>
    /// 是否隐藏鼠标光标（点击时）
    /// </summary>
    public bool HideCursor { get; set; } = true;
}
