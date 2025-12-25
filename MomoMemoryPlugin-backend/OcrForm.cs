using MomoBackend.Core;
using MomoBackend.Models;
using System.Drawing;

namespace MomoBackend;

/// <summary>
/// OCR 识别窗口
/// </summary>
public class OcrForm : Form
{
    private readonly OcrService _ocrService;
    private readonly ScreenshotService _screenshotService;
    private readonly WindowManager _windowManager;

    // 窗口选择
    private ComboBox _windowComboBox = null!;
    private Button _refreshWindowsBtn = null!;
    private Label _ocrStatusLabel = null!;

    // 区域1
    private NumericUpDown _region1X = null!;
    private NumericUpDown _region1Y = null!;
    private NumericUpDown _region1Width = null!;
    private NumericUpDown _region1Height = null!;
    private ComboBox _region1Lang = null!;
    private CheckBox _region1Enabled = null!;
    private TextBox _region1Result = null!;
    private Button _region1Capture = null!;

    // 区域2
    private NumericUpDown _region2X = null!;
    private NumericUpDown _region2Y = null!;
    private NumericUpDown _region2Width = null!;
    private NumericUpDown _region2Height = null!;
    private ComboBox _region2Lang = null!;
    private CheckBox _region2Enabled = null!;
    private TextBox _region2Result = null!;
    private Button _region2Capture = null!;

    // 自动刷新
    private CheckBox _autoRefreshCheck = null!;
    private NumericUpDown _refreshInterval = null!;
    private Button _manualOcrBtn = null!;
    private Button _previewBtn = null!;

    // 日志
    private TextBox _logTextBox = null!;

    // 定时器
    private System.Windows.Forms.Timer? _ocrTimer;

    // 当前选中窗口
    private WindowInfo? _selectedWindow;

    public OcrForm()
    {
        _ocrService = new OcrService();
        _screenshotService = new ScreenshotService();
        _windowManager = new WindowManager();

        InitializeComponent();
        LoadWindows();
    }

