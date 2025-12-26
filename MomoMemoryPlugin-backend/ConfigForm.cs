using MomoBackend.Core;
using MomoBackend.Models;
using System.Drawing;

namespace MomoBackend;

/// <summary>
/// 配置窗口 - 管理点击区域和 OCR 区域配置
/// </summary>
public class ConfigForm : Form
{
    private readonly ConfigService _configService;
    private readonly WindowManager _windowManager;
    private readonly ScreenshotService _screenshotService;

    // 目标窗口
    private ComboBox _windowComboBox = null!;
    private Button _refreshWindowsBtn = null!;
    private TextBox _targetTitleText = null!;
    private TextBox _targetProcessText = null!;

    // 点击区域列表
    private ListView _clickPointsListView = null!;
    private Button _addPointBtn = null!;
    private Button _editPointBtn = null!;
    private Button _deletePointBtn = null!;
    private Button _capturePointBtn = null!;
    private Button _previewPointsBtn = null!;

    // OCR 区域列表
    private ListView _ocrRegionsListView = null!;
    private Button _addOcrRegionBtn = null!;
    private Button _editOcrRegionBtn = null!;
    private Button _deleteOcrRegionBtn = null!;
    private Button _captureOcrRegionBtn = null!;
    private Button _previewOcrRegionsBtn = null!;

    // OCR 自动刷新设置
    private NumericUpDown _ocrRefreshInterval = null!;
    private CheckBox _ocrAutoRefresh = null!;
    private ComboBox _ocrEngineCombo = null!;

    // fast_background 点击参数
    private NumericUpDown _fbAlpha = null!;
    private NumericUpDown _fbDelayAfterRestore = null!;
    private NumericUpDown _fbDelayBeforeClick = null!;
    private NumericUpDown _fbDelayAfterMove = null!;
    private NumericUpDown _fbDelayAfterClick = null!;
    private NumericUpDown _fbDelayBeforeRestore = null!;
    private CheckBox _fbMinimizeAfterClick = null!;
    private CheckBox _fbHideCursor = null!;

    // 操作按钮
    private Button _saveBtn = null!;
    private Button _loadBtn = null!;
    private Button _testBtn = null!;
    private Label _configPathLabel = null!;

    // 当前选中窗口
    private WindowInfo? _selectedWindow;

    public ConfigForm()
    {
        _configService = new ConfigService();
        _windowManager = new WindowManager();
        _screenshotService = new ScreenshotService();

        InitializeComponent();
        LoadWindows();
        LoadConfigToUI();
    }

