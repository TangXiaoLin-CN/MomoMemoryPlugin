using MomoBackend.Core;
using MomoBackend.Models;
using System.Drawing;

namespace MomoBackend;

/// <summary>
/// 测试窗口 - 用于验证配置的功能
/// </summary>
public class TestForm : Form
{
    private readonly AppConfig _config;
    private readonly WindowInfo? _selectedWindow;
    private readonly WindowManager _windowManager;
    private readonly MouseController _mouseController;
    private readonly ScreenshotService _screenshotService;
    private readonly OcrService _windowsOcrService;

    // 点击测试
    private ListView _clickPointsListView = null!;
    private Button _testClickBtn = null!;
    private Button _testAllClicksBtn = null!;

    // OCR 测试
    private TextBox _ocr1Result = null!;
    private TextBox _ocr2Result = null!;
    private Button _testOcr1Btn = null!;
    private Button _testOcr2Btn = null!;
    private Button _testAllOcrBtn = null!;

    // 自动刷新
    private CheckBox _autoRefreshCheck = null!;
    private System.Windows.Forms.Timer? _ocrTimer;

    // 日志
    private TextBox _logTextBox = null!;

    public TestForm(AppConfig config, WindowInfo? selectedWindow)
    {
        _config = config;
        _selectedWindow = selectedWindow;
        _windowManager = new WindowManager();
        _mouseController = new MouseController();
        _screenshotService = new ScreenshotService();
        _windowsOcrService = new OcrService();

        InitializeComponent();
        LoadClickPoints();
    }

