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
    public string Name { get; set; } = "";
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
    public OcrRegion Region1 { get; set; } = new() { Name = "区域1", X = 0, Y = 0, Width = 200, Height = 50 };
    public OcrRegion Region2 { get; set; } = new() { Name = "区域2", X = 0, Y = 50, Width = 200, Height = 50 };
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
    /// OCR 区域1 配置
    /// </summary>
    public OcrRegion OcrRegion1 { get; set; } = new() { Name = "区域1", X = 0, Y = 0, Width = 200, Height = 50 };

    /// <summary>
    /// OCR 区域2 配置
    /// </summary>
    public OcrRegion OcrRegion2 { get; set; } = new() { Name = "区域2", X = 0, Y = 50, Width = 200, Height = 50 };

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
}
