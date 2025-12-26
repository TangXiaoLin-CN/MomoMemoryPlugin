using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using MomoBackend.Models;
using static MomoBackend.Core.NativeMethods;

namespace MomoBackend.Core;

public class MouseController
{
    private FastBackgroundSettings _fastBgSettings = new();

    /// <summary>
    /// 日志事件 - 用于输出调试信息
    /// </summary>
    public event Action<string>? OnLog;

    private void Log(string message)
    {
        OnLog?.Invoke(message);
    }

    /// <summary>
    /// 设置 fast_background 模式的参数
    /// </summary>
    public void SetFastBackgroundSettings(FastBackgroundSettings settings)
    {
        _fastBgSettings = settings ?? new FastBackgroundSettings();
    }

    /// <summary>
    /// 执行点击
    /// </summary>
    public ClickResult Click(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, string mode, string button)
    {
        try
        {
            bool rightClick = button == "right";

            switch (mode.ToLower())
            {
                case "stealth":
                    return StealthClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "quick_switch":
                    return QuickSwitchClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "transparent":
                    return TransparentClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "post_message":
                    return PostMessageClick(hwnd, relativeX, relativeY, rightClick);

                case "send_message":
                    return SendMessageClick(hwnd, relativeX, relativeY, rightClick);

                case "foreground":
                    return ForegroundClick(windowX + relativeX, windowY + relativeY, rightClick);

                case "send_input":
                    return SendInputClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "block_input":
                    return BlockInputClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "offscreen":
                    return OffscreenClick(hwnd, relativeX, relativeY, rightClick);

                case "ui_automation":
                    return UIAutomationClick(windowX + relativeX, windowY + relativeY);

                case "ultra_fast":
                    return UltraFastClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "child_window":
                    return ChildWindowClick(hwnd, relativeX, relativeY, rightClick);

                case "direct_message":
                    return DirectMessageClick(hwnd, relativeX, relativeY, rightClick);

                case "child_raw":
                    return ChildRawClick(hwnd, relativeX, relativeY, rightClick);

                case "debug_child":
                    return DebugChildWindow(hwnd, relativeX, relativeY, windowX, windowY);

                case "minimize_restore":
                    return MinimizeRestoreClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "activate_message":
                    return ActivateMessageClick(hwnd, relativeX, relativeY, rightClick);

                case "activate_sendinput":
                    return ActivateSendInputClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "hidden_cursor":
                    return HiddenCursorClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "hook_cursor":
                    return HookCursorClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                case "fast_background":
                    return FastBackgroundClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);

                default:
                    return StealthClick(hwnd, relativeX, relativeY, windowX, windowY, rightClick);
            }
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 隐身点击：透明化 + 激活 + 点击 + 恢复
    /// </summary>
    private ClickResult StealthClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        // 保存原始窗口样式
        int originalExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool wasLayered = (originalExStyle & WS_EX_LAYERED) != 0;

        try
        {
            // 1. 设置透明样式并完全透明
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED);
            }
            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
            Thread.Sleep(30);

            // 2. 激活窗口（此时窗口不可见）
            AttachAndActivate(hwnd);
            Thread.Sleep(50);

            // 3. 移动鼠标并点击
            SetCursorPos(absoluteX, absoluteY);
            Thread.Sleep(10);

