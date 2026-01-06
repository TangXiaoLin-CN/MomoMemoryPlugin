using System.Runtime.InteropServices;
using MomoBackend.Core;
using MomoBackend.Views;

namespace MomoBackend;

static class Program
{
    // 用于单实例检测的 Mutex 名称
    private const string MutexName = "MomoBackend_ConfigWindow_SingleInstance";

    // Windows API for activating existing window
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    private const int SW_RESTORE = 9;
    private const string ConfigWindowTitle = "Momo 配置管理";

    [STAThread]
    static void Main(string[] args)
    {
        // 预热 PaddleOCR 引擎（在后台线程初始化）
        PaddleOcrService.Warmup();

        // 检查是否以无界面模式运行（供插件调用）
        bool headless = args.Contains("--headless") || args.Contains("-h");

        if (headless)
        {
            // 无界面模式：只启动 HTTP API 服务（使用 WinForms 保持消息循环）
            // Headless 模式不需要单实例检测，由插件管理
            ApplicationConfiguration.Initialize();
            RunHeadless();
        }
        else
        {
            // GUI 模式：检查是否已有实例运行
            using var mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // 已有实例运行，尝试激活现有窗口
                ActivateExistingWindow();
                return;
            }

            // 解析命令行参数获取目标窗口信息
            var initialWindowInfo = ParseWindowArgs(args);

            // 正常模式：显示 WPF 主窗口
            var app = new System.Windows.Application();
            app.Run(new MainConfigWindow(initialWindowInfo));
        }
    }

    /// <summary>
    /// 解析命令行参数获取目标窗口信息
    /// </summary>
    private static InitialWindowInfo? ParseWindowArgs(string[] args)
    {
        long hwnd = 0;
        string? title = null;
        string? processName = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--hwnd" when i + 1 < args.Length:
                    long.TryParse(args[++i], out hwnd);
                    break;
                case "--title" when i + 1 < args.Length:
                    title = args[++i];
                    break;
                case "--process" when i + 1 < args.Length:
                    processName = args[++i];
                    break;
            }
        }

        // 只要有任何有效信息就返回
        if (hwnd != 0 || !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(processName))
        {
            return new InitialWindowInfo
            {
                Hwnd = hwnd,
                Title = title ?? "",
                ProcessName = processName ?? ""
            };
        }

        return null;
    }

    /// <summary>
    /// 激活已存在的配置窗口
    /// </summary>
    private static void ActivateExistingWindow()
    {
        var hwnd = FindWindow(null, ConfigWindowTitle);
        if (hwnd != IntPtr.Zero)
        {
            // 如果窗口最小化了，先恢复
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }
            // 激活窗口
            SetForegroundWindow(hwnd);
        }
    }

    /// <summary>
    /// 无界面模式运行
    /// </summary>
    static void RunHeadless()
    {
        var httpService = new HttpApiService(5678);

        httpService.OnLog += (msg) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
        };

        try
        {
            httpService.Start();
            Console.WriteLine("Momo Backend running in headless mode...");
            Console.WriteLine("Press Ctrl+C to exit.");

            // 保持运行直到收到退出信号
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                exitEvent.Set();
            };

            // 使用 Application.Run 保持消息循环（某些 Windows API 需要）
            Application.Run(new HiddenForm(exitEvent));
        }
        finally
        {
            httpService.Dispose();
        }
    }
}

/// <summary>
/// 初始窗口信息（从命令行传递）
/// </summary>
public class InitialWindowInfo
{
    public long Hwnd { get; set; }
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
}

/// <summary>
/// 隐藏窗口 - 用于保持消息循环
/// </summary>
class HiddenForm : Form
{
    private readonly ManualResetEvent _exitEvent;

    public HiddenForm(ManualResetEvent exitEvent)
    {
        _exitEvent = exitEvent;
        this.ShowInTaskbar = false;
        this.WindowState = FormWindowState.Minimized;
        this.FormBorderStyle = FormBorderStyle.None;
        this.Opacity = 0;
        this.Size = new Size(0, 0);

        // 定期检查退出信号
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (s, e) =>
        {
            if (_exitEvent.WaitOne(0))
            {
                Application.Exit();
            }
        };
        timer.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        this.Visible = false;
    }
}
