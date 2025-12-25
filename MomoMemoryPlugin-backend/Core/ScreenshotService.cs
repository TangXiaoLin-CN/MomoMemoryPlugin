using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MomoBackend.Core;

/// <summary>
/// 截图服务 - 支持后台窗口截图
/// </summary>
public class ScreenshotService
{
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int SW_RESTORE = 9;
    private const int SW_MINIMIZE = 6;
    private const int SW_SHOWNOACTIVATE = 4;
    private const uint PW_CLIENTONLY = 1;
    private const uint PW_RENDERFULLCONTENT = 2;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int LWA_ALPHA = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    /// <summary>
    /// 截取窗口客户区图像（支持后台窗口）
    /// </summary>
    public Bitmap? CaptureWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        bool wasMinimized = IsIconic(hwnd);
        bool wasHidden = !IsWindowVisible(hwnd);
        int originalExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool wasLayered = (originalExStyle & WS_EX_LAYERED) != 0;

        try
        {
            // 如果窗口最小化或隐藏，需要临时恢复（设为透明）
            if (wasMinimized || wasHidden)
            {
                // 先设置透明
                if (!wasLayered)
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED);
                }
                SetLayeredWindowAttributes(hwnd, 0, 1, LWA_ALPHA);

                if (wasHidden)
                    ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                if (wasMinimized)
                    ShowWindow(hwnd, SW_RESTORE);

                Thread.Sleep(50); // 等待窗口恢复
            }

            // 获取窗口尺寸
            if (!GetWindowRect(hwnd, out RECT windowRect))
                return null;

            int width = windowRect.Right - windowRect.Left;
            int height = windowRect.Bottom - windowRect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // 创建位图
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                try
                {
                    // 使用 PrintWindow 捕获窗口（即使被遮挡也能捕获）
                    // PW_RENDERFULLCONTENT 用于捕获 DirectX/OpenGL 内容
                    if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
                    {
                        // 如果失败，尝试普通模式
                        PrintWindow(hwnd, hdc, 0);
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            return bitmap;
        }
        finally
        {
            // 恢复窗口状态
            if (wasMinimized || wasHidden)
            {
                if (wasMinimized)
                    ShowWindow(hwnd, SW_MINIMIZE);
                if (wasHidden)
                    ShowWindow(hwnd, SW_SHOWNOACTIVATE);

                // 恢复透明度
                SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                if (!wasLayered)
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, originalExStyle);
                }
            }
        }
    }

    /// <summary>
    /// 截取窗口指定区域（客户区坐标）
    /// </summary>
    public Bitmap? CaptureRegion(IntPtr hwnd, Rectangle region)
    {
        var fullBitmap = CaptureWindow(hwnd);
        if (fullBitmap == null)
            return null;

        try
        {
            // 获取窗口和客户区信息以计算标题栏偏移
            if (!GetWindowRect(hwnd, out RECT windowRect))
                return null;

            var clientOrigin = new POINT { X = 0, Y = 0 };
            ClientToScreen(hwnd, ref clientOrigin);

            // 计算客户区相对于窗口的偏移（标题栏高度等）
            int offsetX = clientOrigin.X - windowRect.Left;
            int offsetY = clientOrigin.Y - windowRect.Top;

            // 调整区域坐标
            var adjustedRegion = new Rectangle(
                region.X + offsetX,
                region.Y + offsetY,
                region.Width,
                region.Height
            );

            // 确保区域在图像范围内
            adjustedRegion.Intersect(new Rectangle(0, 0, fullBitmap.Width, fullBitmap.Height));

            if (adjustedRegion.Width <= 0 || adjustedRegion.Height <= 0)
            {
                fullBitmap.Dispose();
                return null;
            }

            // 裁剪区域
            var croppedBitmap = fullBitmap.Clone(adjustedRegion, fullBitmap.PixelFormat);
            fullBitmap.Dispose();

            return croppedBitmap;
        }
        catch
        {
            fullBitmap.Dispose();
            return null;
        }
    }

    /// <summary>
    /// 保存截图到文件（用于调试）
    /// </summary>
    public bool SaveScreenshot(Bitmap bitmap, string filePath)
    {
        try
        {
            bitmap.Save(filePath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
