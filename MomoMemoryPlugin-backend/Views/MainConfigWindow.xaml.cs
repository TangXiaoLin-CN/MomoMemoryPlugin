using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using MomoBackend.Core;
using MomoBackend.Models;
using MomoBackend.Views.Dialogs;

// Avoid ambiguity between WPF and WinForms
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace MomoBackend.Views;

/// <summary>
/// 主配置窗口 - WPF 版本
/// </summary>
public partial class MainConfigWindow : Window
{
    private readonly ConfigService _configService;
    private readonly WindowManager _windowManager;
    private readonly ScreenshotService _screenshotService;
    private HttpApiService? _httpApiService;

    private WindowInfo? _selectedWindow;
    private bool _ownsHttpApi = false;  // 标记是否由本窗口启动了 HTTP API
    private InitialWindowInfo? _initialWindowInfo;  // 从命令行传递的初始窗口信息

    public MainConfigWindow() : this(null) { }

    public MainConfigWindow(InitialWindowInfo? initialWindowInfo)
    {
        InitializeComponent();

        _initialWindowInfo = initialWindowInfo;
        _configService = new ConfigService();
        _windowManager = new WindowManager();
        _screenshotService = new ScreenshotService();

        // 启动 HTTP API（如果尚未运行）
        StartHttpApiIfNeeded();

        // 加载数据
        LoadWindows();
        LoadConfigToUI();

        // 如果有初始窗口信息，尝试预选窗口
        if (_initialWindowInfo != null)
        {
            TrySelectInitialWindow();
        }

        // 设置配置路径标签
        ConfigPathLabel.Text = $"配置文件: {_configService.ConfigPath}";
    }

    /// <summary>
    /// 检测 HTTP API 是否已经在运行
    /// </summary>
    private bool IsHttpApiRunning()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var response = client.GetAsync("http://localhost:5678/api/status").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void StartHttpApiIfNeeded()
    {
        // 先检测是否已有 API 服务在运行
        if (IsHttpApiRunning())
        {
            Log("检测到 HTTP API 服务已在运行 (由插件启动)");
            return;
        }

        // API 未运行，启动新服务
        try
        {
            _httpApiService = new HttpApiService(5678);
            _httpApiService.OnLog += msg => Dispatcher.Invoke(() => Log($"[API] {msg}"));
            _httpApiService.Start();
            _ownsHttpApi = true;
            Log("HTTP API 服务已启动");
        }
        catch (Exception ex)
        {
            Log($"HTTP API 启动失败: {ex.Message}");
        }
    }

    #region 窗口选择

    private void LoadWindows()
    {
        WindowComboBox.Items.Clear();
        var windows = _windowManager.GetAllWindows();

        foreach (var win in windows.OrderBy(w => w.Title))
        {
            var item = new ComboBoxItem
            {
                Content = FormatWindowTitle(win),
                Tag = win
            };
            WindowComboBox.Items.Add(item);
        }

        Log($"已加载 {windows.Count} 个窗口");
    }

