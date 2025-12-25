using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MomoBackend.Models;
using static MomoBackend.Core.NativeMethods;

namespace MomoBackend.Core;

public class WindowManager
{
    /// <summary>
    /// 获取所有可见窗口列表（包含更多窗口类型）
    /// </summary>
    public List<WindowInfo> GetAllWindows()
    {
        var windows = new List<WindowInfo>();
        var shellWindow = GetShellWindow();

        EnumWindows((hWnd, lParam) =>
        {
            // 跳过 shell 窗口（桌面）
            if (hWnd == shellWindow)
                return true;

            // 检查窗口是否可见
            if (!IsWindowVisible(hWnd))
                return true;

            // 跳过被最小化到任务栏的窗口（可选保留）
            // if (IsIconic(hWnd))
            //     return true;

            // 获取窗口标题
            var title = GetWindowTitle(hWnd);

            // 跳过无标题窗口，但保留一些特殊情况
            if (string.IsNullOrWhiteSpace(title))
                return true;

            // 跳过工具窗口和不在任务栏显示的窗口
            var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // 如果是工具窗口且没有 AppWindow 样式，可能是辅助窗口
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;

            // 跳过纯工具窗口（除非它明确要求显示在任务栏）
            if (isToolWindow && !isAppWindow)
                return true;

            // 检查是否是 cloaked 窗口（UWP 隐藏窗口）
            if (IsWindowCloaked(hWnd))
                return true;

            var info = GetWindowInfo(hWnd);
            if (info != null)
            {
                windows.Add(info);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// 检查窗口是否被 "cloaked"（UWP 应用隐藏状态）
    /// </summary>
    private bool IsWindowCloaked(IntPtr hWnd)
    {
        int cloaked = 0;
        int hr = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    /// <summary>
    /// 获取窗口信息
    /// </summary>
    public WindowInfo? GetWindowInfo(IntPtr hWnd)
    {
        if (!IsWindow(hWnd))
            return null;

        var title = GetWindowTitle(hWnd);
        GetWindowThreadProcessId(hWnd, out uint processId);

        string processName = "";
        try
        {
            var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch { }

        var rect = GetWindowRect(hWnd);

        return new WindowInfo
        {
            Hwnd = hWnd.ToInt64(),
            Title = title,
            ProcessName = processName,
            ProcessId = (int)processId,
            Rect = rect
        };
    }

    /// <summary>
    /// 获取窗口标题
    /// </summary>
    public string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0)
            return "";

        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// 获取窗口位置和大小
    /// </summary>
    public WindowRect? GetWindowRect(IntPtr hWnd)
    {
        if (!NativeMethods.GetWindowRect(hWnd, out RECT rect))
            return null;

        return new WindowRect
        {
            X = rect.Left,
            Y = rect.Top,
            Width = rect.Right - rect.Left,
            Height = rect.Bottom - rect.Top
        };
    }

    /// <summary>
    /// 获取窗口客户区在屏幕上的原点位置（不包含标题栏和边框）
    /// </summary>
    public (int X, int Y)? GetClientAreaOrigin(IntPtr hWnd)
    {
        var point = new POINT { X = 0, Y = 0 };
        if (ClientToScreen(hWnd, ref point))
        {
            return (point.X, point.Y);
        }
        return null;
    }

    /// <summary>
    /// 检查窗口是否有效
    /// </summary>
    public bool IsWindowValid(IntPtr hWnd)
    {
        return IsWindow(hWnd) && IsWindowVisible(hWnd);
    }

    /// <summary>
    /// 获取当前前台窗口
    /// </summary>
    public IntPtr GetForeground()
    {
        return GetForegroundWindow();
    }

    /// <summary>
    /// 设置前台窗口
    /// </summary>
    public bool SetForeground(IntPtr hWnd)
    {
        return SetForegroundWindow(hWnd);
    }
}
