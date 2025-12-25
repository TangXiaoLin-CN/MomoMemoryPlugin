using MomoBackend.Core;

namespace MomoBackend;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // 预热 PaddleOCR 引擎（在后台线程初始化）
        PaddleOcrService.Warmup();

        // 检查是否以无界面模式运行（供插件调用）
        bool headless = args.Contains("--headless") || args.Contains("-h");

        if (headless)
        {
            // 无界面模式：只启动 HTTP API 服务
            RunHeadless();
        }
        else
        {
            // 正常模式：显示主窗口
            Application.Run(new MainForm());
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