    /// <summary>
    /// 尝试根据初始窗口信息预选窗口
    /// </summary>
    private void TrySelectInitialWindow()
    {
        if (_initialWindowInfo == null) return;

        ComboBoxItem? matchedItem = null;

        // 优先按 HWND 匹配
        if (_initialWindowInfo.Hwnd != 0)
        {
            foreach (ComboBoxItem item in WindowComboBox.Items)
            {
                if (item.Tag is WindowInfo win && win.Hwnd == _initialWindowInfo.Hwnd)
                {
                    matchedItem = item;
                    break;
                }
            }
        }

        // 如果 HWND 没匹配到，按标题和进程名匹配
        if (matchedItem == null && !string.IsNullOrEmpty(_initialWindowInfo.Title))
        {
            foreach (ComboBoxItem item in WindowComboBox.Items)
            {
                if (item.Tag is WindowInfo win)
                {
                    // 完全匹配标题
                    if (win.Title == _initialWindowInfo.Title)
                    {
                        matchedItem = item;
                        break;
                    }
                    // 或者标题包含且进程名匹配
                    if (win.Title.Contains(_initialWindowInfo.Title) &&
                        !string.IsNullOrEmpty(_initialWindowInfo.ProcessName) &&
                        win.ProcessName == _initialWindowInfo.ProcessName)
                    {
                        matchedItem = item;
                        break;
                    }
                }
            }
        }

        // 如果找到匹配项，选中并应用
        if (matchedItem != null)
        {
            WindowComboBox.SelectedItem = matchedItem;
            if (matchedItem.Tag is WindowInfo win)
            {
                _selectedWindow = win;
                TargetTitleText.Text = win.Title;
                TargetProcessText.Text = win.ProcessName;
                Log($"已从插件预选窗口: {win.Title}");
            }
        }
        else if (!string.IsNullOrEmpty(_initialWindowInfo.Title))
        {
            // 即使没找到匹配的窗口，也显示传递的信息
            TargetTitleText.Text = _initialWindowInfo.Title;
            TargetProcessText.Text = _initialWindowInfo.ProcessName;
            Log($"预选窗口未在列表中找到: {_initialWindowInfo.Title} (可能已关闭)");
        }
    }

    private static string FormatWindowTitle(WindowInfo win)
    {
        var title = win.Title.Length > 40 ? win.Title[..40] + "..." : win.Title;
        return $"{title} [{win.ProcessName}]";
    }

    private void RefreshWindows_Click(object sender, RoutedEventArgs e)
    {
        LoadWindows();
    }

