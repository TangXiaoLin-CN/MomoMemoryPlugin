using MomoBackend.Core;
using MomoBackend.Models;

namespace MomoBackend;

public class MainForm : Form
{
    private readonly WindowManager _windowManager;
    private readonly MouseController _mouseController;
    private readonly CoordinateManager _coordinateManager;
    private readonly HttpApiService _httpApiService;

    // 控件
    private ComboBox _windowComboBox = null!;
    private Button _refreshWindowsBtn = null!;
    private Label _hwndLabel = null!;
    private NumericUpDown _xInput = null!;
    private NumericUpDown _yInput = null!;
    private ComboBox _modeComboBox = null!;
    private ComboBox _buttonComboBox = null!;
    private Button _clickBtn = null!;
    private Button _captureBtn = null!;
    private Label _mousePositionLabel = null!;
    private TextBox _logTextBox = null!;  // 改为 TextBox
    private System.Windows.Forms.Timer _mouseTimer = null!;

    // 当前选中的窗口
    private WindowInfo? _selectedWindow;

    public MainForm()
    {
        _windowManager = new WindowManager();
        _mouseController = new MouseController();
        _coordinateManager = new CoordinateManager();
        _httpApiService = new HttpApiService(5678);

        InitializeComponent();
        LoadWindows();
        StartMouseTracking();
        StartHttpApi();
    }

