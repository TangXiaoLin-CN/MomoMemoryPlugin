# 后台窗口点击工具 - 需求文档

## 一、项目背景

需要开发一个 Windows 工具，能够在**不影响用户当前操作**的情况下，对指定窗口进行鼠标点击操作。

### 核心需求
- **后台点击**：点击时不移动用户的鼠标光标
- **不抢占焦点**：点击后用户当前的工作窗口保持在前台
- **无感知**：用户在操作过程中几乎感知不到目标窗口的变化

## 二、功能需求

### 2.1 窗口管理
| 功能 | 说明 |
|------|------|
| 枚举窗口 | 获取系统所有可见窗口列表 |
| 窗口信息 | 获取窗口标题、进程名、句柄(HWND)、位置大小 |
| 窗口选择 | 用户可以选择并保存目标窗口 |
| 窗口验证 | 检查窗口是否仍然有效 |

### 2.2 坐标管理
| 功能 | 说明 |
|------|------|
| 坐标拾取 | 记录鼠标相对于目标窗口的坐标 |
| 坐标别名 | 为坐标设置易记的名称 |
| 坐标存储 | 持久化保存坐标配置 |
| 坐标列表 | 管理已保存的坐标（增删改查） |

### 2.3 点击功能
| 功能 | 说明 |
|------|------|
| 后台点击 | 在不影响用户的情况下点击目标窗口 |
| 左键/右键 | 支持左键和右键点击 |
| 单击/双击 | 支持单击和双击 |
| 点击反馈 | 提示点击是否成功 |

### 2.4 可选功能（OCR）
| 功能 | 说明 |
|------|------|
| 区域截图 | 截取目标窗口指定区域 |
| 文字识别 | OCR 识别截图中的文字 |
| 定时刷新 | 定时执行 OCR 并显示结果 |

## 三、技术要点

### 3.1 Windows API 需要用到的函数

```csharp
// 窗口枚举
[DllImport("user32.dll")]
static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

[DllImport("user32.dll")]
static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

[DllImport("user32.dll")]
static extern bool IsWindowVisible(IntPtr hWnd);

// 窗口信息
[DllImport("user32.dll")]
static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[DllImport("user32.dll")]
static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

// 窗口操作
[DllImport("user32.dll")]
static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
static extern bool SetForegroundWindow(IntPtr hWnd);

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

// 消息发送
[DllImport("user32.dll")]
static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll")]
static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

// 鼠标操作
[DllImport("user32.dll")]
static extern bool SetCursorPos(int X, int Y);

[DllImport("user32.dll")]
static extern bool GetCursorPos(out POINT lpPoint);

[DllImport("user32.dll")]
static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

// 窗口透明度
[DllImport("user32.dll")]
static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

[DllImport("user32.dll")]
static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

[DllImport("user32.dll")]
static extern int GetWindowLong(IntPtr hWnd, int nIndex);

// 子窗口
[DllImport("user32.dll")]
static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, POINT pt, uint uFlags);

[DllImport("user32.dll")]
static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

// 窗口位置
[DllImport("user32.dll")]
static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

// 线程输入
[DllImport("user32.dll")]
static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

[DllImport("kernel32.dll")]
static extern uint GetCurrentThreadId();
```

### 3.2 鼠标消息常量

```csharp
const uint WM_MOUSEMOVE = 0x0200;
const uint WM_LBUTTONDOWN = 0x0201;
const uint WM_LBUTTONUP = 0x0202;
const uint WM_LBUTTONDBLCLK = 0x0203;
const uint WM_RBUTTONDOWN = 0x0204;
const uint WM_RBUTTONUP = 0x0205;
const uint WM_RBUTTONDBLCLK = 0x0206;

// mouse_event 标志
const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
const uint MOUSEEVENTF_LEFTUP = 0x0004;
const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
const uint MOUSEEVENTF_RIGHTUP = 0x0010;

// lParam 构造
IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));
```

## 四、后台点击方案

### 方案对比

