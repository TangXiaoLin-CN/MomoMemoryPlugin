using MomoBackend.Core;
using MomoBackend.Models;
using static MomoBackend.Core.NativeMethods;

namespace MomoBackend;

/// <summary>
/// 嵌入窗口窗体 - 将目标窗口嵌入到我们的窗口中
/// </summary>
public class EmbedForm : Form
{
    private readonly WindowManager _windowManager;
    private readonly MouseController _mouseController;

    private Panel _embedPanel = null!;
    private TextBox _logTextBox = null!;
    private FlowLayoutPanel _buttonPanel = null!;

    private IntPtr _embeddedHwnd = IntPtr.Zero;
    private RECT _originalRect;
    private int _originalStyle;
    private IntPtr _originalParent;

    // 保存的坐标点
    private List<CoordinatePoint> _savedPoints = new();

    public EmbedForm()
    {
        _windowManager = new WindowManager();
        _mouseController = new MouseController();

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Momo 嵌入窗口模式";
        this.Size = new Size(800, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.Sizable;

        // 主布局
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // 顶部控制栏
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 嵌入区域
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));  // 底部日志和按钮

        // ========== 顶部控制栏 ==========
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(5)
        };

        var embedBtn = new Button { Text = "选择并嵌入窗口", Width = 120, Height = 28 };
        embedBtn.Click += EmbedBtn_Click;

        var releaseBtn = new Button { Text = "释放窗口", Width = 80, Height = 28 };
        releaseBtn.Click += ReleaseBtn_Click;

        var addPointBtn = new Button { Text = "添加坐标点 (3秒)", Width = 110, Height = 28 };
        addPointBtn.Click += AddPointBtn_Click;

        var clearPointsBtn = new Button { Text = "清空坐标", Width = 80, Height = 28 };
        clearPointsBtn.Click += (s, e) => { _savedPoints.Clear(); RefreshButtonPanel(); Log("已清空所有坐标点"); };

        var testBackgroundBtn = new Button { Text = "测试后台点击", Width = 100, Height = 28 };
        testBackgroundBtn.Click += TestBackgroundBtn_Click;

        topPanel.Controls.AddRange(new Control[] { embedBtn, releaseBtn, addPointBtn, clearPointsBtn, testBackgroundBtn });

        // ========== 嵌入区域 ==========
        _embedPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            BorderStyle = BorderStyle.FixedSingle
        };

        // 提示文字
        var hintLabel = new Label
        {
            Text = "点击「选择并嵌入窗口」将目标窗口嵌入此区域",
            ForeColor = Color.Gray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _embedPanel.Controls.Add(hintLabel);

        // ========== 底部区域 ==========
        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        // 日志
        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.LightGreen
        };

        // 快捷按钮面板
        _buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.FromArgb(50, 50, 50),
            Padding = new Padding(5)
        };

        var buttonLabel = new Label
        {
            Text = "快捷点击按钮:",
            ForeColor = Color.White,
            AutoSize = true
        };
        _buttonPanel.Controls.Add(buttonLabel);

        bottomPanel.Controls.Add(_logTextBox, 0, 0);
        bottomPanel.Controls.Add(_buttonPanel, 1, 0);

        mainLayout.Controls.Add(topPanel, 0, 0);
        mainLayout.Controls.Add(_embedPanel, 0, 1);
        mainLayout.Controls.Add(bottomPanel, 0, 2);

        this.Controls.Add(mainLayout);

        // 窗口大小改变时调整嵌入窗口大小
        _embedPanel.SizeChanged += (s, e) => ResizeEmbeddedWindow();
    }

    private void EmbedBtn_Click(object? sender, EventArgs e)
    {
        // 获取窗口列表
        var windows = _windowManager.GetAllWindows();

        // 创建选择对话框
        using var dialog = new Form
        {
            Text = "选择要嵌入的窗口",
            Size = new Size(500, 400),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei", 10)
        };

        foreach (var win in windows.OrderBy(w => w.Title))
        {
            listBox.Items.Add(new WindowItem(win));
        }

        var okBtn = new Button
        {
            Text = "嵌入",
            Dock = DockStyle.Bottom,
            Height = 35,
            DialogResult = DialogResult.OK
        };

        dialog.Controls.Add(listBox);
        dialog.Controls.Add(okBtn);
        dialog.AcceptButton = okBtn;

        if (dialog.ShowDialog() == DialogResult.OK && listBox.SelectedItem is WindowItem item)
        {
            EmbedWindow((IntPtr)item.Window.Hwnd);
        }
    }

    private void EmbedWindow(IntPtr hwnd)
    {
        if (_embeddedHwnd != IntPtr.Zero)
        {
            ReleaseEmbeddedWindow();
        }

        // 保存原始状态
        _embeddedHwnd = hwnd;
        GetWindowRect(hwnd, out _originalRect);
        _originalStyle = GetWindowLong(hwnd, GWL_STYLE);
        _originalParent = GetParent(hwnd);

        // 移除边框样式
        int newStyle = _originalStyle & ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        SetWindowLong(hwnd, GWL_STYLE, newStyle);

        // 设置父窗口
        SetParent(hwnd, _embedPanel.Handle);

        // 调整大小
        ResizeEmbeddedWindow();

        // 清除提示文字
        _embedPanel.Controls.Clear();

        Log($"已嵌入窗口: {_windowManager.GetWindowTitle(hwnd)}");
    }

    private void ResizeEmbeddedWindow()
    {
        if (_embeddedHwnd != IntPtr.Zero && IsWindow(_embeddedHwnd))
        {
            MoveWindow(_embeddedHwnd, 0, 0, _embedPanel.ClientSize.Width, _embedPanel.ClientSize.Height, true);
        }
    }

    private void ReleaseBtn_Click(object? sender, EventArgs e)
    {
        ReleaseEmbeddedWindow();
    }

    private void ReleaseEmbeddedWindow()
    {
        if (_embeddedHwnd == IntPtr.Zero)
            return;

        // 恢复父窗口
        SetParent(_embeddedHwnd, _originalParent);

        // 恢复样式
        SetWindowLong(_embeddedHwnd, GWL_STYLE, _originalStyle);

        // 恢复位置和大小
        int width = _originalRect.Right - _originalRect.Left;
        int height = _originalRect.Bottom - _originalRect.Top;
        MoveWindow(_embeddedHwnd, _originalRect.Left, _originalRect.Top, width, height, true);

        Log($"已释放窗口");

        _embeddedHwnd = IntPtr.Zero;

        // 恢复提示文字
        var hintLabel = new Label
        {
            Text = "点击「选择并嵌入窗口」将目标窗口嵌入此区域",
            ForeColor = Color.Gray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _embedPanel.Controls.Add(hintLabel);
    }

    private async void AddPointBtn_Click(object? sender, EventArgs e)
    {
        if (_embeddedHwnd == IntPtr.Zero)
        {
            MessageBox.Show("请先嵌入窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log("3秒后捕获坐标，请将鼠标移动到目标位置...");

        for (int i = 3; i > 0; i--)
        {
            Log($"倒计时: {i}");
            await Task.Delay(1000);
        }

        // 获取鼠标位置（相对于嵌入面板）
        GetCursorPos(out POINT pt);
        var panelPos = _embedPanel.PointToScreen(Point.Empty);
        int relX = pt.X - panelPos.X;
        int relY = pt.Y - panelPos.Y;

        // 弹出输入框让用户命名
        using var nameDialog = new Form
        {
            Text = "命名坐标点",
            Size = new Size(300, 120),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var textBox = new TextBox
        {
            Location = new Point(20, 20),
            Width = 240,
            Text = $"按钮{_savedPoints.Count + 1}"
        };

        var okBtn = new Button
        {
            Text = "确定",
            Location = new Point(100, 50),
            DialogResult = DialogResult.OK
        };

        nameDialog.Controls.AddRange(new Control[] { textBox, okBtn });
        nameDialog.AcceptButton = okBtn;

        if (nameDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var point = new CoordinatePoint
            {
                Alias = textBox.Text,
                X = relX,
                Y = relY
            };
            _savedPoints.Add(point);
            RefreshButtonPanel();
            Log($"已添加坐标点: {point.Alias} ({point.X}, {point.Y})");
        }
    }

    private void RefreshButtonPanel()
    {
        _buttonPanel.Controls.Clear();

        var label = new Label
        {
            Text = "快捷点击按钮:",
            ForeColor = Color.White,
            AutoSize = true
        };
        _buttonPanel.Controls.Add(label);

        foreach (var point in _savedPoints)
        {
            var btn = new Button
            {
                Text = $"{point.Alias}\n({point.X},{point.Y})",
                Width = 100,
                Height = 40,
                Tag = point,
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btn.Click += PointBtn_Click;
            _buttonPanel.Controls.Add(btn);
        }
    }

    private void PointBtn_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.Tag is CoordinatePoint point)
        {
            ClickAtPoint(point.X, point.Y);
        }
    }

    /// <summary>
    /// 测试后台点击 - 使用 PostMessage 而不移动鼠标
    /// </summary>
    private void TestBackgroundBtn_Click(object? sender, EventArgs e)
    {
        if (_embeddedHwnd == IntPtr.Zero)
        {
            MessageBox.Show("请先嵌入窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_savedPoints.Count == 0)
        {
            MessageBox.Show("请先添加至少一个坐标点", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var point = _savedPoints[0];
        Log($"测试后台点击: {point.Alias} ({point.X}, {point.Y})");

        // 方法1: 直接向嵌入窗口发送 PostMessage
        IntPtr lParam = MakeLParam(point.X, point.Y);

        PostMessage(_embeddedHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        Thread.Sleep(10);
        PostMessage(_embeddedHwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
        Thread.Sleep(30);
        PostMessage(_embeddedHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);

        Log("PostMessage 已发送到嵌入窗口");

        // 方法2: 尝试向 Panel 发送消息（作为父窗口）
        Thread.Sleep(100);
        PostMessage(_embedPanel.Handle, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        Thread.Sleep(10);
        PostMessage(_embedPanel.Handle, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
        Thread.Sleep(30);
        PostMessage(_embedPanel.Handle, WM_LBUTTONUP, IntPtr.Zero, lParam);

        Log("PostMessage 已发送到父Panel");
    }

    private void ClickAtPoint(int relativeX, int relativeY)
    {
        if (_embeddedHwnd == IntPtr.Zero)
        {
            Log("错误: 没有嵌入的窗口");
            return;
        }

        // 获取嵌入面板在屏幕上的位置
        var panelPos = _embedPanel.PointToScreen(Point.Empty);
        int absoluteX = panelPos.X + relativeX;
        int absoluteY = panelPos.Y + relativeY;

        // 保存原始鼠标位置
        GetCursorPos(out POINT originalPos);

        try
        {
            // 隐藏光标
            ShowCursor(false);

            // 移动并点击
            SetCursorPos(absoluteX, absoluteY);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);

            // 返回原位
            SetCursorPos(originalPos.X, originalPos.Y);

            Log($"点击: ({relativeX}, {relativeY}) 屏幕({absoluteX}, {absoluteY})");
        }
        finally
        {
            ShowCursor(true);
        }
    }

    private void Log(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        _logTextBox.AppendText($"[{time}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 释放嵌入的窗口
        if (_embeddedHwnd != IntPtr.Zero)
        {
            ReleaseEmbeddedWindow();
        }
        base.OnFormClosing(e);
    }

    // 窗口样式常量
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;

    // 消息常量
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;

    private static IntPtr MakeLParam(int x, int y)
    {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }
}
