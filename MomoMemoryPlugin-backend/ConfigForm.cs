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

    // OCR 区域1
    private NumericUpDown _ocr1X = null!;
    private NumericUpDown _ocr1Y = null!;
    private NumericUpDown _ocr1Width = null!;
    private NumericUpDown _ocr1Height = null!;
    private ComboBox _ocr1Lang = null!;
    private CheckBox _ocr1Enabled = null!;
    private Button _ocr1SelectBtn = null!;

    // OCR 区域2
    private NumericUpDown _ocr2X = null!;
    private NumericUpDown _ocr2Y = null!;
    private NumericUpDown _ocr2Width = null!;
    private NumericUpDown _ocr2Height = null!;
    private ComboBox _ocr2Lang = null!;
    private CheckBox _ocr2Enabled = null!;
    private Button _ocr2SelectBtn = null!;

    // OCR 自动刷新设置
    private NumericUpDown _ocrRefreshInterval = null!;
    private CheckBox _ocrAutoRefresh = null!;
    private ComboBox _ocrEngineCombo = null!;

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
        this.Size = new Size(750, 700);
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
            Size = new Size(715, 140)
        };

        // OCR 区域1
        var ocr1Label = new Label { Text = "区域1:", Location = new Point(10, 25), Width = 50 };
        _ocr1Enabled = new CheckBox { Text = "启用", Location = new Point(60, 23), Checked = true, Width = 55 };

        var ocr1XLabel = new Label { Text = "X:", Location = new Point(120, 25), Width = 20 };
        _ocr1X = new NumericUpDown { Location = new Point(140, 22), Width = 60, Minimum = 0, Maximum = 5000 };

        var ocr1YLabel = new Label { Text = "Y:", Location = new Point(210, 25), Width = 20 };
        _ocr1Y = new NumericUpDown { Location = new Point(230, 22), Width = 60, Minimum = 0, Maximum = 5000 };

        var ocr1WLabel = new Label { Text = "宽:", Location = new Point(300, 25), Width = 25 };
        _ocr1Width = new NumericUpDown { Location = new Point(325, 22), Width = 60, Minimum = 10, Maximum = 2000, Value = 200 };

        var ocr1HLabel = new Label { Text = "高:", Location = new Point(395, 25), Width = 25 };
        _ocr1Height = new NumericUpDown { Location = new Point(420, 22), Width = 60, Minimum = 10, Maximum = 2000, Value = 50 };

        var ocr1LangLabel = new Label { Text = "语言:", Location = new Point(490, 25), Width = 35 };
        _ocr1Lang = new ComboBox { Location = new Point(525, 22), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        _ocr1Lang.Items.AddRange(new object[] { "自动", "中文", "英文" });
        _ocr1Lang.SelectedIndex = 0;

        _ocr1SelectBtn = new Button { Text = "框选", Location = new Point(605, 20), Width = 50, BackColor = Color.LightYellow };
        _ocr1SelectBtn.Click += Ocr1Select_Click;

        // OCR 区域2
        var ocr2Label = new Label { Text = "区域2:", Location = new Point(10, 60), Width = 50 };
        _ocr2Enabled = new CheckBox { Text = "启用", Location = new Point(60, 58), Checked = false, Width = 55 };

        var ocr2XLabel = new Label { Text = "X:", Location = new Point(120, 60), Width = 20 };
        _ocr2X = new NumericUpDown { Location = new Point(140, 57), Width = 60, Minimum = 0, Maximum = 5000 };

        var ocr2YLabel = new Label { Text = "Y:", Location = new Point(210, 60), Width = 20 };
        _ocr2Y = new NumericUpDown { Location = new Point(230, 57), Width = 60, Minimum = 0, Maximum = 5000, Value = 50 };

        var ocr2WLabel = new Label { Text = "宽:", Location = new Point(300, 60), Width = 25 };
        _ocr2Width = new NumericUpDown { Location = new Point(325, 57), Width = 60, Minimum = 10, Maximum = 2000, Value = 200 };

        var ocr2HLabel = new Label { Text = "高:", Location = new Point(395, 60), Width = 25 };
        _ocr2Height = new NumericUpDown { Location = new Point(420, 57), Width = 60, Minimum = 10, Maximum = 2000, Value = 50 };

        var ocr2LangLabel = new Label { Text = "语言:", Location = new Point(490, 60), Width = 35 };
        _ocr2Lang = new ComboBox { Location = new Point(525, 57), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        _ocr2Lang.Items.AddRange(new object[] { "自动", "中文", "英文" });
        _ocr2Lang.SelectedIndex = 0;

        _ocr2SelectBtn = new Button { Text = "框选", Location = new Point(605, 55), Width = 50, BackColor = Color.LightYellow };
        _ocr2SelectBtn.Click += Ocr2Select_Click;

        // OCR 刷新设置
        var refreshLabel = new Label { Text = "间隔(秒):", Location = new Point(10, 100), Width = 60 };
        _ocrRefreshInterval = new NumericUpDown { Location = new Point(70, 97), Width = 50, Minimum = 1, Maximum = 60, Value = 3 };
        _ocrAutoRefresh = new CheckBox { Text = "自动刷新", Location = new Point(130, 98), Width = 80 };

        var engineLabel = new Label { Text = "OCR引擎:", Location = new Point(220, 100), Width = 60 };
        _ocrEngineCombo = new ComboBox { Location = new Point(280, 97), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _ocrEngineCombo.Items.AddRange(new object[] { "PaddleOCR (推荐)", "Windows OCR" });
        _ocrEngineCombo.SelectedIndex = 0;

        var previewOcrBtn = new Button { Text = "预览OCR区域", Location = new Point(450, 95), Width = 100, BackColor = Color.LightCyan };
        previewOcrBtn.Click += PreviewOcrRegions_Click;

        ocrGroup.Controls.AddRange(new Control[] {
            ocr1Label, _ocr1Enabled, ocr1XLabel, _ocr1X, ocr1YLabel, _ocr1Y,
            ocr1WLabel, _ocr1Width, ocr1HLabel, _ocr1Height, ocr1LangLabel, _ocr1Lang, _ocr1SelectBtn,
            ocr2Label, _ocr2Enabled, ocr2XLabel, _ocr2X, ocr2YLabel, _ocr2Y,
            ocr2WLabel, _ocr2Width, ocr2HLabel, _ocr2Height, ocr2LangLabel, _ocr2Lang, _ocr2SelectBtn,
            refreshLabel, _ocrRefreshInterval, _ocrAutoRefresh, engineLabel, _ocrEngineCombo, previewOcrBtn
        });
        this.Controls.Add(ocrGroup);
        y += 150;

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

        // 加载 OCR 区域1
        _ocr1X.Value = config.OcrRegion1.X;
        _ocr1Y.Value = config.OcrRegion1.Y;
        _ocr1Width.Value = Math.Max(10, config.OcrRegion1.Width);
        _ocr1Height.Value = Math.Max(10, config.OcrRegion1.Height);
        _ocr1Enabled.Checked = config.OcrRegion1.Enabled;
        _ocr1Lang.SelectedIndex = config.OcrRegion1.Language switch
        {
            "zh" => 1,
            "en" => 2,
            _ => 0
        };

        // 加载 OCR 区域2
        _ocr2X.Value = config.OcrRegion2.X;
        _ocr2Y.Value = config.OcrRegion2.Y;
        _ocr2Width.Value = Math.Max(10, config.OcrRegion2.Width);
        _ocr2Height.Value = Math.Max(10, config.OcrRegion2.Height);
        _ocr2Enabled.Checked = config.OcrRegion2.Enabled;
        _ocr2Lang.SelectedIndex = config.OcrRegion2.Language switch
        {
            "zh" => 1,
            "en" => 2,
            _ => 0
        };

        // 加载 OCR 自动刷新设置
        _ocrRefreshInterval.Value = Math.Max(1, Math.Min(60, config.OcrRefreshInterval / 1000));
        _ocrAutoRefresh.Checked = config.OcrAutoRefresh;

        // 加载 OCR 引擎选择
        _ocrEngineCombo.SelectedIndex = config.OcrEngine == "windows" ? 1 : 0;
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

            // 绘制 OCR 区域1
            if (_ocr1Enabled.Checked)
            {
                using var pen = new Pen(Color.Blue, 2);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(pen,
                    (int)_ocr1X.Value + offsetX,
                    (int)_ocr1Y.Value + offsetY,
                    (int)_ocr1Width.Value,
                    (int)_ocr1Height.Value);
                g.DrawString("OCR区域1", this.Font, Brushes.Blue,
                    (int)_ocr1X.Value + offsetX,
                    (int)_ocr1Y.Value + offsetY - 15);
            }

            // 绘制 OCR 区域2
            if (_ocr2Enabled.Checked)
            {
                using var pen = new Pen(Color.Green, 2);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(pen,
                    (int)_ocr2X.Value + offsetX,
                    (int)_ocr2Y.Value + offsetY,
                    (int)_ocr2Width.Value,
                    (int)_ocr2Height.Value);
                g.DrawString("OCR区域2", this.Font, Brushes.Green,
                    (int)_ocr2X.Value + offsetX,
                    (int)_ocr2Y.Value + offsetY - 15);
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

    private void PreviewOcrRegions_Click(object? sender, EventArgs e)
    {
        PreviewPoints_Click(sender, e);
    }

    private void SelectOcrRegion(int regionNum)
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

            if (regionNum == 1)
            {
                _ocr1X.Value = Math.Max(0, region.X);
                _ocr1Y.Value = Math.Max(0, region.Y);
                _ocr1Width.Value = Math.Max(10, region.Width);
                _ocr1Height.Value = Math.Max(10, region.Height);
                _ocr1Enabled.Checked = true;
            }
            else
            {
                _ocr2X.Value = Math.Max(0, region.X);
                _ocr2Y.Value = Math.Max(0, region.Y);
                _ocr2Width.Value = Math.Max(10, region.Width);
                _ocr2Height.Value = Math.Max(10, region.Height);
                _ocr2Enabled.Checked = true;
            }
        }

        bitmap.Dispose();
    }

    private void Ocr1Select_Click(object? sender, EventArgs e) => SelectOcrRegion(1);
    private void Ocr2Select_Click(object? sender, EventArgs e) => SelectOcrRegion(2);

    private void SaveUIToConfig()
    {
        var config = _configService.Config;

        // 保存 OCR 区域1
        config.OcrRegion1.X = (int)_ocr1X.Value;
        config.OcrRegion1.Y = (int)_ocr1Y.Value;
        config.OcrRegion1.Width = (int)_ocr1Width.Value;
        config.OcrRegion1.Height = (int)_ocr1Height.Value;
        config.OcrRegion1.Enabled = _ocr1Enabled.Checked;
        config.OcrRegion1.Language = _ocr1Lang.SelectedIndex switch
        {
            1 => "zh",
            2 => "en",
            _ => "auto"
        };

        // 保存 OCR 区域2
        config.OcrRegion2.X = (int)_ocr2X.Value;
        config.OcrRegion2.Y = (int)_ocr2Y.Value;
        config.OcrRegion2.Width = (int)_ocr2Width.Value;
        config.OcrRegion2.Height = (int)_ocr2Height.Value;
        config.OcrRegion2.Enabled = _ocr2Enabled.Checked;
        config.OcrRegion2.Language = _ocr2Lang.SelectedIndex switch
        {
            1 => "zh",
            2 => "en",
            _ => "auto"
        };

        // 保存 OCR 自动刷新设置
        config.OcrRefreshInterval = (int)_ocrRefreshInterval.Value * 1000;
        config.OcrAutoRefresh = _ocrAutoRefresh.Checked;

        // 保存 OCR 引擎选择
        config.OcrEngine = _ocrEngineCombo.SelectedIndex == 1 ? "windows" : "paddle";
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