    private void InitializeComponent()
    {
        this.Text = "OCR 文字识别";
        this.Size = new Size(700, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.Sizable;

        int y = 10;
        int labelWidth = 70;
        int inputWidth = 60;

        // ========== 窗口选择 ==========
        var windowLabel = new Label { Text = "目标窗口:", Location = new Point(10, y + 3), Width = labelWidth };
        _windowComboBox = new ComboBox
        {
            Location = new Point(80, y),
            Width = 400,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _windowComboBox.SelectedIndexChanged += WindowComboBox_SelectedIndexChanged;

        _refreshWindowsBtn = new Button { Text = "刷新", Location = new Point(490, y - 1), Width = 50 };
        _refreshWindowsBtn.Click += (s, e) => LoadWindows();

        _ocrStatusLabel = new Label
        {
            Text = $"OCR状态: {_ocrService.GetStatus()}",
            Location = new Point(550, y + 3),
            Width = 130,
            ForeColor = _ocrService.IsAvailable ? Color.Green : Color.Red
        };

        this.Controls.AddRange(new Control[] { windowLabel, _windowComboBox, _refreshWindowsBtn, _ocrStatusLabel });
        y += 35;

        // ========== 区域1 ==========
        var region1Group = new GroupBox
        {
            Text = "识别区域 1",
            Location = new Point(10, y),
            Size = new Size(330, 160)
        };

        int gy = 20;
        _region1Enabled = new CheckBox { Text = "启用", Location = new Point(10, gy), Checked = true, Width = 60 };

        var r1XLabel = new Label { Text = "X:", Location = new Point(70, gy + 3), Width = 20 };
        _region1X = new NumericUpDown { Location = new Point(90, gy), Width = inputWidth, Minimum = 0, Maximum = 5000 };

        var r1YLabel = new Label { Text = "Y:", Location = new Point(160, gy + 3), Width = 20 };
        _region1Y = new NumericUpDown { Location = new Point(180, gy), Width = inputWidth, Minimum = 0, Maximum = 5000 };

        gy += 30;
        var r1WLabel = new Label { Text = "宽:", Location = new Point(10, gy + 3), Width = 25 };
        _region1Width = new NumericUpDown { Location = new Point(35, gy), Width = inputWidth, Minimum = 10, Maximum = 2000, Value = 200 };

        var r1HLabel = new Label { Text = "高:", Location = new Point(105, gy + 3), Width = 25 };
        _region1Height = new NumericUpDown { Location = new Point(130, gy), Width = inputWidth, Minimum = 10, Maximum = 2000, Value = 50 };

        var r1LangLabel = new Label { Text = "语言:", Location = new Point(200, gy + 3), Width = 35 };
        _region1Lang = new ComboBox
        {
            Location = new Point(235, gy),
            Width = 80,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _region1Lang.Items.AddRange(new object[] { "自动", "中文", "英文" });
        _region1Lang.SelectedIndex = 0;

        gy += 30;
        _region1Capture = new Button { Text = "框选区域", Location = new Point(10, gy), Width = 80, BackColor = Color.LightYellow };
        _region1Capture.Click += Region1SelectVisual_Click;

        var region1OcrBtn = new Button { Text = "识别", Location = new Point(95, gy), Width = 50 };
        region1OcrBtn.Click += async (s, e) => await CaptureRegionAsync(1);

        gy += 30;
        _region1Result = new TextBox
        {
            Location = new Point(10, gy),
            Size = new Size(305, 50),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft YaHei", 9)
        };

        region1Group.Controls.AddRange(new Control[] {
            _region1Enabled, r1XLabel, _region1X, r1YLabel, _region1Y,
            r1WLabel, _region1Width, r1HLabel, _region1Height,
            r1LangLabel, _region1Lang, _region1Capture, region1OcrBtn, _region1Result
        });
        this.Controls.Add(region1Group);

        // ========== 区域2 ==========
        var region2Group = new GroupBox
        {
            Text = "识别区域 2",
            Location = new Point(350, y),
            Size = new Size(330, 160)
        };

        gy = 20;
        _region2Enabled = new CheckBox { Text = "启用", Location = new Point(10, gy), Checked = false, Width = 60 };

        var r2XLabel = new Label { Text = "X:", Location = new Point(70, gy + 3), Width = 20 };
        _region2X = new NumericUpDown { Location = new Point(90, gy), Width = inputWidth, Minimum = 0, Maximum = 5000 };

        var r2YLabel = new Label { Text = "Y:", Location = new Point(160, gy + 3), Width = 20 };
        _region2Y = new NumericUpDown { Location = new Point(180, gy), Width = inputWidth, Minimum = 0, Maximum = 5000, Value = 50 };

        gy += 30;
        var r2WLabel = new Label { Text = "宽:", Location = new Point(10, gy + 3), Width = 25 };
        _region2Width = new NumericUpDown { Location = new Point(35, gy), Width = inputWidth, Minimum = 10, Maximum = 2000, Value = 200 };

        var r2HLabel = new Label { Text = "高:", Location = new Point(105, gy + 3), Width = 25 };
        _region2Height = new NumericUpDown { Location = new Point(130, gy), Width = inputWidth, Minimum = 10, Maximum = 2000, Value = 50 };

        var r2LangLabel = new Label { Text = "语言:", Location = new Point(200, gy + 3), Width = 35 };
        _region2Lang = new ComboBox
        {
            Location = new Point(235, gy),
            Width = 80,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _region2Lang.Items.AddRange(new object[] { "自动", "中文", "英文" });
        _region2Lang.SelectedIndex = 0;

        gy += 30;
        _region2Capture = new Button { Text = "框选区域", Location = new Point(10, gy), Width = 80, BackColor = Color.LightYellow };
        _region2Capture.Click += Region2SelectVisual_Click;

        var region2OcrBtn = new Button { Text = "识别", Location = new Point(95, gy), Width = 50 };
        region2OcrBtn.Click += async (s, e) => await CaptureRegionAsync(2);

        gy += 30;
        _region2Result = new TextBox
        {
            Location = new Point(10, gy),
            Size = new Size(305, 50),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft YaHei", 9)
        };

        region2Group.Controls.AddRange(new Control[] {
            _region2Enabled, r2XLabel, _region2X, r2YLabel, _region2Y,
            r2WLabel, _region2Width, r2HLabel, _region2Height,
            r2LangLabel, _region2Lang, _region2Capture, region2OcrBtn, _region2Result
        });
        this.Controls.Add(region2Group);

        y += 170;

        // ========== 操作按钮 ==========
        _manualOcrBtn = new Button
        {
            Text = "执行 OCR 识别",
            Location = new Point(10, y),
            Size = new Size(120, 35),
            BackColor = Color.LightGreen
        };
        _manualOcrBtn.Click += ManualOcrBtn_Click;

        _previewBtn = new Button
        {
            Text = "预览截图",
            Location = new Point(140, y),
            Size = new Size(80, 35)
        };
        _previewBtn.Click += PreviewBtn_Click;

        _autoRefreshCheck = new CheckBox
        {
            Text = "自动刷新",
            Location = new Point(240, y + 8),
            Width = 80
        };
        _autoRefreshCheck.CheckedChanged += AutoRefreshCheck_Changed;

        var intervalLabel = new Label { Text = "间隔(秒):", Location = new Point(320, y + 10), Width = 60 };
        _refreshInterval = new NumericUpDown
        {
            Location = new Point(380, y + 7),
            Width = 60,
            Minimum = 1,
            Maximum = 60,
            Value = 3
        };

        this.Controls.AddRange(new Control[] { _manualOcrBtn, _previewBtn, _autoRefreshCheck, intervalLabel, _refreshInterval });
        y += 45;

        // ========== 日志 ==========
        var logLabel = new Label { Text = "识别日志:", Location = new Point(10, y), Width = 70 };
        this.Controls.Add(logLabel);
        y += 20;

        _logTextBox = new TextBox
        {
            Location = new Point(10, y),
            Size = new Size(665, 220),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9)
        };
        this.Controls.Add(_logTextBox);

        // 绑定窗口大小变化
        this.Resize += (s, e) =>
        {
            _logTextBox.Width = this.ClientSize.Width - 25;
            _logTextBox.Height = this.ClientSize.Height - _logTextBox.Top - 10;
        };
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
            Log($"已选择窗口: {_selectedWindow.Title}");
        }
    }

    private async void Region1Capture_Click(object? sender, EventArgs e)
    {
        await CaptureRegionAsync(1);
    }

    private async void Region2Capture_Click(object? sender, EventArgs e)
    {
        await CaptureRegionAsync(2);
    }

    private void Region1SelectVisual_Click(object? sender, EventArgs e)
    {
        SelectRegionVisual(1);
    }

    private void Region2SelectVisual_Click(object? sender, EventArgs e)
    {
        SelectRegionVisual(2);
    }

    /// <summary>
    /// 可视化框选区域
    /// </summary>
    private void SelectRegionVisual(int regionNum)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 截取窗口图像
        var bitmap = _screenshotService.CaptureWindow((IntPtr)_selectedWindow.Hwnd);
        if (bitmap == null)
        {
            MessageBox.Show("截图失败，请确保目标窗口存在", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 计算客户区偏移
        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (!NativeMethods.GetWindowRect((IntPtr)_selectedWindow.Hwnd, out var windowRect))
        {
            bitmap.Dispose();
            return;
        }

        int offsetX = clientOrigin?.X - windowRect.Left ?? 0;
        int offsetY = clientOrigin?.Y - windowRect.Top ?? 0;

        // 打开区域选择器
        using var selector = new RegionSelectorForm(bitmap, offsetX, offsetY);
        var result = selector.ShowDialog();

        if (result == DialogResult.OK && selector.HasSelection)
        {
            var region = selector.SelectedRegion;

            // 更新对应区域的坐标
            if (regionNum == 1)
            {
                _region1X.Value = Math.Max(0, region.X);
                _region1Y.Value = Math.Max(0, region.Y);
                _region1Width.Value = Math.Max(10, region.Width);
                _region1Height.Value = Math.Max(10, region.Height);
                _region1Enabled.Checked = true;
                Log($"区域1 已设置: ({region.X},{region.Y}) {region.Width}x{region.Height}");
            }
            else
            {
                _region2X.Value = Math.Max(0, region.X);
                _region2Y.Value = Math.Max(0, region.Y);
                _region2Width.Value = Math.Max(10, region.Width);
                _region2Height.Value = Math.Max(10, region.Height);
                _region2Enabled.Checked = true;
                Log($"区域2 已设置: ({region.X},{region.Y}) {region.Width}x{region.Height}");
            }
        }

        bitmap.Dispose();
    }

    private async Task CaptureRegionAsync(int regionNum)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var (x, y, width, height, lang, resultBox) = regionNum == 1
            ? ((int)_region1X.Value, (int)_region1Y.Value, (int)_region1Width.Value, (int)_region1Height.Value,
               _region1Lang.SelectedItem?.ToString() ?? "自动", _region1Result)
            : ((int)_region2X.Value, (int)_region2Y.Value, (int)_region2Width.Value, (int)_region2Height.Value,
               _region2Lang.SelectedItem?.ToString() ?? "自动", _region2Result);

        Log($"捕获区域{regionNum}: ({x},{y}) {width}x{height}");

        var region = new Rectangle(x, y, width, height);
        var bitmap = _screenshotService.CaptureRegion((IntPtr)_selectedWindow.Hwnd, region);

        if (bitmap == null)
        {
            Log($"区域{regionNum}: 截图失败");
            resultBox.Text = "[截图失败]";
            return;
        }

        try
        {
            var langCode = lang switch
            {
                "中文" => "zh",
                "英文" => "en",
                _ => "auto"
            };

            var result = await _ocrService.RecognizeAsync(bitmap, langCode);

            if (result.Success)
            {
                resultBox.Text = result.Text;
                Log($"区域{regionNum}: {(string.IsNullOrEmpty(result.Text) ? "[无文字]" : result.Text)}");
            }
            else
            {
                resultBox.Text = $"[识别失败: {result.ErrorMessage}]";
                Log($"区域{regionNum}: 识别失败 - {result.ErrorMessage}");
            }
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private async void ManualOcrBtn_Click(object? sender, EventArgs e)
    {
        _manualOcrBtn.Enabled = false;
        try
        {
            if (_region1Enabled.Checked)
                await CaptureRegionAsync(1);
            if (_region2Enabled.Checked)
                await CaptureRegionAsync(2);
        }
        finally
        {
            _manualOcrBtn.Enabled = true;
        }
    }

    private void PreviewBtn_Click(object? sender, EventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var bitmap = _screenshotService.CaptureWindow((IntPtr)_selectedWindow.Hwnd);
        if (bitmap == null)
        {
            MessageBox.Show("截图失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 在位图上绘制区域框
        using (var g = Graphics.FromImage(bitmap))
        {
            using var pen1 = new Pen(Color.Red, 2);
            using var pen2 = new Pen(Color.Blue, 2);

            // 获取客户区偏移
            var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
            if (!NativeMethods.GetWindowRect((IntPtr)_selectedWindow.Hwnd, out var windowRect))
                return;

            int offsetX = clientOrigin?.X - windowRect.Left ?? 0;
            int offsetY = clientOrigin?.Y - windowRect.Top ?? 0;

            if (_region1Enabled.Checked)
            {
                g.DrawRectangle(pen1,
                    (int)_region1X.Value + offsetX,
                    (int)_region1Y.Value + offsetY,
                    (int)_region1Width.Value,
                    (int)_region1Height.Value);
                g.DrawString("区域1", this.Font, Brushes.Red,
                    (int)_region1X.Value + offsetX,
                    (int)_region1Y.Value + offsetY - 15);
            }

            if (_region2Enabled.Checked)
            {
                g.DrawRectangle(pen2,
                    (int)_region2X.Value + offsetX,
                    (int)_region2Y.Value + offsetY,
                    (int)_region2Width.Value,
                    (int)_region2Height.Value);
                g.DrawString("区域2", this.Font, Brushes.Blue,
                    (int)_region2X.Value + offsetX,
                    (int)_region2Y.Value + offsetY - 15);
            }
        }

        // 显示预览窗口
        var previewForm = new Form
        {
            Text = "截图预览 (按ESC关闭)",
            StartPosition = FormStartPosition.CenterScreen,
            KeyPreview = true
        };

        var pictureBox = new PictureBox
        {
            Image = bitmap,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill
        };

        previewForm.Controls.Add(pictureBox);
        previewForm.Size = new Size(
            Math.Min(bitmap.Width + 20, 1200),
            Math.Min(bitmap.Height + 40, 800)
        );

        previewForm.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                previewForm.Close();
        };

        previewForm.FormClosed += (s, e) => bitmap.Dispose();
        previewForm.Show();

        Log("预览截图已显示");
    }

    private void AutoRefreshCheck_Changed(object? sender, EventArgs e)
    {
        if (_autoRefreshCheck.Checked)
        {
            StartAutoRefresh();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    private void StartAutoRefresh()
    {
        _ocrTimer?.Stop();
        _ocrTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)_refreshInterval.Value * 1000
        };
        _ocrTimer.Tick += async (s, e) =>
        {
            if (_selectedWindow != null)
            {
                if (_region1Enabled.Checked)
                    await CaptureRegionAsync(1);
                if (_region2Enabled.Checked)
                    await CaptureRegionAsync(2);
            }
        };
        _ocrTimer.Start();
        Log($"自动刷新已启动，间隔 {_refreshInterval.Value} 秒");
    }

    private void StopAutoRefresh()
    {
        _ocrTimer?.Stop();
        _ocrTimer?.Dispose();
        _ocrTimer = null;
        Log("自动刷新已停止");
    }

    private void Log(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        _logTextBox.AppendText($"[{time}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopAutoRefresh();
        base.OnFormClosing(e);
    }
}