| 方案 | 原理 | 优点 | 缺点 | 适用场景 |
|------|------|------|------|----------|
| PostMessage | 异步发送鼠标消息 | 不移动鼠标 | 很多程序不响应 | 标准 Win32 控件 |
| SendMessage | 同步发送鼠标消息 | 不移动鼠标 | 很多程序不响应 | 标准 Win32 控件 |
| 子窗口定位 | 找到子控件发送消息 | 更精准 | 复杂，不一定有效 | 有子控件的程序 |
| 快速前台切换 | 激活窗口→点击→切回 | 几乎都有效 | 会有闪烁 | 通用 |
| 透明化点击 | 透明→激活→点击→恢复 | 用户看不到 | 部分程序无效 | 支持透明的程序 |
| 屏幕外点击 | 移到屏外→激活→点击→移回 | 用户看不到 | 部分程序会阻止 | 允许移动的程序 |

### 方案 1：PostMessage（纯后台，但很多程序不响应）

```csharp
public static void BackgroundClick(IntPtr hwnd, int x, int y, bool rightClick = false)
{
    IntPtr lParam = MakeLParam(x, y);

    // 发送鼠标移动
    PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
    Thread.Sleep(10);

    if (rightClick)
    {
        PostMessage(hwnd, WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
        Thread.Sleep(30);
        PostMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
    }
    else
    {
        PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
        Thread.Sleep(30);
        PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
    }
}
```

### 方案 2：快速前台切换（最可靠，但会短暂闪烁）

```csharp
public static void QuickSwitchClick(IntPtr targetHwnd, int absoluteX, int absoluteY)
{
    // 保存当前状态
    IntPtr prevForeground = GetForegroundWindow();
    GetCursorPos(out POINT originalPos);

    // 附加线程输入（允许 SetForegroundWindow）
    uint targetThreadId = GetWindowThreadProcessId(targetHwnd, out _);
    uint currentThreadId = GetCurrentThreadId();
    bool attached = AttachThreadInput(currentThreadId, targetThreadId, true);

    try
    {
        // 激活目标窗口
        ShowWindow(targetHwnd, SW_RESTORE);
        SetForegroundWindow(targetHwnd);
        Thread.Sleep(30);

        // 移动鼠标并点击
        SetCursorPos(absoluteX, absoluteY);
        Thread.Sleep(10);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(30);

        // 恢复前台窗口
        if (prevForeground != IntPtr.Zero && prevForeground != targetHwnd)
        {
            SetForegroundWindow(prevForeground);
        }

        // 恢复鼠标位置
        SetCursorPos(originalPos.X, originalPos.Y);
    }
    finally
    {
        if (attached)
        {
            AttachThreadInput(currentThreadId, targetThreadId, false);
        }
    }
}
```

### 方案 3：透明化点击（用户看不到窗口变化）

```csharp
public static void TransparentClick(IntPtr targetHwnd, int x, int y)
{
    IntPtr prevForeground = GetForegroundWindow();

    // 添加透明样式
    int exStyle = GetWindowLong(targetHwnd, GWL_EXSTYLE);
    SetWindowLong(targetHwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

    // 设为完全透明
    SetLayeredWindowAttributes(targetHwnd, 0, 0, LWA_ALPHA);
    Thread.Sleep(30);

    try
    {
        // 激活并点击（窗口不可见）
        SetForegroundWindow(targetHwnd);
        Thread.Sleep(30);

        // 使用 PostMessage 或 SendMessage 点击
        IntPtr lParam = MakeLParam(x, y);
        PostMessage(targetHwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
        Thread.Sleep(30);
        PostMessage(targetHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        Thread.Sleep(30);

        // 恢复前台窗口
        if (prevForeground != IntPtr.Zero)
        {
            SetForegroundWindow(prevForeground);
        }
    }
    finally
    {
        // 恢复透明度
        SetLayeredWindowAttributes(targetHwnd, 0, 255, LWA_ALPHA);
        Thread.Sleep(10);

        // 移除透明样式（如果之前没有）
        if ((exStyle & WS_EX_LAYERED) == 0)
        {
            SetWindowLong(targetHwnd, GWL_EXSTYLE, exStyle);
        }
    }
}

const int GWL_EXSTYLE = -20;
const int WS_EX_LAYERED = 0x80000;
const int LWA_ALPHA = 0x2;
```

### 方案 4：屏幕外点击