    private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowComboBox.SelectedItem is ComboBoxItem item && item.Tag is WindowInfo win)
        {
            _selectedWindow = win;
            TargetTitleText.Text = win.Title;
            TargetProcessText.Text = win.ProcessName;
        }
    }

    private void ApplyWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _configService.Config.TargetWindowTitle = _selectedWindow.Title;
        _configService.Config.TargetProcessName = _selectedWindow.ProcessName;
        Log($"已应用目标窗口: {_selectedWindow.Title}");
        MessageBox.Show("已应用目标窗口配置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region 点击区域管理

    private void RefreshClickPointsList()
    {
        ClickPointsDataGrid.ItemsSource = null;
        ClickPointsDataGrid.ItemsSource = _configService.Config.ClickPoints;
    }

    private void AddPoint_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ClickPointEditDialog(null) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ClickPoint != null)
        {
            _configService.AddClickPoint(dialog.ClickPoint);
            RefreshClickPointsList();
            Log($"已添加点击区域: {dialog.ClickPoint.Alias}");
        }
    }

    private void EditPoint_Click(object sender, RoutedEventArgs e)
    {
        if (ClickPointsDataGrid.SelectedItem is not ClickPoint point)
        {
            MessageBox.Show("请先选择要编辑的点击区域", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new ClickPointEditDialog(point) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ClickPoint != null)
        {
            var index = _configService.Config.ClickPoints.IndexOf(point);
            if (index >= 0)
            {
                _configService.Config.ClickPoints[index] = dialog.ClickPoint;
                RefreshClickPointsList();
                Log($"已编辑点击区域: {dialog.ClickPoint.Alias}");
            }
        }
    }

    private void DeletePoint_Click(object sender, RoutedEventArgs e)
    {
        if (ClickPointsDataGrid.SelectedItem is not ClickPoint point)
        {
            MessageBox.Show("请先选择要删除的点击区域", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show($"确定要删除点击区域 \"{point.Alias}\" 吗？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _configService.Config.ClickPoints.Remove(point);
            RefreshClickPointsList();
            Log($"已删除点击区域: {point.Alias}");
        }
    }

    private void CapturePoint_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bitmap = _screenshotService.CaptureWindow((IntPtr)_selectedWindow.Hwnd);
        if (bitmap == null)
        {
            MessageBox.Show("截图失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // 使用 WinForms 的区域选择器
        using var selector = new RegionSelectorForm(bitmap, offsetX, offsetY);
        var formResult = selector.ShowDialog();

        if (formResult == System.Windows.Forms.DialogResult.OK && selector.HasSelection)
        {
            var region = selector.SelectedRegion;
            int centerX = region.X + region.Width / 2;
            int centerY = region.Y + region.Height / 2;

            var newPoint = new ClickPoint
            {
                X = centerX,
                Y = centerY,
                ClickMode = "fast_background",
                Button = "left"
            };

            var dialog = new ClickPointEditDialog(newPoint) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.ClickPoint != null)
            {
                _configService.AddClickPoint(dialog.ClickPoint);
                RefreshClickPointsList();
                Log($"已捕获点击区域: {dialog.ClickPoint.Alias} ({dialog.ClickPoint.X}, {dialog.ClickPoint.Y})");
            }
        }

        bitmap.Dispose();
    }

    #endregion

    #region OCR 区域管理

    private void RefreshOcrRegionsList()
    {
        // 创建带 LanguageDisplay 属性的视图模型
        var regions = _configService.Config.OcrRegions.Select(r => new OcrRegionViewModel(r)).ToList();
        OcrRegionsDataGrid.ItemsSource = null;
        OcrRegionsDataGrid.ItemsSource = regions;
    }

    private void AddOcrRegion_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OcrRegionEditDialog(null) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.OcrRegion != null)
        {
            _configService.Config.OcrRegions.Add(dialog.OcrRegion);
            RefreshOcrRegionsList();
            Log($"已添加 OCR 区域: {dialog.OcrRegion.Alias}");
        }
    }

    private void EditOcrRegion_Click(object sender, RoutedEventArgs e)
    {
        if (OcrRegionsDataGrid.SelectedItem is not OcrRegionViewModel vm)
        {
            MessageBox.Show("请先选择要编辑的 OCR 区域", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var region = _configService.Config.OcrRegions.FirstOrDefault(r => r.Alias == vm.Alias);
        if (region == null) return;

        var dialog = new OcrRegionEditDialog(region) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.OcrRegion != null)
        {
            var index = _configService.Config.OcrRegions.IndexOf(region);
            if (index >= 0)
            {
                _configService.Config.OcrRegions[index] = dialog.OcrRegion;
                RefreshOcrRegionsList();
                Log($"已编辑 OCR 区域: {dialog.OcrRegion.Alias}");
            }
        }
    }

    private void DeleteOcrRegion_Click(object sender, RoutedEventArgs e)
    {
        if (OcrRegionsDataGrid.SelectedItem is not OcrRegionViewModel vm)
        {
            MessageBox.Show("请先选择要删除的 OCR 区域", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var region = _configService.Config.OcrRegions.FirstOrDefault(r => r.Alias == vm.Alias);
        if (region == null) return;

        var result = MessageBox.Show($"确定要删除 OCR 区域 \"{region.Alias}\" 吗？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _configService.Config.OcrRegions.Remove(region);
            RefreshOcrRegionsList();
            Log($"已删除 OCR 区域: {region.Alias}");
        }
    }

    private void CaptureOcrRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bitmap = _screenshotService.CaptureWindow((IntPtr)_selectedWindow.Hwnd);
        if (bitmap == null)
        {
            MessageBox.Show("截图失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        var formResult = selector.ShowDialog();

        if (formResult == System.Windows.Forms.DialogResult.OK && selector.HasSelection)
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

            var dialog = new OcrRegionEditDialog(newRegion) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.OcrRegion != null)
            {
                _configService.Config.OcrRegions.Add(dialog.OcrRegion);
                RefreshOcrRegionsList();
                Log($"已捕获 OCR 区域: {dialog.OcrRegion.Alias}");
            }
        }

        bitmap.Dispose();
    }

    #endregion

    #region 预览

    private void PreviewRegions_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bitmap = _screenshotService.CaptureWindow((IntPtr)_selectedWindow.Hwnd);
        if (bitmap == null)
        {
            MessageBox.Show("截图失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            // 绘制点击位置
            int pointIndex = 1;
            foreach (var point in _configService.Config.ClickPoints)
            {
                int screenX = point.X + offsetX;
                int screenY = point.Y + offsetY;

                using var pen = new System.Drawing.Pen(System.Drawing.Color.Red, 2);
                g.DrawLine(pen, screenX - 10, screenY, screenX + 10, screenY);
                g.DrawLine(pen, screenX, screenY - 10, screenX, screenY + 10);
                g.DrawEllipse(pen, screenX - 5, screenY - 5, 10, 10);

                using var font = new System.Drawing.Font("Microsoft YaHei", 9, System.Drawing.FontStyle.Bold);
                using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 255, 255, 255));
                using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
                var label = $"{pointIndex}. {point.Alias} ({point.X},{point.Y})";
                var labelSize = g.MeasureString(label, font);
                g.FillRectangle(bgBrush, screenX + 12, screenY - labelSize.Height / 2, labelSize.Width + 4, labelSize.Height);
                g.DrawString(label, font, textBrush, screenX + 14, screenY - labelSize.Height / 2);
                pointIndex++;
            }

            // 绘制 OCR 区域
            var colors = new[] {
                System.Drawing.Color.Blue, System.Drawing.Color.Green,
                System.Drawing.Color.Purple, System.Drawing.Color.Orange, System.Drawing.Color.Teal
            };
            int regionIndex = 0;
            foreach (var region in _configService.Config.OcrRegions)
            {
                if (!region.Enabled) continue;

                var color = colors[regionIndex % colors.Length];
                using var pen = new System.Drawing.Pen(color, 2);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(pen, region.X + offsetX, region.Y + offsetY, region.Width, region.Height);

                using var font = new System.Drawing.Font("Microsoft YaHei", 9, System.Drawing.FontStyle.Bold);
                using var brush = new System.Drawing.SolidBrush(color);
                var label = string.IsNullOrEmpty(region.Alias) ? $"OCR区域{regionIndex + 1}" : region.Alias;
                g.DrawString(label, font, brush, region.X + offsetX, region.Y + offsetY - 18);
                regionIndex++;
            }
        }

        // 显示预览窗口 (使用 WinForms)
        var previewForm = new System.Windows.Forms.Form
        {
            Text = "配置预览 (按ESC关闭)",
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            KeyPreview = true
        };

        var pictureBox = new System.Windows.Forms.PictureBox
        {
            Image = bitmap,
            SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom,
            Dock = System.Windows.Forms.DockStyle.Fill
        };

        previewForm.Controls.Add(pictureBox);
        previewForm.Size = new System.Drawing.Size(
            Math.Min(bitmap.Width + 20, 1200),
            Math.Min(bitmap.Height + 40, 800)
        );

        previewForm.KeyDown += (s, args) =>
        {
            if (args.KeyCode == System.Windows.Forms.Keys.Escape)
                previewForm.Close();
        };

        previewForm.FormClosed += (s, args) => bitmap.Dispose();
        previewForm.Show();
    }

    #endregion

    #region 配置操作

    private void LoadConfigToUI()
    {
        var config = _configService.Config;

        TargetTitleText.Text = config.TargetWindowTitle;
        TargetProcessText.Text = config.TargetProcessName;

        RefreshClickPointsList();
        RefreshOcrRegionsList();

        OcrRefreshIntervalText.Text = (config.OcrRefreshInterval / 1000).ToString();
        OcrAutoRefreshCheck.IsChecked = config.OcrAutoRefresh;
        OcrEngineCombo.SelectedIndex = config.OcrEngine == "windows" ? 1 : 0;

        FbAlphaText.Text = config.FastBackground.WindowAlpha.ToString();
        FbDelayAfterRestoreText.Text = config.FastBackground.DelayAfterRestore.ToString();
        FbDelayBeforeClickText.Text = config.FastBackground.DelayBeforeClick.ToString();
        FbDelayAfterMoveText.Text = config.FastBackground.DelayAfterMove.ToString();
        FbDelayAfterClickText.Text = config.FastBackground.DelayAfterClick.ToString();
        FbDelayBeforeRestoreText.Text = config.FastBackground.DelayBeforeRestore.ToString();
        FbMinimizeAfterClickCheck.IsChecked = config.FastBackground.MinimizeAfterClick;
        FbHideCursorCheck.IsChecked = config.FastBackground.HideCursor;
    }

    private void SaveUIToConfig()
    {
        var config = _configService.Config;

        if (int.TryParse(OcrRefreshIntervalText.Text, out var interval))
            config.OcrRefreshInterval = interval * 1000;
        config.OcrAutoRefresh = OcrAutoRefreshCheck.IsChecked == true;
        config.OcrEngine = OcrEngineCombo.SelectedIndex == 1 ? "windows" : "paddle";

        if (byte.TryParse(FbAlphaText.Text, out var alpha))
            config.FastBackground.WindowAlpha = alpha;
        if (int.TryParse(FbDelayAfterRestoreText.Text, out var dar))
            config.FastBackground.DelayAfterRestore = dar;
        if (int.TryParse(FbDelayBeforeClickText.Text, out var dbc))
            config.FastBackground.DelayBeforeClick = dbc;
        if (int.TryParse(FbDelayAfterMoveText.Text, out var dam))
            config.FastBackground.DelayAfterMove = dam;
        if (int.TryParse(FbDelayAfterClickText.Text, out var dac))
            config.FastBackground.DelayAfterClick = dac;
        if (int.TryParse(FbDelayBeforeRestoreText.Text, out var dbr))
            config.FastBackground.DelayBeforeRestore = dbr;
        config.FastBackground.MinimizeAfterClick = FbMinimizeAfterClickCheck.IsChecked == true;
        config.FastBackground.HideCursor = FbHideCursorCheck.IsChecked == true;
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveUIToConfig();

        if (_configService.Save())
        {
            Log("配置已保存");
            MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("保存失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        _configService.Load();
        LoadConfigToUI();
        Log("配置已重新加载");
        MessageBox.Show("配置已重新加载", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveUIToConfig();

        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件|*.json",
            DefaultExt = "json",
            FileName = "momo-config-export.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, _configService.ExportToJson());
                Log($"配置已导出: {dialog.FileName}");
                MessageBox.Show("配置已导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON 文件|*.json",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                if (_configService.ImportFromJson(json))
                {
                    LoadConfigToUI();
                    Log($"配置已导入: {dialog.FileName}");
                    MessageBox.Show("配置已导入", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("导入失败：无效的配置文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenTestWindow_Click(object sender, RoutedEventArgs e)
    {
        SaveUIToConfig();
        var testWindow = new TestWindow(_configService.Config, _selectedWindow) { Owner = this };
        testWindow.Show();
    }

    #endregion

    #region 日志

    private void Log(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        LogTextBox.AppendText($"[{time}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        // 只有在本窗口启动了 HTTP API 时才关闭它
        if (_ownsHttpApi)
        {
            _httpApiService?.Dispose();
        }
        base.OnClosed(e);
    }
}

/// <summary>
/// OCR 区域视图模型（用于 DataGrid 显示）
/// </summary>
public class OcrRegionViewModel
{
    public string Alias { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string LanguageDisplay { get; set; }
    public bool Enabled { get; set; }

    public OcrRegionViewModel(OcrRegion region)
    {
        Alias = region.Alias;
        X = region.X;
        Y = region.Y;
        Width = region.Width;
        Height = region.Height;
        LanguageDisplay = region.Language switch
        {
            "zh" => "中文",
            "en" => "英文",
            _ => "自动"
        };
        Enabled = region.Enabled;
    }
}