            if (rightClick)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }
            Thread.Sleep(30);

            // 4. 恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 5. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
                Thread.Sleep(30);
            }

            return new ClickResult { Success = true, Message = "Stealth click completed" };
        }
        finally
        {
            // 6. 恢复窗口透明度
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);

            // 恢复原始样式
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle);
            }
        }
    }

    /// <summary>
    /// 快速切换点击：激活 + 点击 + 切回
    /// </summary>
    private ClickResult QuickSwitchClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        try
        {
            // 1. 激活目标窗口
            AttachAndActivate(hwnd);
            Thread.Sleep(30);

            // 2. 移动鼠标并点击
            SetCursorPos(absoluteX, absoluteY);
            Thread.Sleep(10);

            if (rightClick)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }
            Thread.Sleep(30);

            // 3. 恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 4. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }

            return new ClickResult { Success = true, Message = "Quick switch click completed" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 透明点击：使用 PostMessage（窗口需要响应消息）
    /// </summary>
    private ClickResult TransparentClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();

        // 保存原始窗口样式
        int originalExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool wasLayered = (originalExStyle & WS_EX_LAYERED) != 0;

        try
        {
            // 1. 设置透明
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED);
            }
            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
            Thread.Sleep(30);

            // 2. 激活窗口
            AttachAndActivate(hwnd);
            Thread.Sleep(50);

            // 3. 使用 PostMessage 点击
            IntPtr lParam = MakeLParam(relativeX, relativeY);

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
            Thread.Sleep(30);

            // 4. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
                Thread.Sleep(30);
            }

            return new ClickResult { Success = true, Message = "Transparent click completed" };
        }
        finally
        {
            // 恢复透明度
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle);
            }
        }
    }

    /// <summary>
    /// PostMessage 点击（纯后台，很多程序不响应）
    /// </summary>
    private ClickResult PostMessageClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        IntPtr lParam = MakeLParam(relativeX, relativeY);

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

        return new ClickResult { Success = true, Message = "PostMessage click sent" };
    }

    /// <summary>
    /// SendMessage 点击（同步后台，很多程序不响应）
    /// </summary>
    private ClickResult SendMessageClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        IntPtr lParam = MakeLParam(relativeX, relativeY);

        SendMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        Thread.Sleep(10);

        if (rightClick)
        {
            SendMessage(hwnd, WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
            Thread.Sleep(30);
            SendMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        }
        else
        {
            SendMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            Thread.Sleep(30);
            SendMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        return new ClickResult { Success = true, Message = "SendMessage click sent" };
    }

    /// <summary>
    /// 前台点击（普通点击）
    /// </summary>
    private ClickResult ForegroundClick(int absoluteX, int absoluteY, bool rightClick)
    {
        SetCursorPos(absoluteX, absoluteY);
        Thread.Sleep(10);

        if (rightClick)
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
        }
        else
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        }

        return new ClickResult { Success = true, Message = "Foreground click completed" };
    }

    /// <summary>
    /// SendInput 点击：使用更现代的 SendInput API
    /// </summary>
    private ClickResult SendInputClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        // 保存原始窗口样式
        int originalExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool wasLayered = (originalExStyle & WS_EX_LAYERED) != 0;

        try
        {
            // 1. 设置透明
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED);
            }
            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
            Thread.Sleep(30);

            // 2. 激活窗口
            AttachAndActivate(hwnd);
            Thread.Sleep(50);

            // 3. 使用 SendInput 执行点击
            NativeMethods.SendInputClick(absoluteX, absoluteY, rightClick);
            Thread.Sleep(30);

            // 4. 恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 5. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
                Thread.Sleep(30);
            }

            return new ClickResult { Success = true, Message = "SendInput click completed" };
        }
        finally
        {
            // 恢复透明度
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle);
            }
        }
    }

    /// <summary>
    /// BlockInput 点击：临时阻止用户输入，防止干扰
    /// 注意：需要管理员权限才能使用 BlockInput
    /// </summary>
    private ClickResult BlockInputClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        // 保存原始窗口样式
        int originalExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool wasLayered = (originalExStyle & WS_EX_LAYERED) != 0;

        bool inputBlocked = false;

        try
        {
            // 1. 设置透明
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED);
            }
            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
            Thread.Sleep(30);

            // 2. 阻止用户输入（需要管理员权限）
            inputBlocked = BlockInput(true);

            // 3. 激活窗口
            AttachAndActivate(hwnd);
            Thread.Sleep(30);

            // 4. 使用 SendInput 执行点击
            NativeMethods.SendInputClick(absoluteX, absoluteY, rightClick);
            Thread.Sleep(30);

            // 5. 恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 6. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
                Thread.Sleep(20);
            }

            string message = inputBlocked
                ? "BlockInput click completed (input was blocked)"
                : "BlockInput click completed (blocking failed - needs admin rights)";

            return new ClickResult { Success = true, Message = message };
        }
        finally
        {
            // 恢复用户输入
            if (inputBlocked)
            {
                BlockInput(false);
            }

            // 恢复透明度
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle);
            }
        }
    }

    /// <summary>
    /// 离屏点击：将窗口移到屏幕外点击，然后移回
    /// </summary>
    private ClickResult OffscreenClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        // 获取原始窗口位置
        if (!GetWindowRect(hwnd, out RECT originalRect))
        {
            return new ClickResult { Success = false, Message = "Failed to get window rect" };
        }

        int originalWidth = originalRect.Right - originalRect.Left;
        int originalHeight = originalRect.Bottom - originalRect.Top;

        // 计算离屏位置（移到屏幕最右边外面）
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int offscreenX = screenWidth + 100;  // 屏幕右边外 100 像素

        try
        {
            // 1. 移动窗口到离屏位置
            MoveWindow(hwnd, offscreenX, 0, originalWidth, originalHeight, false);
            Thread.Sleep(30);

            // 2. 激活窗口
            AttachAndActivate(hwnd);
            Thread.Sleep(50);

            // 3. 计算新的绝对坐标（离屏位置）
            int absoluteX = offscreenX + relativeX;
            int absoluteY = relativeY;

            // 4. 使用 SendInput 点击（即使在屏幕外也能工作）
            NativeMethods.SendInputClick(absoluteX, absoluteY, rightClick);
            Thread.Sleep(30);

            // 5. 恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 6. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
                Thread.Sleep(30);
            }

            // 7. 移动窗口回原位
            MoveWindow(hwnd, originalRect.Left, originalRect.Top, originalWidth, originalHeight, true);

            return new ClickResult { Success = true, Message = "Offscreen click completed" };
        }
        catch (Exception ex)
        {
            // 确保窗口回到原位
            MoveWindow(hwnd, originalRect.Left, originalRect.Top, originalWidth, originalHeight, true);
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// UI Automation 点击：使用 Windows 辅助功能 API，完全不需要真实鼠标
    /// 这是最理想的后台点击方式，但需要目标应用支持 UI Automation
    /// </summary>
    private ClickResult UIAutomationClick(int absoluteX, int absoluteY)
    {
        if (UIAutomationHelper.TryClickElementAtPoint(absoluteX, absoluteY, out string message))
        {
            return new ClickResult { Success = true, Message = message };
        }
        return new ClickResult { Success = false, Message = message };
    }

    /// <summary>
    /// 超快速切换：优化的快速切换，最小化延迟
    /// 使用异步操作和最小延迟，尽量减少用户感知
    /// </summary>
    private ClickResult UltraFastClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        try
        {
            // 附加线程（提前做，减少激活延迟）
            uint targetThreadId = GetWindowThreadProcessId(hwnd, out _);
            uint currentThreadId = GetCurrentThreadId();
            bool attached = false;

            if (targetThreadId != currentThreadId)
            {
                attached = AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            try
            {
                // 快速激活（不使用 ShowWindow，减少闪烁）
                SetForegroundWindow(hwnd);
                BringWindowToTop(hwnd);

                // 立即移动并点击（最小延迟）
                SetCursorPos(absoluteX, absoluteY);

                // 使用 SendInput（更快）
                NativeMethods.SendInputClick(absoluteX, absoluteY, rightClick);

                // 立即恢复
                SetCursorPos(originalPos.X, originalPos.Y);

                if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
                {
                    SetForegroundWindow(prevForeground);
                }
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }
            }

            return new ClickResult { Success = true, Message = "Ultra fast click completed" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 子窗口点击：查找指定坐标处的子窗口，直接向子窗口发送消息
    /// 某些应用的子控件可能响应 PostMessage
    /// </summary>
    private ClickResult ChildWindowClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        // 获取客户区在屏幕上的位置（不包含标题栏和边框）
        var clientOrigin = new POINT { X = 0, Y = 0 };
        ClientToScreen(hwnd, ref clientOrigin);

        // 计算目标点的屏幕坐标（基于客户区）
        var screenPoint = new POINT { X = clientOrigin.X + relativeX, Y = clientOrigin.Y + relativeY };

        // 从屏幕坐标找到子窗口
        IntPtr childHwnd = WindowFromPoint(screenPoint);

        if (childHwnd == IntPtr.Zero)
        {
            return new ClickResult { Success = false, Message = "No window at point" };
        }

        // 获取子窗口的类名用于调试
        var className = new StringBuilder(256);
        GetClassName(childHwnd, className, 256);

        // 转换为子窗口的客户区坐标
        var clientPoint = screenPoint;
        ScreenToClient(childHwnd, ref clientPoint);

        IntPtr lParam = MakeLParam(clientPoint.X, clientPoint.Y);

        // 发送鼠标消息
        PostMessage(childHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        Thread.Sleep(5);

        if (rightClick)
        {
            PostMessage(childHwnd, WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
            Thread.Sleep(20);
            PostMessage(childHwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        }
        else
        {
            PostMessage(childHwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            Thread.Sleep(20);
            PostMessage(childHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        return new ClickResult
        {
            Success = true,
            Message = $"Sent to child window: {className} at client({clientPoint.X}, {clientPoint.Y})"
        };
    }

    /// <summary>
    /// 直接消息点击：直接向主窗口发送消息，使用客户区坐标
    /// 不查找子窗口，坐标更准确
    /// </summary>
    private ClickResult DirectMessageClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        // 直接使用客户区坐标作为 lParam
        IntPtr lParam = MakeLParam(relativeX, relativeY);

        // 发送鼠标消息到主窗口
        PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        Thread.Sleep(5);

        if (rightClick)
        {
            PostMessage(hwnd, WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
            Thread.Sleep(20);
            PostMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        }
        else
        {
            PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            Thread.Sleep(20);
            PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        return new ClickResult
        {
            Success = true,
            Message = $"Direct message to main window at client({relativeX}, {relativeY})"
        };
    }

    /// <summary>
    /// 子窗口原始坐标点击：先发送鼠标移动消息，再点击
    /// </summary>
    private ClickResult ChildRawClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        // 获取主窗口客户区原点
        var clientOrigin = new POINT { X = 0, Y = 0 };
        ClientToScreen(hwnd, ref clientOrigin);

        // 计算屏幕坐标
        var screenPoint = new POINT { X = clientOrigin.X + relativeX, Y = clientOrigin.Y + relativeY };

        // 找到子窗口
        IntPtr childHwnd = WindowFromPoint(screenPoint);
        if (childHwnd == IntPtr.Zero || childHwnd == hwnd)
        {
            childHwnd = hwnd;
        }

        // 获取子窗口客户区原点
        var childOrigin = new POINT { X = 0, Y = 0 };
        ClientToScreen(childHwnd, ref childOrigin);

        // 计算子窗口相对于主窗口的偏移
        int offsetX = childOrigin.X - clientOrigin.X;
        int offsetY = childOrigin.Y - clientOrigin.Y;

        // 调整坐标：减去子窗口偏移
        int adjustedX = relativeX - offsetX;
        int adjustedY = relativeY - offsetY;

        IntPtr lParam = MakeLParam(adjustedX, adjustedY);

        // 关键：先发送多次 WM_MOUSEMOVE 让应用记录鼠标位置
        for (int i = 0; i < 3; i++)
        {
            PostMessage(childHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        }
        Thread.Sleep(10);  // 给应用一点时间处理

        // 然后发送点击
        if (rightClick)
        {
            PostMessage(childHwnd, WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
            PostMessage(childHwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        }
        else
        {
            PostMessage(childHwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            PostMessage(childHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        var className = new StringBuilder(256);
        GetClassName(childHwnd, className, 256);

        return new ClickResult
        {
            Success = true,
            Message = $"({relativeX},{relativeY}) -> ({adjustedX},{adjustedY}) @ {className}"
        };
    }

    /// <summary>
    /// 调试模式：显示子窗口信息和坐标转换结果（打印到日志）
    /// </summary>
    private ClickResult DebugChildWindow(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY)
    {
        var sb = new StringBuilder();

        // 主窗口信息
        var mainClassName = new StringBuilder(256);
        GetClassName(hwnd, mainClassName, 256);
        sb.Append($"主窗口={mainClassName} | ");

        // 客户区原点
        var clientOrigin = new POINT { X = 0, Y = 0 };
        ClientToScreen(hwnd, ref clientOrigin);
        sb.Append($"客户区原点=({clientOrigin.X},{clientOrigin.Y}) | ");

        // 计算屏幕坐标
        var screenPoint = new POINT { X = clientOrigin.X + relativeX, Y = clientOrigin.Y + relativeY };
        sb.Append($"屏幕坐标=({screenPoint.X},{screenPoint.Y}) | ");

        // 找到的子窗口
        IntPtr childHwnd = WindowFromPoint(screenPoint);
        if (childHwnd != IntPtr.Zero)
        {
            var childClassName = new StringBuilder(256);
            GetClassName(childHwnd, childClassName, 256);
            sb.Append($"子窗口={childClassName} | ");
            sb.Append($"同一窗口={childHwnd == hwnd} | ");

            // 子窗口客户区原点
            var childOrigin = new POINT { X = 0, Y = 0 };
            ClientToScreen(childHwnd, ref childOrigin);
            sb.Append($"子窗口原点=({childOrigin.X},{childOrigin.Y}) | ");

            // 偏移
            int offsetX = childOrigin.X - clientOrigin.X;
            int offsetY = childOrigin.Y - clientOrigin.Y;
            sb.Append($"偏移=({offsetX},{offsetY})");
        }

        return new ClickResult
        {
            Success = true,
            Message = sb.ToString()
        };
    }

    /// <summary>
    /// 最小化恢复点击：先最小化窗口，激活点击，再最小化回去
    /// 窗口最小化状态下用户不会看到切换
    /// </summary>
    private ClickResult MinimizeRestoreClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);

        // 检查窗口当前状态
        bool wasMinimized = IsIconic(hwnd);

        try
        {
            // 1. 如果窗口没有最小化，先最小化它（这样恢复时用户可能察觉更小）
            // 但这里我们的策略是：快速恢复、点击、再最小化

            if (wasMinimized)
            {
                // 窗口已经最小化，直接恢复-点击-最小化
                ShowWindow(hwnd, SW_RESTORE);
                Thread.Sleep(50);
            }

            // 获取窗口当前位置
            if (!GetWindowRect(hwnd, out RECT rect))
            {
                return new ClickResult { Success = false, Message = "Failed to get window rect" };
            }

            int absoluteX = rect.Left + relativeX;
            int absoluteY = rect.Top + relativeY;

            // 激活并点击
            AttachAndActivate(hwnd);
            Thread.Sleep(30);

            SetCursorPos(absoluteX, absoluteY);
            NativeMethods.SendInputClick(absoluteX, absoluteY, rightClick);

            // 恢复鼠标
            SetCursorPos(originalPos.X, originalPos.Y);

            // 恢复前台
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }

            // 如果之前是最小化的，重新最小化
            if (wasMinimized)
            {
                Thread.Sleep(30);
                ShowWindow(hwnd, SW_HIDE);  // 使用 HIDE 而不是 MINIMIZE，更隐蔽
            }

            return new ClickResult { Success = true, Message = "Minimize-restore click completed" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 激活+消息点击：类似 AutoHotkey ControlClick
    /// 激活窗口但不移动鼠标，通过消息发送点击
    /// </summary>
    private ClickResult ActivateMessageClick(IntPtr hwnd, int relativeX, int relativeY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();

        try
        {
            // 1. 激活窗口（不移动鼠标）
            AttachAndActivate(hwnd);
            Thread.Sleep(30);

            // 2. 发送鼠标消息到窗口（不移动实际鼠标）
            IntPtr lParam = MakeLParam(relativeX, relativeY);

            // 发送鼠标移动消息（让窗口知道鼠标"在"那个位置）
            PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
            Thread.Sleep(10);

            // 发送点击消息
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

            Thread.Sleep(30);

            // 3. 恢复之前的前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }

            return new ClickResult { Success = true, Message = "Activate + Message click completed" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 激活+SendInput点击：激活窗口，使用SendInput发送点击但不移动鼠标光标
    /// 这是最接近 AutoHotkey 行为的实现
    /// </summary>
    private ClickResult ActivateSendInputClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        IntPtr prevForeground = GetForegroundWindow();

        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        try
        {
            // 1. 激活窗口
            AttachAndActivate(hwnd);
            Thread.Sleep(30);

            // 2. 使用 SendInput 发送绝对坐标点击（不移动鼠标光标）
            SendInputClickWithoutMove(absoluteX, absoluteY, rightClick);

            Thread.Sleep(30);

            // 3. 恢复之前的前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }

            return new ClickResult { Success = true, Message = "Activate + SendInput click completed (no mouse move)" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 隐藏光标点击：隐藏鼠标光标 + 快速点击 + 恢复
    /// 用户看不到光标移动，坐标使用客户区坐标
    /// </summary>
    private ClickResult HiddenCursorClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        GetCursorPos(out POINT originalPos);

        // 使用传入的客户区原点计算绝对坐标
        int absoluteX = windowX + relativeX;
        int absoluteY = windowY + relativeY;

        try
        {
            // 1. 隐藏鼠标光标
            ShowCursor(false);

            // 2. 移动鼠标到目标位置
            SetCursorPos(absoluteX, absoluteY);

            // 3. 点击
            if (rightClick)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }

            // 4. 立即返回原位置
            SetCursorPos(originalPos.X, originalPos.Y);

            return new ClickResult
            {
                Success = true,
                Message = $"隐藏点击 ({relativeX},{relativeY}) 屏幕({absoluteX},{absoluteY})"
            };
        }
        finally
        {
            // 5. 恢复鼠标光标显示
            ShowCursor(true);
        }
    }

    /// <summary>
    /// 使用 SendInput 在指定坐标点击，但尝试不移动鼠标光标
    /// </summary>
    private void SendInputClickWithoutMove(int absoluteX, int absoluteY, bool rightClick)
    {
        // 获取屏幕尺寸
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        // 转换为 0-65535 的归一化坐标
        int normalizedX = (absoluteX * 65535) / screenWidth;
        int normalizedY = (absoluteY * 65535) / screenHeight;

        // 保存当前鼠标位置
        GetCursorPos(out POINT originalPos);
        int origNormX = (originalPos.X * 65535) / screenWidth;
        int origNormY = (originalPos.Y * 65535) / screenHeight;

        var inputs = new INPUT[5];

        // 1. 移动到目标位置
        inputs[0].type = INPUT_MOUSE;
        inputs[0].U.mi.dx = normalizedX;
        inputs[0].U.mi.dy = normalizedY;
        inputs[0].U.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;

        // 2. 按下
        inputs[1].type = INPUT_MOUSE;
        inputs[1].U.mi.dwFlags = rightClick ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;

        // 3. 释放
        inputs[2].type = INPUT_MOUSE;
        inputs[2].U.mi.dwFlags = rightClick ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

        // 4. 移回原位置
        inputs[3].type = INPUT_MOUSE;
        inputs[3].U.mi.dx = origNormX;
        inputs[3].U.mi.dy = origNormY;
        inputs[3].U.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;

        // 一次性发送所有输入（原子操作，尽量减少可见的鼠标移动）
        SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <summary>
    /// 附加线程并激活窗口
    /// </summary>
    private void AttachAndActivate(IntPtr hwnd)
    {
        uint targetThreadId = GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = GetCurrentThreadId();

        bool attached = false;
        if (targetThreadId != currentThreadId)
        {
            attached = AttachThreadInput(currentThreadId, targetThreadId, true);
        }

        try
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    /// <summary>
    /// Hook 点击：快速隐形点击
    ///
    /// 方案：窗口平时可最小化，点击时快速恢复→点击→最小化
    /// 整个过程在毫秒级完成，用户感知很小。
    /// </summary>
    private ClickResult HookCursorClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        // 检查 Hook DLL
        var dllCheck = CursorHook.CheckHookDll();
        if (!dllCheck.Exists)
        {
            return new ClickResult
            {
                Success = false,
                Message = dllCheck.Message + "\n\n" + CursorHook.GetImplementationGuide()
            };
        }

        // 获取目标进程 ID
        GetWindowThreadProcessId(hwnd, out uint processId);

        // 保存当前状态
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);
        bool wasMinimized = IsIconic(hwnd);
        bool wasHidden = !IsWindowVisible(hwnd);

        using var hook = new CursorHook();

        try
        {
            // 附加到进程并注入 Hook
            var attachResult = hook.Attach((int)processId);
            if (!attachResult.Success)
                return new ClickResult { Success = false, Message = attachResult.Message };

            var injectResult = hook.InjectHook();
            if (!injectResult.Success)
                return new ClickResult { Success = false, Message = injectResult.Message };

            // === 快速点击流程 ===

            // 1. 如果窗口被隐藏或最小化，先恢复它
            if (wasHidden)
            {
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            }
            if (wasMinimized)
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            // 等待窗口恢复并获取新的窗口位置
            Thread.Sleep(30);

            // 重新获取窗口位置（因为恢复后位置可能变了）
            if (!GetWindowRect(hwnd, out RECT rect))
            {
                return new ClickResult { Success = false, Message = "无法获取窗口位置" };
            }

            // 计算新的绝对坐标
            int absoluteX = rect.Left + relativeX;
            int absoluteY = rect.Top + relativeY;

            // 2. 启用 Hook
            hook.SetFakePositionAndEnable(absoluteX, absoluteY);

            // 3. 隐藏光标
            ShowCursor(false);

            // 4. 激活窗口并点击
            AttachAndActivate(hwnd);
            SetCursorPos(absoluteX, absoluteY);

            if (rightClick)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }

            // 5. 立即恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 6. 显示光标
            ShowCursor(true);

            // 7. 恢复前台窗口
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }

            // 8. 如果之前是最小化/隐藏的，恢复原状态
            Thread.Sleep(20);  // 给点击一点时间完成
            if (wasMinimized)
            {
                ShowWindow(hwnd, SW_MINIMIZE);
            }
            if (wasHidden)
            {
                ShowWindow(hwnd, SW_HIDE);
            }

            // 禁用 Hook
            hook.DisableHook();

            string stateMsg = wasMinimized ? " (窗口已重新最小化)" : (wasHidden ? " (窗口已重新隐藏)" : "");
            return new ClickResult
            {
                Success = true,
                Message = $"快速点击: ({relativeX},{relativeY}){stateMsg}"
            };
        }
        catch (Exception ex)
        {
            ShowCursor(true);
            SetCursorPos(originalPos.X, originalPos.Y);
            hook.DisableHook();
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
                SetForegroundWindow(prevForeground);
            return new ClickResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 获取当前鼠标位置
    /// </summary>
    public CursorPosition GetCursorPosition()
    {
        GetCursorPos(out POINT pt);
        return new CursorPosition { X = pt.X, Y = pt.Y };
    }

    /// <summary>
    /// 快速后台点击：无需 Hook，支持最小化窗口
    /// 流程：隐藏光标 → 设置透明 → 恢复窗口 → 快速点击 → 最小化 → 恢复透明度 → 显示光标
    /// </summary>
    private ClickResult FastBackgroundClick(IntPtr hwnd, int relativeX, int relativeY, int windowX, int windowY, bool rightClick)
    {
        var settings = _fastBgSettings;

        // 保存当前状态
        IntPtr prevForeground = GetForegroundWindow();
        GetCursorPos(out POINT originalPos);
        bool wasMinimized = IsIconic(hwnd);
        bool wasHidden = !IsWindowVisible(hwnd);

        // 保存原始窗口样式
        int originalExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool wasLayered = (originalExStyle & WS_EX_LAYERED) != 0;

        // 使用配置的透明度
        byte windowAlpha = settings.WindowAlpha;

        // 保存原始光标（用于系统级隐藏）
        IntPtr originalCursor = IntPtr.Zero;
        IntPtr blankCursor = IntPtr.Zero;
        bool cursorReplaced = false;

        try
        {
            // 1. 【最先】隐藏光标（使用系统级替换）
            if (settings.HideCursor)
            {
                try
                {
                    // 创建一个 32x32 的透明光标
                    byte[] andMask = new byte[128];
                    byte[] xorMask = new byte[128];
                    for (int i = 0; i < 128; i++)
                    {
                        andMask[i] = 0xFF;  // 全透明
                        xorMask[i] = 0x00;  // 不绘制
                    }
                    blankCursor = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, xorMask);

                    if (blankCursor != IntPtr.Zero)
                    {
                        // 替换多种系统光标为透明光标
                        uint[] cursorTypes = { OCR_NORMAL, OCR_IBEAM, OCR_HAND, OCR_APPSTARTING };

                        foreach (var cursorType in cursorTypes)
                        {
                            IntPtr cursorCopy = CopyIcon(blankCursor);
                            if (cursorCopy != IntPtr.Zero && SetSystemCursor(cursorCopy, cursorType))
                            {
                                cursorReplaced = true;
                            }
                        }
                    }
                }
                catch
                {
                    // 光标隐藏失败不影响点击功能
                }
            }

            // 2. 设置窗口为分层窗口并设置透明度（在恢复窗口之前）
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED);
            }
            SetLayeredWindowAttributes(hwnd, 0, windowAlpha, LWA_ALPHA);

            // 3. 恢复窗口（如果最小化或隐藏）- 此时窗口已经透明
            if (wasHidden)
            {
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            }
            if (wasMinimized)
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            Thread.Sleep(settings.DelayAfterRestore);  // 等待窗口恢复

            // 4. 【重要】恢复窗口后重新获取客户区原点，因为最小化时的坐标是无效的
            int actualWindowX = windowX;
            int actualWindowY = windowY;
            if (wasMinimized || wasHidden)
            {
                var clientOrigin = new POINT { X = 0, Y = 0 };
                if (ClientToScreen(hwnd, ref clientOrigin))
                {
                    actualWindowX = clientOrigin.X;
                    actualWindowY = clientOrigin.Y;
                }
            }

            // 5. 计算绝对坐标
            int absoluteX = actualWindowX + relativeX;
            int absoluteY = actualWindowY + relativeY;

            // 6. 激活窗口并点击
            AttachAndActivate(hwnd);
            Thread.Sleep(settings.DelayBeforeClick);  // 等待窗口激活

            SetCursorPos(absoluteX, absoluteY);
            Thread.Sleep(settings.DelayAfterMove);

            if (rightClick)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                Thread.Sleep(10);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                Thread.Sleep(10);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }

            Thread.Sleep(settings.DelayAfterClick);  // 给点击一点时间被处理

            // 7. 立即恢复鼠标位置
            SetCursorPos(originalPos.X, originalPos.Y);

            // 8. 恢复前台窗口
            Thread.Sleep(settings.DelayBeforeRestore);
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }

            // 9. 最小化目标窗口（可选）
            if (settings.MinimizeAfterClick)
            {
                ShowWindow(hwnd, SW_MINIMIZE);
            }

            return new ClickResult
            {
                Success = true,
                Message = $"快速后台点击完成 ({relativeX},{relativeY}) -> ({absoluteX},{absoluteY})"
            };
        }
        catch (Exception ex)
        {
            SetCursorPos(originalPos.X, originalPos.Y);
            if (prevForeground != IntPtr.Zero && prevForeground != hwnd)
            {
                SetForegroundWindow(prevForeground);
            }
            return new ClickResult { Success = false, Message = ex.Message };
        }
        finally
        {
            // 【最后】恢复窗口透明度
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            if (!wasLayered)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle);
            }

            // 【最后】恢复系统光标
            if (settings.HideCursor && cursorReplaced)
            {
                try
                {
                    SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                }
                catch
                {
                    // 恢复失败不影响程序运行
                }
            }

            // 清理创建的光标
            if (blankCursor != IntPtr.Zero)
            {
                DestroyCursor(blankCursor);
            }
            if (originalCursor != IntPtr.Zero)
            {
                DestroyCursor(originalCursor);
            }
        }
    }
}