```csharp
public static void OffscreenClick(IntPtr targetHwnd, int relativeX, int relativeY)
{
    IntPtr prevForeground = GetForegroundWindow();

    // 获取原始位置
    GetWindowRect(targetHwnd, out RECT originalRect);
    int origX = originalRect.Left;
    int origY = originalRect.Top;
    int width = originalRect.Right - originalRect.Left;
    int height = originalRect.Bottom - originalRect.Top;

    // 移动到屏幕外
    MoveWindow(targetHwnd, -30000, origY, width, height, false);
    Thread.Sleep(50);

    try
    {
        // 激活并点击
        SetForegroundWindow(targetHwnd);
        Thread.Sleep(50);

        IntPtr lParam = MakeLParam(relativeX, relativeY);
        PostMessage(targetHwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
        Thread.Sleep(30);
        PostMessage(targetHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        Thread.Sleep(50);

        // 先恢复前台窗口
        if (prevForeground != IntPtr.Zero)
        {
            SetForegroundWindow(prevForeground);
            Thread.Sleep(50);
        }
    }
    finally
    {
        // 再移回原位
        MoveWindow(targetHwnd, origX, origY, width, height, true);
    }
}
```

## 五、已测试的结论

基于之前的测试，目标程序的特性：

| 测试项 | 结果 |
|--------|------|
| PostMessage 后台点击 | ❌ 不响应 |
| SendMessage 后台点击 | ❌ 不响应 |
| 窗口在前台时 ControlClick | ✅ 有效，且不抢占鼠标 |
| 窗口在后台时 ControlClick | ❌ 不响应 |

**结论**：目标程序只在获得焦点时才处理鼠标输入，必须先激活窗口才能点击。

## 六、推荐实现方案

鉴于目标程序的特性，推荐使用**透明化 + 前台激活**的组合方案：

```csharp
public class StealthClicker
{
    public static void Click(IntPtr targetHwnd, int relativeX, int relativeY)
    {
        IntPtr prevForeground = GetForegroundWindow();

        // 1. 设置窗口透明（用户看不到）
        MakeWindowTransparent(targetHwnd, 0);
        Thread.Sleep(30);

        try
        {
            // 2. 激活窗口（此时窗口不可见）
            SetForegroundWindow(targetHwnd);
            Thread.Sleep(50);

            // 3. 点击（使用 SendInput 更可靠）
            int absoluteX = GetWindowX(targetHwnd) + relativeX;
            int absoluteY = GetWindowY(targetHwnd) + relativeY;

            // 保存并移动鼠标
            GetCursorPos(out POINT originalPos);
            SetCursorPos(absoluteX, absoluteY);
            Thread.Sleep(10);

            // 点击
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(30);

            // 恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 4. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != targetHwnd)
            {
                SetForegroundWindow(prevForeground);
                Thread.Sleep(30);
            }
        }
        finally
        {
            // 5. 恢复窗口可见
            MakeWindowTransparent(targetHwnd, 255);
        }
    }

    private static void MakeWindowTransparent(IntPtr hwnd, byte alpha)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) == 0)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        }
        SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
    }
}
```

## 七、注意事项

1. **管理员权限**：某些窗口操作可能需要以管理员身份运行
2. **UAC 窗口**：无法操作 UAC 提权窗口
3. **全屏游戏**：全屏独占模式的游戏可能无法使用这些方法
4. **反作弊**：游戏的反作弊系统可能会检测并阻止这类操作
5. **DPI 缩放**：高 DPI 屏幕需要处理坐标缩放问题
6. **多显示器**：需要考虑多显示器的坐标计算

## 八、项目结构建议

```
BackgroundClicker/
├── BackgroundClicker.sln
├── BackgroundClicker/
│   ├── Program.cs              # 入口
│   ├── MainForm.cs             # 主界面
│   ├── Core/
│   │   ├── WindowManager.cs    # 窗口管理
│   │   ├── MouseController.cs  # 鼠标控制
│   │   ├── CoordinateManager.cs# 坐标管理
│   │   └── NativeMethods.cs    # Windows API 声明
│   ├── Models/
│   │   ├── WindowInfo.cs       # 窗口信息模型
│   │   └── Coordinate.cs       # 坐标模型
│   └── Config/
│       └── Settings.cs         # 配置管理
└── README.md
```

## 九、参考资料

- [Windows API 文档](https://docs.microsoft.com/en-us/windows/win32/api/)
- [PostMessage vs SendMessage](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew)
- [SetLayeredWindowAttributes](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes)
- [mouse_event](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mouse_event)