    private void StartHttpApi()
    {
        try
        {
            _httpApiService.OnLog += (msg) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => Log($"[API] {msg}"));
                }
                else
                {
                    Log($"[API] {msg}");
                }
            };
            _httpApiService.Start();
        }
        catch (Exception ex)
        {
            Log($"HTTP API 启动失败: {ex.Message}");
        }
    }

    private void InitializeComponent()
    {
        this.Text = "Momo 后台点击测试工具";
        this.Size = new Size(600, 550);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        int y = 20;
        int labelWidth = 80;
        int controlLeft = 100;

        // ========== 窗口选择 ==========
        var windowLabel = new Label { Text = "目标窗口:", Location = new Point(20, y + 3), Width = labelWidth };
        _windowComboBox = new ComboBox
        {
            Location = new Point(controlLeft, y),
            Width = 350,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _windowComboBox.SelectedIndexChanged += WindowComboBox_SelectedIndexChanged;

        _refreshWindowsBtn = new Button
        {
            Text = "刷新",
            Location = new Point(460, y - 1),
            Width = 60
        };
        _refreshWindowsBtn.Click += (s, e) => LoadWindows();

        this.Controls.AddRange(new Control[] { windowLabel, _windowComboBox, _refreshWindowsBtn });
        y += 35;

        // ========== HWND 显示 ==========
        var hwndTextLabel = new Label { Text = "句柄:", Location = new Point(20, y + 3), Width = labelWidth };
        _hwndLabel = new Label
        {
            Text = "未选择",
            Location = new Point(controlLeft, y + 3),
            Width = 200,
            ForeColor = Color.Blue
        };
        this.Controls.AddRange(new Control[] { hwndTextLabel, _hwndLabel });
        y += 35;

        // ========== 坐标输入 ==========
        var xLabel = new Label { Text = "X 坐标:", Location = new Point(20, y + 3), Width = labelWidth };
        _xInput = new NumericUpDown
        {
            Location = new Point(controlLeft, y),
            Width = 100,
            Minimum = -10000,
            Maximum = 10000,
            Value = 0
        };

        var yLabel = new Label { Text = "Y 坐标:", Location = new Point(220, y + 3), Width = 60 };
        _yInput = new NumericUpDown
        {
            Location = new Point(280, y),
            Width = 100,
            Minimum = -10000,
            Maximum = 10000,
            Value = 0
        };

        this.Controls.AddRange(new Control[] { xLabel, _xInput, yLabel, _yInput });
        y += 35;

        // ========== 点击模式 ==========
        var modeLabel = new Label { Text = "点击模式:", Location = new Point(20, y + 3), Width = labelWidth };
        _modeComboBox = new ComboBox
        {
            Location = new Point(controlLeft, y),
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _modeComboBox.Items.AddRange(new object[]
        {
            "fast_background (后台点击) ★推荐",
            "hidden_cursor (隐藏光标点击)",
            "hook_cursor (Hook点击)",
            "debug_child (调试窗口信息)",
            "quick_switch (快速切换)",
            "foreground (前台点击)"
        });
        _modeComboBox.SelectedIndex = 0;

        var buttonLabel = new Label { Text = "按键:", Location = new Point(330, y + 3), Width = 40 };
        _buttonComboBox = new ComboBox
        {
            Location = new Point(370, y),
            Width = 80,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _buttonComboBox.Items.AddRange(new object[] { "left (左键)", "right (右键)" });
        _buttonComboBox.SelectedIndex = 0;

        this.Controls.AddRange(new Control[] { modeLabel, _modeComboBox, buttonLabel, _buttonComboBox });
        y += 40;

        // ========== 操作按钮 ==========
        _captureBtn = new Button
        {
            Text = "捕获鼠标位置 (3秒后)",
            Location = new Point(100, y),
            Width = 150,
            Height = 35
        };
        _captureBtn.Click += CaptureBtn_Click;

        _clickBtn = new Button
        {
            Text = "执行点击",
            Location = new Point(270, y),
            Width = 120,
            Height = 35,
            BackColor = Color.LightGreen
        };
        _clickBtn.Click += ClickBtn_Click;

        var ocrBtn = new Button
        {
            Text = "OCR识别",
            Location = new Point(400, y),
            Width = 80,
            Height = 35,
            BackColor = Color.LightBlue
        };
        ocrBtn.Click += (s, e) =>
        {
            var ocrForm = new OcrForm();
            ocrForm.Show();
        };

        var configBtn = new Button
        {
            Text = "配置",
            Location = new Point(490, y),
            Width = 60,
            Height = 35,
            BackColor = Color.LightGray
        };
        configBtn.Click += (s, e) =>
        {
            var configForm = new ConfigForm();
            configForm.Show();
        };

        this.Controls.AddRange(new Control[] { _captureBtn, _clickBtn, ocrBtn, configBtn });
        y += 50;

        // ========== 鼠标位置实时显示 ==========
        var mousePosTextLabel = new Label
        {
            Text = "当前鼠标位置:",
            Location = new Point(20, y + 3),
            Width = 100
        };
        _mousePositionLabel = new Label
        {
            Text = "X: 0, Y: 0 (相对: X: 0, Y: 0)",
            Location = new Point(120, y + 3),
            Width = 400,
            ForeColor = Color.DarkGreen
        };
        this.Controls.AddRange(new Control[] { mousePosTextLabel, _mousePositionLabel });
        y += 35;

        // ========== 日志 ==========
        var logLabel = new Label { Text = "操作日志 (可复制):", Location = new Point(20, y), Width = 120 };
        this.Controls.Add(logLabel);
        y += 20;

        _logTextBox = new TextBox
        {
            Location = new Point(20, y),
            Width = 540,
            Height = 200,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9)
        };
        this.Controls.Add(_logTextBox);
    }

    private void LoadWindows()
    {
        _windowComboBox.Items.Clear();
        var windows = _windowManager.GetAllWindows();

        foreach (var win in windows.OrderBy(w => w.Title))
        {
            _windowComboBox.Items.Add(new WindowItem(win));
        }

        Log($"已加载 {windows.Count} 个窗口");
    }

    private void WindowComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_windowComboBox.SelectedItem is WindowItem item)
        {
            _selectedWindow = item.Window;
            _hwndLabel.Text = $"{_selectedWindow.Hwnd} ({_selectedWindow.ProcessName})";
            Log($"已选择窗口: {_selectedWindow.Title}");
        }
    }

    private void StartMouseTracking()
    {
        _mouseTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _mouseTimer.Tick += (s, e) => UpdateMousePosition();
        _mouseTimer.Start();
    }

    private void UpdateMousePosition()
    {
        var pos = _mouseController.GetCursorPosition();
        string relativeText = "";

        if (_selectedWindow != null)
        {
            // 使用客户区原点计算相对坐标（不包含标题栏）
            var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
            if (clientOrigin != null)
            {
                int relX = pos.X - clientOrigin.Value.X;
                int relY = pos.Y - clientOrigin.Value.Y;
                relativeText = $" (客户区: X: {relX}, Y: {relY})";
            }
        }

        _mousePositionLabel.Text = $"X: {pos.X}, Y: {pos.Y}{relativeText}";
    }

    private async void CaptureBtn_Click(object? sender, EventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _captureBtn.Enabled = false;
        Log("3秒后捕获鼠标位置，请移动鼠标到目标位置...");

        for (int i = 3; i > 0; i--)
        {
            _captureBtn.Text = $"捕获中... {i}";
            await Task.Delay(1000);
        }

        var pos = _mouseController.GetCursorPosition();
        // 使用客户区原点计算相对坐标（不包含标题栏）
        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);

        if (clientOrigin != null)
        {
            int relX = pos.X - clientOrigin.Value.X;
            int relY = pos.Y - clientOrigin.Value.Y;
            _xInput.Value = relX;
            _yInput.Value = relY;
            Log($"已捕获客户区坐标: X={relX}, Y={relY}");
        }

        _captureBtn.Text = "捕获鼠标位置 (3秒后)";
        _captureBtn.Enabled = true;
    }

    private void ClickBtn_Click(object? sender, EventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 检查窗口是否有效
        if (!_windowManager.IsWindowValid((IntPtr)_selectedWindow.Hwnd))
        {
            MessageBox.Show("目标窗口已失效，请刷新窗口列表", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 使用客户区原点（不包含标题栏和边框）
        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (clientOrigin == null)
        {
            MessageBox.Show("无法获取窗口客户区位置", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 获取参数
        int x = (int)_xInput.Value;
        int y = (int)_yInput.Value;
        string mode = _modeComboBox.SelectedItem?.ToString()?.Split(' ')[0] ?? "stealth";
        string button = _buttonComboBox.SelectedItem?.ToString()?.Split(' ')[0] ?? "left";

        Log($"执行点击: 模式={mode}, 客户区坐标=({x}, {y}), 按键={button}");

        // 执行点击（传递客户区原点）
        var result = _mouseController.Click(
            (IntPtr)_selectedWindow.Hwnd,
            x, y,
            clientOrigin.Value.X, clientOrigin.Value.Y,
            mode, button
        );

        if (result.Success)
        {
            Log($"✓ 点击成功: {result.Message}");
        }
        else
        {
            Log($"✗ 点击失败: {result.Message}");
        }
    }

    private void Log(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        _logTextBox.AppendText($"[{time}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _mouseTimer?.Stop();
        _mouseTimer?.Dispose();
        _httpApiService?.Dispose();
        base.OnFormClosing(e);
    }
}

// 窗口列表项
public class WindowItem
{
    public WindowInfo Window { get; }

    public WindowItem(WindowInfo window)
    {
        Window = window;
    }

    public override string ToString()
    {
        var title = Window.Title.Length > 40 ? Window.Title[..40] + "..." : Window.Title;
        return $"{title} [{Window.ProcessName}]";
    }
}