    private void InitializeComponent()
    {
        this.Text = "配置管理";
        this.Size = new Size(750, 920);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        int y = 10;

        // ========== 目标窗口配置 ==========
        var windowGroup = new GroupBox
        {
            Text = "目标窗口配置",
            Location = new Point(10, y),
            Size = new Size(715, 100)
        };

        var windowLabel = new Label { Text = "选择窗口:", Location = new Point(10, 25), Width = 70 };
        _windowComboBox = new ComboBox
        {
            Location = new Point(80, 22),
            Width = 450,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _windowComboBox.SelectedIndexChanged += WindowComboBox_SelectedIndexChanged;

        _refreshWindowsBtn = new Button { Text = "刷新", Location = new Point(540, 21), Width = 50 };
        _refreshWindowsBtn.Click += (s, e) => LoadWindows();

        var applyWindowBtn = new Button { Text = "应用", Location = new Point(600, 21), Width = 50, BackColor = Color.LightGreen };
        applyWindowBtn.Click += ApplyWindow_Click;

        var titleLabel = new Label { Text = "窗口标题:", Location = new Point(10, 60), Width = 70 };
        _targetTitleText = new TextBox { Location = new Point(80, 57), Width = 280, ReadOnly = true };

        var processLabel = new Label { Text = "进程名:", Location = new Point(380, 60), Width = 55 };
        _targetProcessText = new TextBox { Location = new Point(435, 57), Width = 200, ReadOnly = true };

        windowGroup.Controls.AddRange(new Control[] {
            windowLabel, _windowComboBox, _refreshWindowsBtn, applyWindowBtn,
            titleLabel, _targetTitleText, processLabel, _targetProcessText
        });
        this.Controls.Add(windowGroup);
        y += 110;

        // ========== 点击区域配置 ==========
        var clickGroup = new GroupBox
        {
            Text = "点击区域配置",
            Location = new Point(10, y),
            Size = new Size(715, 200)
        };

        _clickPointsListView = new ListView
        {
            Location = new Point(10, 20),
            Size = new Size(580, 140),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _clickPointsListView.Columns.Add("别名", 120);
        _clickPointsListView.Columns.Add("X", 60);
        _clickPointsListView.Columns.Add("Y", 60);
        _clickPointsListView.Columns.Add("点击模式", 120);
        _clickPointsListView.Columns.Add("按键", 60);

        _addPointBtn = new Button { Text = "添加", Location = new Point(600, 20), Width = 100 };
        _addPointBtn.Click += AddPoint_Click;

        _editPointBtn = new Button { Text = "编辑", Location = new Point(600, 55), Width = 100 };
        _editPointBtn.Click += EditPoint_Click;

        _deletePointBtn = new Button { Text = "删除", Location = new Point(600, 90), Width = 100 };
        _deletePointBtn.Click += DeletePoint_Click;

        _capturePointBtn = new Button { Text = "框选捕获", Location = new Point(600, 125), Width = 100, BackColor = Color.LightYellow };
        _capturePointBtn.Click += CapturePoint_Click;

        _previewPointsBtn = new Button { Text = "预览位置", Location = new Point(600, 160), Width = 100, BackColor = Color.LightCyan };
        _previewPointsBtn.Click += PreviewPoints_Click;

        var tipLabel = new Label
        {
            Text = "提示: \"框选捕获\"在截图上选择区域 | \"预览位置\"查看所有配置的位置",
            Location = new Point(10, 165),
            Width = 580,
            ForeColor = Color.Gray
        };

        clickGroup.Controls.AddRange(new Control[] {
            _clickPointsListView, _addPointBtn, _editPointBtn, _deletePointBtn, _capturePointBtn, _previewPointsBtn, tipLabel
        });
        this.Controls.Add(clickGroup);
        y += 210;

        // ========== OCR 区域配置 ==========
        var ocrGroup = new GroupBox
        {
            Text = "OCR 识别区域配置",
            Location = new Point(10, y),
            Size = new Size(715, 200)
        };

        // OCR 区域列表
        _ocrRegionsListView = new ListView
        {
            Location = new Point(10, 20),
            Size = new Size(580, 100),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _ocrRegionsListView.Columns.Add("别名", 100);
        _ocrRegionsListView.Columns.Add("X", 50);
        _ocrRegionsListView.Columns.Add("Y", 50);
        _ocrRegionsListView.Columns.Add("宽", 50);
        _ocrRegionsListView.Columns.Add("高", 50);
        _ocrRegionsListView.Columns.Add("语言", 60);
        _ocrRegionsListView.Columns.Add("启用", 50);

        _addOcrRegionBtn = new Button { Text = "添加", Location = new Point(600, 20), Width = 100 };
        _addOcrRegionBtn.Click += AddOcrRegion_Click;

        _editOcrRegionBtn = new Button { Text = "编辑", Location = new Point(600, 55), Width = 100 };
        _editOcrRegionBtn.Click += EditOcrRegion_Click;

        _deleteOcrRegionBtn = new Button { Text = "删除", Location = new Point(600, 90), Width = 100 };
        _deleteOcrRegionBtn.Click += DeleteOcrRegion_Click;

        _captureOcrRegionBtn = new Button { Text = "框选捕获", Location = new Point(10, 125), Width = 100, BackColor = Color.LightYellow };
        _captureOcrRegionBtn.Click += CaptureOcrRegion_Click;

        _previewOcrRegionsBtn = new Button { Text = "预览区域", Location = new Point(120, 125), Width = 100, BackColor = Color.LightCyan };
        _previewOcrRegionsBtn.Click += PreviewOcrRegions_Click;

        // OCR 刷新设置
        var refreshLabel = new Label { Text = "间隔(秒):", Location = new Point(240, 128), Width = 60 };
        _ocrRefreshInterval = new NumericUpDown { Location = new Point(300, 125), Width = 50, Minimum = 1, Maximum = 60, Value = 3 };
        _ocrAutoRefresh = new CheckBox { Text = "自动刷新", Location = new Point(360, 127), Width = 80 };

        var engineLabel = new Label { Text = "OCR引擎:", Location = new Point(450, 128), Width = 60 };
        _ocrEngineCombo = new ComboBox { Location = new Point(510, 125), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _ocrEngineCombo.Items.AddRange(new object[] { "PaddleOCR (推荐)", "Windows OCR" });
        _ocrEngineCombo.SelectedIndex = 0;

        var ocrTipLabel = new Label
        {
            Text = "提示: OCR 区域在状态栏显示时使用别名标识",
            Location = new Point(10, 160),
            Width = 580,
            ForeColor = Color.Gray
        };

        ocrGroup.Controls.AddRange(new Control[] {
            _ocrRegionsListView, _addOcrRegionBtn, _editOcrRegionBtn, _deleteOcrRegionBtn,
            _captureOcrRegionBtn, _previewOcrRegionsBtn,
            refreshLabel, _ocrRefreshInterval, _ocrAutoRefresh, engineLabel, _ocrEngineCombo, ocrTipLabel
        });
        this.Controls.Add(ocrGroup);
        y += 210;

        // ========== 后台点击参数配置 ==========
        var fastBgGroup = new GroupBox
        {
            Text = "后台点击参数 (fast_background)",
            Location = new Point(10, y),
            Size = new Size(715, 200)
        };

        // 创建 ToolTip 组件
        var toolTip = new ToolTip
        {
            AutoPopDelay = 10000,  // 显示10秒
            InitialDelay = 300,
            ReshowDelay = 100,
            ShowAlways = true
        };

        int fbY = 20;
        int fbLabelWidth = 100;
        int fbNumWidth = 60;

        // 第一行：透明度、恢复后延迟、激活后延迟、移动后延迟
        var alphaLabel = new Label { Text = "窗口透明度:", Location = new Point(10, fbY + 3), Width = fbLabelWidth };
        _fbAlpha = new NumericUpDown { Location = new Point(110, fbY), Width = fbNumWidth, Minimum = 0, Maximum = 255, Value = 3 };
        toolTip.SetToolTip(_fbAlpha, "点击时目标窗口的透明度\n0 = 完全透明（不可见）\n255 = 完全不透明\n建议值: 1-10（近乎不可见但可点击）");
        toolTip.SetToolTip(alphaLabel, "点击时目标窗口的透明度 (0-255)");

        var restoreLabel = new Label { Text = "恢复后(ms):", Location = new Point(180, fbY + 3), Width = fbLabelWidth };
        _fbDelayAfterRestore = new NumericUpDown { Location = new Point(280, fbY), Width = fbNumWidth, Minimum = 0, Maximum = 1000, Value = 30 };
        toolTip.SetToolTip(_fbDelayAfterRestore, "窗口从最小化恢复后的等待时间\n等待窗口完全显示后再继续\n如果窗口恢复慢可增加此值");
        toolTip.SetToolTip(restoreLabel, "窗口恢复后等待时间 (毫秒)");

        var beforeClickLabel = new Label { Text = "激活后(ms):", Location = new Point(350, fbY + 3), Width = fbLabelWidth };
        _fbDelayBeforeClick = new NumericUpDown { Location = new Point(450, fbY), Width = fbNumWidth, Minimum = 0, Maximum = 1000, Value = 20 };
        toolTip.SetToolTip(_fbDelayBeforeClick, "窗口激活（获得焦点）后的等待时间\n等待窗口准备好接收输入\n如果点击经常失败可增加此值");
        toolTip.SetToolTip(beforeClickLabel, "窗口激活后、点击前等待时间 (毫秒)");

        var afterMoveLabel = new Label { Text = "移动后(ms):", Location = new Point(520, fbY + 3), Width = fbLabelWidth };
        _fbDelayAfterMove = new NumericUpDown { Location = new Point(620, fbY), Width = fbNumWidth, Minimum = 0, Maximum = 1000, Value = 10 };
        toolTip.SetToolTip(_fbDelayAfterMove, "鼠标移动到目标位置后的等待时间\n等待系统处理鼠标移动事件\n一般 5-20ms 即可");
        toolTip.SetToolTip(afterMoveLabel, "鼠标移动后、点击前等待时间 (毫秒)");

        fbY += 30;

        // 第二行：点击后延迟、切换前延迟
        var afterClickLabel = new Label { Text = "点击后(ms):", Location = new Point(10, fbY + 3), Width = fbLabelWidth };
        _fbDelayAfterClick = new NumericUpDown { Location = new Point(110, fbY), Width = fbNumWidth, Minimum = 0, Maximum = 1000, Value = 30 };
        toolTip.SetToolTip(_fbDelayAfterClick, "点击完成后的等待时间\n等待目标应用响应点击事件\n如果应用响应慢可增加此值");
        toolTip.SetToolTip(afterClickLabel, "点击后等待应用响应的时间 (毫秒)");

        var beforeRestoreLabel = new Label { Text = "切换前(ms):", Location = new Point(180, fbY + 3), Width = fbLabelWidth };
        _fbDelayBeforeRestore = new NumericUpDown { Location = new Point(280, fbY), Width = fbNumWidth, Minimum = 0, Maximum = 1000, Value = 20 };
        toolTip.SetToolTip(_fbDelayBeforeRestore, "切换回原来前台窗口前的等待时间\n确保点击已被处理后再切换\n一般 10-30ms 即可");
        toolTip.SetToolTip(beforeRestoreLabel, "切换回原窗口前的等待时间 (毫秒)");

        // 第二行：复选框
        _fbMinimizeAfterClick = new CheckBox { Text = "点击后最小化窗口", Location = new Point(360, fbY), Width = 140, Checked = true };
        toolTip.SetToolTip(_fbMinimizeAfterClick, "点击完成后是否将目标窗口最小化\n勾选: 点击后自动最小化目标窗口\n不勾选: 目标窗口保持原状态");

        _fbHideCursor = new CheckBox { Text = "隐藏鼠标光标", Location = new Point(510, fbY), Width = 120, Checked = true };
        toolTip.SetToolTip(_fbHideCursor, "点击时是否隐藏鼠标光标\n勾选: 点击过程中光标不可见\n不勾选: 可以看到光标移动");

        fbY += 35;

        // 第三行：详细说明
        var fbHelpLabel1 = new Label
        {
            Text = "【点击流程】恢复窗口 → 设置透明 → 等待(恢复后) → 激活窗口 → 等待(激活后) → 移动鼠标 → 等待(移动后) → 点击 → 等待(点击后) → 恢复鼠标 → 等待(切换前) → 切换窗口",
            Location = new Point(10, fbY),
            Width = 690,
            ForeColor = Color.DarkBlue
        };

        fbY += 20;
        var fbHelpLabel2 = new Label
        {
            Text = "【调试建议】如果点击不稳定: 1) 先增加\"激活后\"延迟到 50-100ms  2) 再增加\"点击后\"延迟  3) 透明度建议 1-10",
            Location = new Point(10, fbY),
            Width = 690,
            ForeColor = Color.Gray
        };

        fbY += 20;
        var fbHelpLabel3 = new Label
        {
            Text = "【注意】总延迟 = 恢复后 + 激活后 + 移动后 + 点击后 + 切换前，延迟越长点击越稳定但速度越慢",
            Location = new Point(10, fbY),
            Width = 690,
            ForeColor = Color.Gray
        };

        fastBgGroup.Controls.AddRange(new Control[] {
            alphaLabel, _fbAlpha, restoreLabel, _fbDelayAfterRestore,
            beforeClickLabel, _fbDelayBeforeClick, afterMoveLabel, _fbDelayAfterMove,
            afterClickLabel, _fbDelayAfterClick, beforeRestoreLabel, _fbDelayBeforeRestore,
            _fbMinimizeAfterClick, _fbHideCursor, fbHelpLabel1, fbHelpLabel2, fbHelpLabel3
        });
        this.Controls.Add(fastBgGroup);
        y += 210;

        // ========== 操作按钮 ==========
        _saveBtn = new Button
        {
            Text = "保存配置",
            Location = new Point(10, y),
            Size = new Size(100, 35),
            BackColor = Color.LightGreen
        };
        _saveBtn.Click += SaveConfig_Click;

        _loadBtn = new Button
        {
            Text = "重新加载",
            Location = new Point(120, y),
            Size = new Size(100, 35)
        };
        _loadBtn.Click += LoadConfig_Click;

        var exportBtn = new Button
        {
            Text = "导出配置",
            Location = new Point(230, y),
            Size = new Size(100, 35)
        };
        exportBtn.Click += ExportConfig_Click;

        var importBtn = new Button
        {
            Text = "导入配置",
            Location = new Point(340, y),
            Size = new Size(100, 35)
        };
        importBtn.Click += ImportConfig_Click;

        _testBtn = new Button
        {
            Text = "打开测试窗口",
            Location = new Point(450, y),
            Size = new Size(120, 35),
            BackColor = Color.LightBlue
        };
        _testBtn.Click += OpenTestWindow_Click;

        y += 45;

        _configPathLabel = new Label
        {
            Text = $"配置文件: {_configService.ConfigPath}",
            Location = new Point(10, y),
            Width = 700,
            ForeColor = Color.Gray
        };

        this.Controls.AddRange(new Control[] { _saveBtn, _loadBtn, exportBtn, importBtn, _testBtn, _configPathLabel });
    }

    private void LoadWindows()
    {
        _windowComboBox.Items.Clear();
        var windows = _windowManager.GetAllWindows();

        foreach (var win in windows.OrderBy(w => w.Title))
        {
            _windowComboBox.Items.Add(new WindowItem(win));
        }
    }

    private void WindowComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_windowComboBox.SelectedItem is WindowItem item)
        {
            _selectedWindow = item.Window;
            _targetTitleText.Text = _selectedWindow.Title;
            _targetProcessText.Text = _selectedWindow.ProcessName;
        }
    }

    private void ApplyWindow_Click(object? sender, EventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择窗口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _configService.Config.TargetWindowTitle = _selectedWindow.Title;
        _configService.Config.TargetProcessName = _selectedWindow.ProcessName;
        MessageBox.Show("已应用目标窗口配置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LoadConfigToUI()
    {
        var config = _configService.Config;

        // 加载目标窗口配置
        _targetTitleText.Text = config.TargetWindowTitle;
        _targetProcessText.Text = config.TargetProcessName;

        // 加载点击区域
        RefreshClickPointsList();

        // 加载 OCR 区域列表
        RefreshOcrRegionsList();

        // 加载 OCR 自动刷新设置
        _ocrRefreshInterval.Value = Math.Max(1, Math.Min(60, config.OcrRefreshInterval / 1000));
        _ocrAutoRefresh.Checked = config.OcrAutoRefresh;

        // 加载 OCR 引擎选择
        _ocrEngineCombo.SelectedIndex = config.OcrEngine == "windows" ? 1 : 0;

        // 加载 fast_background 点击参数
        _fbAlpha.Value = config.FastBackground.WindowAlpha;
        _fbDelayAfterRestore.Value = config.FastBackground.DelayAfterRestore;
        _fbDelayBeforeClick.Value = config.FastBackground.DelayBeforeClick;
        _fbDelayAfterMove.Value = config.FastBackground.DelayAfterMove;
        _fbDelayAfterClick.Value = config.FastBackground.DelayAfterClick;
        _fbDelayBeforeRestore.Value = config.FastBackground.DelayBeforeRestore;
        _fbMinimizeAfterClick.Checked = config.FastBackground.MinimizeAfterClick;
        _fbHideCursor.Checked = config.FastBackground.HideCursor;
    }

    private void RefreshOcrRegionsList()
    {
        _ocrRegionsListView.Items.Clear();
        foreach (var region in _configService.Config.OcrRegions)
        {
            var item = new ListViewItem(region.Alias);
            item.SubItems.Add(region.X.ToString());
            item.SubItems.Add(region.Y.ToString());
            item.SubItems.Add(region.Width.ToString());
            item.SubItems.Add(region.Height.ToString());
            item.SubItems.Add(region.Language switch { "zh" => "中文", "en" => "英文", _ => "自动" });
            item.SubItems.Add(region.Enabled ? "是" : "否");
            item.Tag = region;
            _ocrRegionsListView.Items.Add(item);
        }
    }

    private void RefreshClickPointsList()
    {
        _clickPointsListView.Items.Clear();
        foreach (var point in _configService.Config.ClickPoints)
        {
            var item = new ListViewItem(point.Alias);
            item.SubItems.Add(point.X.ToString());
            item.SubItems.Add(point.Y.ToString());
            item.SubItems.Add(point.ClickMode);
            item.SubItems.Add(point.Button);
            item.Tag = point;
            _clickPointsListView.Items.Add(item);
        }
    }

    private void AddPoint_Click(object? sender, EventArgs e)
    {
        using var dialog = new ClickPointEditForm(null);
        if (dialog.ShowDialog() == DialogResult.OK && dialog.ClickPoint != null)
        {
            _configService.AddClickPoint(dialog.ClickPoint);
            RefreshClickPointsList();
        }
    }

    private void EditPoint_Click(object? sender, EventArgs e)
    {
        if (_clickPointsListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要编辑的点击区域", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedItem = _clickPointsListView.SelectedItems[0];
        var point = selectedItem.Tag as ClickPoint;

        using var dialog = new ClickPointEditForm(point);
        if (dialog.ShowDialog() == DialogResult.OK && dialog.ClickPoint != null)
        {
            var index = _configService.Config.ClickPoints.IndexOf(point!);
            if (index >= 0)
            {
                _configService.Config.ClickPoints[index] = dialog.ClickPoint;
                RefreshClickPointsList();
            }
        }
    }

    private void DeletePoint_Click(object? sender, EventArgs e)
    {
        if (_clickPointsListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要删除的点击区域", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show("确定要删除选中的点击区域吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            var selectedItem = _clickPointsListView.SelectedItems[0];
            var point = selectedItem.Tag as ClickPoint;
            if (point != null)
            {
                _configService.Config.ClickPoints.Remove(point);
                RefreshClickPointsList();
            }
        }
    }

    private void CapturePoint_Click(object? sender, EventArgs e)
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

        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (!NativeMethods.GetWindowRect((IntPtr)_selectedWindow.Hwnd, out var windowRect))
        {
            bitmap.Dispose();
            return;
        }

        int offsetX = clientOrigin?.X - windowRect.Left ?? 0;
        int offsetY = clientOrigin?.Y - windowRect.Top ?? 0;

        using var selector = new RegionSelectorForm(bitmap, offsetX, offsetY);
        var result = selector.ShowDialog();

        if (result == DialogResult.OK && selector.HasSelection)
        {
            var region = selector.SelectedRegion;
            // 使用区域中心点作为点击坐标
            int centerX = region.X + region.Width / 2;
            int centerY = region.Y + region.Height / 2;

            // 打开编辑对话框
            var newPoint = new ClickPoint
            {
                X = centerX,
                Y = centerY,
                ClickMode = "fast_background",
                Button = "left"
            };

            using var dialog = new ClickPointEditForm(newPoint);
            if (dialog.ShowDialog() == DialogResult.OK && dialog.ClickPoint != null)
            {
                _configService.AddClickPoint(dialog.ClickPoint);
                RefreshClickPointsList();
            }
        }

        bitmap.Dispose();
    }

    private void PreviewPoints_Click(object? sender, EventArgs e)
    {
        PreviewAllRegions();
    }

    private void PreviewOcrRegions_Click(object? sender, EventArgs e)
    {
        PreviewAllRegions();
    }

    private void AddOcrRegion_Click(object? sender, EventArgs e)
    {
        using var dialog = new OcrRegionEditForm(null);
        if (dialog.ShowDialog() == DialogResult.OK && dialog.OcrRegion != null)
        {
            _configService.Config.OcrRegions.Add(dialog.OcrRegion);
            RefreshOcrRegionsList();
        }
    }

    private void EditOcrRegion_Click(object? sender, EventArgs e)
    {
        if (_ocrRegionsListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要编辑的 OCR 区域", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedItem = _ocrRegionsListView.SelectedItems[0];
        var region = selectedItem.Tag as OcrRegion;

        using var dialog = new OcrRegionEditForm(region);
        if (dialog.ShowDialog() == DialogResult.OK && dialog.OcrRegion != null)
        {
            var index = _configService.Config.OcrRegions.IndexOf(region!);
            if (index >= 0)
            {
                _configService.Config.OcrRegions[index] = dialog.OcrRegion;
                RefreshOcrRegionsList();
            }
        }
    }

    private void DeleteOcrRegion_Click(object? sender, EventArgs e)
    {
        if (_ocrRegionsListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择要删除的 OCR 区域", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show("确定要删除选中的 OCR 区域吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            var selectedItem = _ocrRegionsListView.SelectedItems[0];
            var region = selectedItem.Tag as OcrRegion;
            if (region != null)
            {
                _configService.Config.OcrRegions.Remove(region);
                RefreshOcrRegionsList();
            }
        }
    }

    private void CaptureOcrRegion_Click(object? sender, EventArgs e)
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

        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (!NativeMethods.GetWindowRect((IntPtr)_selectedWindow.Hwnd, out var windowRect))
        {
            bitmap.Dispose();
            return;
        }

        int offsetX = clientOrigin?.X - windowRect.Left ?? 0;
        int offsetY = clientOrigin?.Y - windowRect.Top ?? 0;

        using var selector = new RegionSelectorForm(bitmap, offsetX, offsetY);
        var result = selector.ShowDialog();

        if (result == DialogResult.OK && selector.HasSelection)
        {
            var selectedRegion = selector.SelectedRegion;

            var newRegion = new OcrRegion
            {
                X = selectedRegion.X,
                Y = selectedRegion.Y,
                Width = selectedRegion.Width,
                Height = selectedRegion.Height,
                Language = "auto",
                Enabled = true
            };

            using var dialog = new OcrRegionEditForm(newRegion);
            if (dialog.ShowDialog() == DialogResult.OK && dialog.OcrRegion != null)
            {
                _configService.Config.OcrRegions.Add(dialog.OcrRegion);
                RefreshOcrRegionsList();
            }
        }

        bitmap.Dispose();
    }

    private void PreviewAllRegions()
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

        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (!NativeMethods.GetWindowRect((IntPtr)_selectedWindow.Hwnd, out var windowRect))
        {
            bitmap.Dispose();
            return;
        }

        int offsetX = clientOrigin?.X - windowRect.Left ?? 0;
        int offsetY = clientOrigin?.Y - windowRect.Top ?? 0;

        // 在位图上绘制所有点击位置和 OCR 区域
        using (var g = Graphics.FromImage(bitmap))
        {
            // 绘制点击位置
            int pointIndex = 1;
            foreach (var point in _configService.Config.ClickPoints)
            {
                int screenX = point.X + offsetX;
                int screenY = point.Y + offsetY;

                // 绘制十字标记
                using var pen = new Pen(Color.Red, 2);
                g.DrawLine(pen, screenX - 10, screenY, screenX + 10, screenY);
                g.DrawLine(pen, screenX, screenY - 10, screenX, screenY + 10);
                g.DrawEllipse(pen, screenX - 5, screenY - 5, 10, 10);

                // 绘制标签
                using var font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                using var bgBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
                using var textBrush = new SolidBrush(Color.Red);
                var label = $"{pointIndex}. {point.Alias} ({point.X},{point.Y})";
                var labelSize = g.MeasureString(label, font);
                g.FillRectangle(bgBrush, screenX + 12, screenY - labelSize.Height / 2, labelSize.Width + 4, labelSize.Height);
                g.DrawString(label, font, textBrush, screenX + 14, screenY - labelSize.Height / 2);
                pointIndex++;
            }

            // 绘制 OCR 区域列表
            var colors = new[] { Color.Blue, Color.Green, Color.Purple, Color.Orange, Color.Teal };
            int regionIndex = 0;
            foreach (var region in _configService.Config.OcrRegions)
            {
                if (!region.Enabled) continue;

                var color = colors[regionIndex % colors.Length];
                using var pen = new Pen(color, 2);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(pen,
                    region.X + offsetX,
                    region.Y + offsetY,
                    region.Width,
                    region.Height);

                using var font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
                using var brush = new SolidBrush(color);
                var label = string.IsNullOrEmpty(region.Alias) ? $"OCR区域{regionIndex + 1}" : region.Alias;
                g.DrawString(label, font, brush,
                    region.X + offsetX,
                    region.Y + offsetY - 18);
                regionIndex++;
            }
        }

        // 显示预览窗口
        var previewForm = new Form
        {
            Text = "配置预览 (按ESC关闭)",
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
    }

    private void SaveUIToConfig()
    {
        var config = _configService.Config;

        // OCR 区域列表直接在添加/编辑时已保存到 config.OcrRegions

        // 保存 OCR 自动刷新设置
        config.OcrRefreshInterval = (int)_ocrRefreshInterval.Value * 1000;
        config.OcrAutoRefresh = _ocrAutoRefresh.Checked;

        // 保存 OCR 引擎选择
        config.OcrEngine = _ocrEngineCombo.SelectedIndex == 1 ? "windows" : "paddle";

        // 保存 fast_background 点击参数
        config.FastBackground.WindowAlpha = (byte)_fbAlpha.Value;
        config.FastBackground.DelayAfterRestore = (int)_fbDelayAfterRestore.Value;
        config.FastBackground.DelayBeforeClick = (int)_fbDelayBeforeClick.Value;
        config.FastBackground.DelayAfterMove = (int)_fbDelayAfterMove.Value;
        config.FastBackground.DelayAfterClick = (int)_fbDelayAfterClick.Value;
        config.FastBackground.DelayBeforeRestore = (int)_fbDelayBeforeRestore.Value;
        config.FastBackground.MinimizeAfterClick = _fbMinimizeAfterClick.Checked;
        config.FastBackground.HideCursor = _fbHideCursor.Checked;
    }

    private void SaveConfig_Click(object? sender, EventArgs e)
    {
        SaveUIToConfig();

        if (_configService.Save())
        {
            MessageBox.Show("配置已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("保存失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadConfig_Click(object? sender, EventArgs e)
    {
        _configService.Load();
        LoadConfigToUI();
        MessageBox.Show("配置已重新加载", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportConfig_Click(object? sender, EventArgs e)
    {
        SaveUIToConfig();

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件|*.json",
            DefaultExt = "json",
            FileName = "momo-config-export.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(dialog.FileName, _configService.ExportToJson());
                MessageBox.Show("配置已导出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ImportConfig_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON 文件|*.json",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                if (_configService.ImportFromJson(json))
                {
                    LoadConfigToUI();
                    MessageBox.Show("配置已导入", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("导入失败：无效的配置文件", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OpenTestWindow_Click(object? sender, EventArgs e)
    {
        SaveUIToConfig();
        var testForm = new TestForm(_configService.Config, _selectedWindow);
        testForm.Show();
    }
}

/// <summary>
/// 点击区域编辑对话框
/// </summary>
public class ClickPointEditForm : Form
{
    private TextBox _aliasText = null!;
    private NumericUpDown _xInput = null!;
    private NumericUpDown _yInput = null!;
    private ComboBox _modeCombo = null!;
    private ComboBox _buttonCombo = null!;

    public ClickPoint? ClickPoint { get; private set; }

    public ClickPointEditForm(ClickPoint? existing)
    {
        InitializeComponent();

        if (existing != null)
        {
            _aliasText.Text = existing.Alias;
            _xInput.Value = existing.X;
            _yInput.Value = existing.Y;
            _modeCombo.SelectedItem = existing.ClickMode;
            _buttonCombo.SelectedItem = existing.Button;
        }
    }

    private void InitializeComponent()
    {
        this.Text = "编辑点击区域";
        this.Size = new Size(350, 250);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        int y = 20;
        int labelWidth = 70;
        int inputLeft = 90;

        var aliasLabel = new Label { Text = "别名:", Location = new Point(20, y + 3), Width = labelWidth };
        _aliasText = new TextBox { Location = new Point(inputLeft, y), Width = 200 };
        y += 35;

        var xLabel = new Label { Text = "X 坐标:", Location = new Point(20, y + 3), Width = labelWidth };
        _xInput = new NumericUpDown { Location = new Point(inputLeft, y), Width = 100, Minimum = -10000, Maximum = 10000 };
        y += 35;

        var yLabel = new Label { Text = "Y 坐标:", Location = new Point(20, y + 3), Width = labelWidth };
        _yInput = new NumericUpDown { Location = new Point(inputLeft, y), Width = 100, Minimum = -10000, Maximum = 10000 };
        y += 35;

        var modeLabel = new Label { Text = "点击模式:", Location = new Point(20, y + 3), Width = labelWidth };
        _modeCombo = new ComboBox { Location = new Point(inputLeft, y), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _modeCombo.Items.AddRange(new object[] { "fast_background", "hidden_cursor", "foreground" });
        _modeCombo.SelectedIndex = 0;
        y += 35;

        var buttonLabel = new Label { Text = "按键:", Location = new Point(20, y + 3), Width = labelWidth };
        _buttonCombo = new ComboBox { Location = new Point(inputLeft, y), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        _buttonCombo.Items.AddRange(new object[] { "left", "right" });
        _buttonCombo.SelectedIndex = 0;
        y += 45;

        var okBtn = new Button { Text = "确定", Location = new Point(80, y), Width = 80, DialogResult = DialogResult.OK };
        okBtn.Click += OkBtn_Click;

        var cancelBtn = new Button { Text = "取消", Location = new Point(170, y), Width = 80, DialogResult = DialogResult.Cancel };

        this.Controls.AddRange(new Control[] {
            aliasLabel, _aliasText,
            xLabel, _xInput,
            yLabel, _yInput,
            modeLabel, _modeCombo,
            buttonLabel, _buttonCombo,
            okBtn, cancelBtn
        });

        this.AcceptButton = okBtn;
        this.CancelButton = cancelBtn;
    }

    private void OkBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_aliasText.Text))
        {
            MessageBox.Show("请输入别名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            this.DialogResult = DialogResult.None;
            return;
        }

        ClickPoint = new ClickPoint
        {
            Alias = _aliasText.Text.Trim(),
            X = (int)_xInput.Value,
            Y = (int)_yInput.Value,
            ClickMode = _modeCombo.SelectedItem?.ToString() ?? "fast_background",
            Button = _buttonCombo.SelectedItem?.ToString() ?? "left"
        };
    }
}

/// <summary>
/// OCR 区域编辑对话框
/// </summary>
public class OcrRegionEditForm : Form
{
    private TextBox _aliasText = null!;
    private NumericUpDown _xInput = null!;
    private NumericUpDown _yInput = null!;
    private NumericUpDown _widthInput = null!;
    private NumericUpDown _heightInput = null!;
    private ComboBox _langCombo = null!;
    private CheckBox _enabledCheckbox = null!;

    public OcrRegion? OcrRegion { get; private set; }

    public OcrRegionEditForm(OcrRegion? existing)
    {
        InitializeComponent();

        if (existing != null)
        {
            _aliasText.Text = existing.Alias;
            _xInput.Value = existing.X;
            _yInput.Value = existing.Y;
            _widthInput.Value = Math.Max(10, existing.Width);
            _heightInput.Value = Math.Max(10, existing.Height);
            _langCombo.SelectedIndex = existing.Language switch
            {
                "zh" => 1,
                "en" => 2,
                _ => 0
            };
            _enabledCheckbox.Checked = existing.Enabled;
        }
    }

    private void InitializeComponent()
    {
        this.Text = "编辑 OCR 区域";
        this.Size = new Size(380, 320);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        int y = 20;
        int labelWidth = 70;
        int inputLeft = 90;

        var aliasLabel = new Label { Text = "别名:", Location = new Point(20, y + 3), Width = labelWidth };
        _aliasText = new TextBox { Location = new Point(inputLeft, y), Width = 200 };
        y += 35;

        var xLabel = new Label { Text = "X:", Location = new Point(20, y + 3), Width = labelWidth };
        _xInput = new NumericUpDown { Location = new Point(inputLeft, y), Width = 100, Minimum = 0, Maximum = 10000 };
        y += 35;

        var yLabel = new Label { Text = "Y:", Location = new Point(20, y + 3), Width = labelWidth };
        _yInput = new NumericUpDown { Location = new Point(inputLeft, y), Width = 100, Minimum = 0, Maximum = 10000 };
        y += 35;

        var widthLabel = new Label { Text = "宽度:", Location = new Point(20, y + 3), Width = labelWidth };
        _widthInput = new NumericUpDown { Location = new Point(inputLeft, y), Width = 100, Minimum = 10, Maximum = 2000, Value = 200 };
        y += 35;

        var heightLabel = new Label { Text = "高度:", Location = new Point(20, y + 3), Width = labelWidth };
        _heightInput = new NumericUpDown { Location = new Point(inputLeft, y), Width = 100, Minimum = 10, Maximum = 2000, Value = 50 };
        y += 35;

        var langLabel = new Label { Text = "语言:", Location = new Point(20, y + 3), Width = labelWidth };
        _langCombo = new ComboBox { Location = new Point(inputLeft, y), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        _langCombo.Items.AddRange(new object[] { "自动", "中文", "英文" });
        _langCombo.SelectedIndex = 0;

        _enabledCheckbox = new CheckBox { Text = "启用", Location = new Point(200, y), Checked = true };
        y += 45;

        var okBtn = new Button { Text = "确定", Location = new Point(100, y), Width = 80, DialogResult = DialogResult.OK };
        okBtn.Click += OkBtn_Click;

        var cancelBtn = new Button { Text = "取消", Location = new Point(190, y), Width = 80, DialogResult = DialogResult.Cancel };

        this.Controls.AddRange(new Control[] {
            aliasLabel, _aliasText,
            xLabel, _xInput,
            yLabel, _yInput,
            widthLabel, _widthInput,
            heightLabel, _heightInput,
            langLabel, _langCombo, _enabledCheckbox,
            okBtn, cancelBtn
        });

        this.AcceptButton = okBtn;
        this.CancelButton = cancelBtn;
    }

    private void OkBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_aliasText.Text))
        {
            MessageBox.Show("请输入别名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            this.DialogResult = DialogResult.None;
            return;
        }

        OcrRegion = new OcrRegion
        {
            Alias = _aliasText.Text.Trim(),
            X = (int)_xInput.Value,
            Y = (int)_yInput.Value,
            Width = (int)_widthInput.Value,
            Height = (int)_heightInput.Value,
            Language = _langCombo.SelectedIndex switch
            {
                1 => "zh",
                2 => "en",
                _ => "auto"
            },
            Enabled = _enabledCheckbox.Checked
        };
    }
}