    private void InitializeComponent()
    {
        this.Text = "配置测试";
        this.Size = new Size(700, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        int y = 10;

        // ========== 目标窗口信息 ==========
        var windowInfoLabel = new Label
        {
            Text = _selectedWindow != null
                ? $"目标窗口: {_selectedWindow.Title} [{_selectedWindow.ProcessName}]"
                : "未选择目标窗口",
            Location = new Point(10, y),
            Width = 650,
            ForeColor = _selectedWindow != null ? Color.Green : Color.Red,
            Font = new Font(this.Font, FontStyle.Bold)
        };
        this.Controls.Add(windowInfoLabel);
        y += 25;

        // ========== 点击测试 ==========
        var clickGroup = new GroupBox
        {
            Text = "点击测试",
            Location = new Point(10, y),
            Size = new Size(665, 180)
        };

        _clickPointsListView = new ListView
        {
            Location = new Point(10, 20),
            Size = new Size(530, 120),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _clickPointsListView.Columns.Add("别名", 150);
        _clickPointsListView.Columns.Add("坐标", 100);
        _clickPointsListView.Columns.Add("模式", 120);
        _clickPointsListView.Columns.Add("状态", 100);

        _testClickBtn = new Button
        {
            Text = "测试选中",
            Location = new Point(550, 20),
            Width = 100,
            BackColor = Color.LightGreen
        };
        _testClickBtn.Click += TestClick_Click;

        _testAllClicksBtn = new Button
        {
            Text = "测试全部",
            Location = new Point(550, 55),
            Width = 100
        };
        _testAllClicksBtn.Click += TestAllClicks_Click;

        var clickTipLabel = new Label
        {
            Text = "提示: 双击列表项可快速测试单个点击区域",
            Location = new Point(10, 145),
            Width = 400,
            ForeColor = Color.Gray
        };

        clickGroup.Controls.AddRange(new Control[] {
            _clickPointsListView, _testClickBtn, _testAllClicksBtn, clickTipLabel
        });
        this.Controls.Add(clickGroup);
        y += 190;

        // ========== OCR 测试 ==========
        var ocrGroup = new GroupBox
        {
            Text = "OCR 测试",
            Location = new Point(10, y),
            Size = new Size(665, 160)
        };

        // OCR 区域1（列表中第一个）
        var region1 = _config.OcrRegions.Count > 0 ? _config.OcrRegions[0] : null;
        var ocr1Label = new Label
        {
            Text = region1 != null && region1.Enabled
                ? $"{region1.Alias}: ({region1.X},{region1.Y}) {region1.Width}x{region1.Height}"
                : "区域1: (未配置)",
            Location = new Point(10, 25),
            Width = 250,
            ForeColor = region1?.Enabled == true ? Color.Black : Color.Gray
        };

        _ocr1Result = new TextBox
        {
            Location = new Point(10, 45),
            Size = new Size(300, 50),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft YaHei", 9)
        };

        _testOcr1Btn = new Button
        {
            Text = "识别区域1",
            Location = new Point(10, 100),
            Width = 100,
            Enabled = region1?.Enabled == true
        };
        _testOcr1Btn.Click += async (s, e) => await TestOcrAsync(0);

        // OCR 区域2（列表中第二个）
        var region2 = _config.OcrRegions.Count > 1 ? _config.OcrRegions[1] : null;
        var ocr2Label = new Label
        {
            Text = region2 != null && region2.Enabled
                ? $"{region2.Alias}: ({region2.X},{region2.Y}) {region2.Width}x{region2.Height}"
                : "区域2: (未配置)",
            Location = new Point(340, 25),
            Width = 250,
            ForeColor = region2?.Enabled == true ? Color.Black : Color.Gray
        };

        _ocr2Result = new TextBox
        {
            Location = new Point(340, 45),
            Size = new Size(300, 50),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft YaHei", 9)
        };

        _testOcr2Btn = new Button
        {
            Text = "识别区域2",
            Location = new Point(340, 100),
            Width = 100,
            Enabled = region2?.Enabled == true
        };
        _testOcr2Btn.Click += async (s, e) => await TestOcrAsync(1);

        _testAllOcrBtn = new Button
        {
            Text = "识别全部",
            Location = new Point(450, 100),
            Width = 100,
            BackColor = Color.LightGreen
        };
        _testAllOcrBtn.Click += async (s, e) =>
        {
            for (int i = 0; i < _config.OcrRegions.Count; i++)
            {
                if (_config.OcrRegions[i].Enabled)
                    await TestOcrAsync(i);
            }
        };

        _autoRefreshCheck = new CheckBox
        {
            Text = $"自动刷新 ({_config.OcrRefreshInterval / 1000}秒)",
            Location = new Point(560, 100),
            Width = 100,
            Checked = _config.OcrAutoRefresh
        };
        _autoRefreshCheck.CheckedChanged += AutoRefreshCheck_Changed;

        ocrGroup.Controls.AddRange(new Control[] {
            ocr1Label, _ocr1Result, _testOcr1Btn,
            ocr2Label, _ocr2Result, _testOcr2Btn,
            _testAllOcrBtn, _autoRefreshCheck
        });
        this.Controls.Add(ocrGroup);
        y += 170;

        // ========== 日志 ==========
        var logLabel = new Label { Text = "测试日志:", Location = new Point(10, y), Width = 70 };
        this.Controls.Add(logLabel);
        y += 20;

        _logTextBox = new TextBox
        {
            Location = new Point(10, y),
            Size = new Size(665, 150),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9)
        };
        this.Controls.Add(_logTextBox);

        // 双击测试点击
        _clickPointsListView.DoubleClick += (s, e) => TestClick_Click(s, e);

        // 如果配置了自动刷新，启动定时器
        if (_config.OcrAutoRefresh)
        {
            StartAutoRefresh();
        }
    }

    private void LoadClickPoints()
    {
        _clickPointsListView.Items.Clear();
        foreach (var point in _config.ClickPoints)
        {
            var item = new ListViewItem(point.Alias);
            item.SubItems.Add($"({point.X}, {point.Y})");
            item.SubItems.Add(point.ClickMode);
            item.SubItems.Add("待测试");
            item.Tag = point;
            _clickPointsListView.Items.Add(item);
        }
    }

    private void TestClick_Click(object? sender, EventArgs e)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        if (_clickPointsListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要测试的点击区域", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedItem = _clickPointsListView.SelectedItems[0];
        var point = selectedItem.Tag as ClickPoint;
        if (point == null) return;

        TestClickPoint(point, selectedItem);
    }

    private async void TestAllClicks_Click(object? sender, EventArgs e)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        _testAllClicksBtn.Enabled = false;
        Log("开始测试所有点击区域...");

        foreach (ListViewItem item in _clickPointsListView.Items)
        {
            var point = item.Tag as ClickPoint;
            if (point != null)
            {
                TestClickPoint(point, item);
                await Task.Delay(500); // 间隔 500ms
            }
        }

        Log("所有点击区域测试完成");
        _testAllClicksBtn.Enabled = true;
    }

    private void TestClickPoint(ClickPoint point, ListViewItem item)
    {
        if (_selectedWindow == null) return;

        // 检查窗口是否有效
        if (!_windowManager.IsWindowValid((IntPtr)_selectedWindow.Hwnd))
        {
            Log($"错误: 目标窗口已失效");
            item.SubItems[3].Text = "窗口失效";
            item.ForeColor = Color.Red;
            return;
        }

        // 获取客户区原点
        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (clientOrigin == null)
        {
            Log($"错误: 无法获取窗口客户区");
            item.SubItems[3].Text = "获取失败";
            item.ForeColor = Color.Red;
            return;
        }

        Log($"测试点击: {point.Alias} ({point.X}, {point.Y}) 模式={point.ClickMode}");

        // 执行点击
        var result = _mouseController.Click(
            (IntPtr)_selectedWindow.Hwnd,
            point.X, point.Y,
            clientOrigin.Value.X, clientOrigin.Value.Y,
            point.ClickMode, point.Button
        );

        if (result.Success)
        {
            item.SubItems[3].Text = "成功";
            item.ForeColor = Color.Green;
            Log($"  ✓ {point.Alias}: 点击成功");
        }
        else
        {
            item.SubItems[3].Text = "失败";
            item.ForeColor = Color.Red;
            Log($"  ✗ {point.Alias}: {result.Message}");
        }
    }

    private async Task TestOcrAsync(int regionIndex)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        if (regionIndex < 0 || regionIndex >= _config.OcrRegions.Count)
        {
            Log($"错误: OCR 区域索引 {regionIndex} 超出范围");
            return;
        }

        var region = _config.OcrRegions[regionIndex];
        var resultBox = regionIndex == 0 ? _ocr1Result : (regionIndex == 1 ? _ocr2Result : null);

        if (!region.Enabled)
        {
            Log($"{region.Alias}: 未启用");
            return;
        }

        var engineName = _config.OcrEngine == "paddle" ? "PaddleOCR" : "Windows OCR";
        Log($"识别 {region.Alias}: ({region.X},{region.Y}) {region.Width}x{region.Height} [{engineName}]");

        var rect = new Rectangle(region.X, region.Y, region.Width, region.Height);
        var bitmap = _screenshotService.CaptureRegion((IntPtr)_selectedWindow.Hwnd, rect);

        if (bitmap == null)
        {
            Log($"{region.Alias}: 截图失败");
            if (resultBox != null) resultBox.Text = "[截图失败]";
            return;
        }

        try
        {
            var langCode = region.Language switch
            {
                "zh" => "zh",
                "en" => "en",
                _ => "auto"
            };

            OcrResult result;
            if (_config.OcrEngine == "paddle")
            {
                // 使用 PaddleOCR（单例）
                result = await PaddleOcrService.Instance.RecognizeAsync(bitmap, langCode);
            }
            else
            {
                // 使用 Windows OCR
                result = await _windowsOcrService.RecognizeAsync(bitmap, langCode);
            }

            if (result.Success)
            {
                if (resultBox != null) resultBox.Text = result.Text;
                Log($"{region.Alias}: {(string.IsNullOrEmpty(result.Text) ? "[无文字]" : result.Text)}");
            }
            else
            {
                if (resultBox != null) resultBox.Text = $"[识别失败: {result.ErrorMessage}]";
                Log($"{region.Alias}: 识别失败 - {result.ErrorMessage}");
            }
        }
        finally
        {
            bitmap.Dispose();
        }
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
            Interval = _config.OcrRefreshInterval
        };
        _ocrTimer.Tick += async (s, e) =>
        {
            if (_selectedWindow != null)
            {
                for (int i = 0; i < _config.OcrRegions.Count; i++)
                {
                    if (_config.OcrRegions[i].Enabled)
                        await TestOcrAsync(i);
                }
            }
        };
        _ocrTimer.Start();
        Log($"自动刷新已启动，间隔 {_config.OcrRefreshInterval / 1000} 秒");
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
        // PaddleOcrService 是单例，不在这里释放
        base.OnFormClosing(e);
    }
}
