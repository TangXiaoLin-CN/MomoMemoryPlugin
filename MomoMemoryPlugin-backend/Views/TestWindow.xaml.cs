using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MomoBackend.Core;
using MomoBackend.Models;

// Avoid ambiguity between WPF and WinForms
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using Rectangle = System.Drawing.Rectangle;
using Button = System.Windows.Controls.Button;

namespace MomoBackend.Views;

/// <summary>
/// 测试窗口 - WPF 版本
/// 合并了 MainForm 和 TestForm 的功能
/// </summary>
public partial class TestWindow : Window
{
    private readonly AppConfig _config;
    private readonly WindowInfo? _selectedWindow;
    private readonly WindowManager _windowManager;
    private readonly MouseController _mouseController;
    private readonly ScreenshotService _screenshotService;
    private readonly OcrService _windowsOcrService;

    private readonly List<ClickTestItem> _clickTestItems = new();
    private readonly List<OcrTestItem> _ocrTestItems = new();

    private DispatcherTimer? _mouseTimer;
    private DispatcherTimer? _ocrTimer;

    public TestWindow(AppConfig config, WindowInfo? selectedWindow)
    {
        InitializeComponent();

        _config = config;
        _selectedWindow = selectedWindow;
        _windowManager = new WindowManager();
        _mouseController = new MouseController();
        _screenshotService = new ScreenshotService();
        _windowsOcrService = new OcrService();

        // 设置窗口信息
        if (_selectedWindow != null)
        {
            WindowInfoLabel.Text = $"目标窗口: {_selectedWindow.Title} [{_selectedWindow.ProcessName}]";
            WindowInfoLabel.Foreground = System.Windows.Media.Brushes.Green;
        }
        else
        {
            WindowInfoLabel.Text = "未选择目标窗口";
            WindowInfoLabel.Foreground = System.Windows.Media.Brushes.Red;
        }

        // 加载测试数据
        LoadClickPoints();
        LoadOcrRegions();

        // 设置自动刷新
        RefreshIntervalLabel.Text = $"({_config.OcrRefreshInterval / 1000}秒)";
        AutoRefreshCheck.IsChecked = _config.OcrAutoRefresh;

        // 启动鼠标位置追踪
        StartMouseTracking();

        // 如果配置了自动刷新，启动定时器
        if (_config.OcrAutoRefresh)
        {
            StartAutoRefresh();
        }
    }

    #region 鼠标追踪

    private void StartMouseTracking()
    {
        _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _mouseTimer.Tick += (s, e) => UpdateMousePosition();
        _mouseTimer.Start();
    }

    private void UpdateMousePosition()
    {
        var pos = _mouseController.GetCursorPosition();
        string relativeText = "";

        if (_selectedWindow != null)
        {
            var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
            if (clientOrigin != null)
            {
                int relX = pos.X - clientOrigin.Value.X;
                int relY = pos.Y - clientOrigin.Value.Y;
                relativeText = $" (客户区: X: {relX}, Y: {relY})";
            }
        }

        MousePositionLabel.Text = $"鼠标: X: {pos.X}, Y: {pos.Y}{relativeText}";
    }

    #endregion

    #region 手动点击测试

    private async void CapturePosition_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        Log("3秒后捕获鼠标位置，请移动鼠标到目标位置...");

        for (int i = 3; i > 0; i--)
        {
            if (button != null) button.Content = $"捕获中... {i}";
            await Task.Delay(1000);
        }

        var pos = _mouseController.GetCursorPosition();
        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);

        if (clientOrigin != null)
        {
            int relX = pos.X - clientOrigin.Value.X;
            int relY = pos.Y - clientOrigin.Value.Y;
            XInput.Text = relX.ToString();
            YInput.Text = relY.ToString();
            Log($"已捕获客户区坐标: X={relX}, Y={relY}");
        }

        if (button != null)
        {
            button.Content = "捕获鼠标位置 (3秒后)";
            button.IsEnabled = true;
        }
    }

    private void ExecuteClick_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_windowManager.IsWindowValid((IntPtr)_selectedWindow.Hwnd))
        {
            MessageBox.Show("目标窗口已失效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (clientOrigin == null)
        {
            MessageBox.Show("无法获取窗口客户区位置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!int.TryParse(XInput.Text, out int x) || !int.TryParse(YInput.Text, out int y))
        {
            MessageBox.Show("请输入有效的坐标", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string mode = ((ModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "").Split(' ')[0];
        string button = ((ButtonComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "").Split(' ')[0];

        Log($"执行点击: 模式={mode}, 客户区坐标=({x}, {y}), 按键={button}");

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

    #endregion

    #region 配置点击测试

    private void LoadClickPoints()
    {
        _clickTestItems.Clear();
        foreach (var point in _config.ClickPoints)
        {
            _clickTestItems.Add(new ClickTestItem(point));
        }
        ClickTestDataGrid.ItemsSource = null;
        ClickTestDataGrid.ItemsSource = _clickTestItems;
    }

    private void ClickTestDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TestSelectedClick_Click(sender, e);
    }

    private void TestSelectedClick_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        if (ClickTestDataGrid.SelectedItem is not ClickTestItem item)
        {
            MessageBox.Show("请先选择要测试的点击区域", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TestClickPoint(item);
    }

    private async void TestAllClicks_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        Log("开始测试所有点击区域...");

        foreach (var item in _clickTestItems)
        {
            TestClickPoint(item);
            await Task.Delay(500);
        }

        Log("所有点击区域测试完成");

        if (button != null) button.IsEnabled = true;
    }

    private void TestClickPoint(ClickTestItem item)
    {
        if (_selectedWindow == null) return;

        if (!_windowManager.IsWindowValid((IntPtr)_selectedWindow.Hwnd))
        {
            Log($"错误: 目标窗口已失效");
            item.Status = "窗口失效";
            RefreshClickTestList();
            return;
        }

        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)_selectedWindow.Hwnd);
        if (clientOrigin == null)
        {
            Log($"错误: 无法获取窗口客户区");
            item.Status = "获取失败";
            RefreshClickTestList();
            return;
        }

        Log($"测试点击: {item.Alias} ({item.Point.X}, {item.Point.Y}) 模式={item.ClickMode}");

        var result = _mouseController.Click(
            (IntPtr)_selectedWindow.Hwnd,
            item.Point.X, item.Point.Y,
            clientOrigin.Value.X, clientOrigin.Value.Y,
            item.Point.ClickMode, item.Point.Button
        );

        if (result.Success)
        {
            item.Status = "成功";
            Log($"  ✓ {item.Alias}: 点击成功");
        }
        else
        {
            item.Status = "失败";
            Log($"  ✗ {item.Alias}: {result.Message}");
        }

        RefreshClickTestList();
    }

    private void RefreshClickTestList()
    {
        ClickTestDataGrid.ItemsSource = null;
        ClickTestDataGrid.ItemsSource = _clickTestItems;
    }

    #endregion

    #region OCR 测试

    private void LoadOcrRegions()
    {
        _ocrTestItems.Clear();
        foreach (var region in _config.OcrRegions)
        {
            if (region.Enabled)
            {
                _ocrTestItems.Add(new OcrTestItem(region));
            }
        }
        OcrTestDataGrid.ItemsSource = null;
        OcrTestDataGrid.ItemsSource = _ocrTestItems;
    }

    private void OcrTestDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TestSelectedOcr_Click(sender, e);
    }

    private async void TestSelectedOcr_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        if (OcrTestDataGrid.SelectedItem is not OcrTestItem item)
        {
            MessageBox.Show("请先选择要测试的 OCR 区域", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await TestOcrRegion(item);
    }

    private async void TestAllOcr_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            Log("错误: 未选择目标窗口");
            return;
        }

        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        foreach (var item in _ocrTestItems)
        {
            await TestOcrRegion(item);
        }

        if (button != null) button.IsEnabled = true;
    }

    private async Task TestOcrRegion(OcrTestItem item)
    {
        if (_selectedWindow == null) return;

        item.Status = "识别中...";
        RefreshOcrTestList();

        var engineName = _config.OcrEngine == "paddle" ? "PaddleOCR" : "Windows OCR";
        Log($"识别 {item.Alias}: {item.RegionDisplay} [{engineName}]");

        var rect = new Rectangle(item.Region.X, item.Region.Y, item.Region.Width, item.Region.Height);
        var bitmap = _screenshotService.CaptureRegion((IntPtr)_selectedWindow.Hwnd, rect);

        if (bitmap == null)
        {
            item.Status = "截图失败";
            item.Result = "[截图失败]";
            Log($"{item.Alias}: 截图失败");
            RefreshOcrTestList();
            return;
        }

        try
        {
            var langCode = item.Region.Language switch
            {
                "zh" => "zh",
                "en" => "en",
                _ => "auto"
            };

            OcrResult result;
            if (_config.OcrEngine == "paddle")
            {
                result = await PaddleOcrService.Instance.RecognizeAsync(bitmap, langCode);
            }
            else
            {
                result = await _windowsOcrService.RecognizeAsync(bitmap, langCode);
            }

            if (result.Success)
            {
                item.Result = result.Text;
                item.Status = "成功";
                Log($"{item.Alias}: {(string.IsNullOrEmpty(result.Text) ? "[无文字]" : result.Text)}");
            }
            else
            {
                item.Result = $"[识别失败: {result.ErrorMessage}]";
                item.Status = "失败";
                Log($"{item.Alias}: 识别失败 - {result.ErrorMessage}");
            }
        }
        finally
        {
            bitmap.Dispose();
        }

        RefreshOcrTestList();
    }

    private void RefreshOcrTestList()
    {
        OcrTestDataGrid.ItemsSource = null;
        OcrTestDataGrid.ItemsSource = _ocrTestItems;
    }

    #endregion

    #region 自动刷新

    private void AutoRefreshCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshCheck.IsChecked == true)
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
        StopAutoRefresh();

        _ocrTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_config.OcrRefreshInterval)
        };
        _ocrTimer.Tick += async (s, e) =>
        {
            if (_selectedWindow != null)
            {
                foreach (var item in _ocrTestItems)
                {
                    await TestOcrRegion(item);
                }
            }
        };
        _ocrTimer.Start();
        Log($"自动刷新已启动，间隔 {_config.OcrRefreshInterval / 1000} 秒");
    }

    private void StopAutoRefresh()
    {
        if (_ocrTimer != null)
        {
            _ocrTimer.Stop();
            _ocrTimer = null;
            Log("自动刷新已停止");
        }
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
        _mouseTimer?.Stop();
        StopAutoRefresh();
        base.OnClosed(e);
    }
}

/// <summary>
/// 点击测试项
/// </summary>
public class ClickTestItem
{
    public ClickPoint Point { get; }
    public string Alias => Point.Alias;
    public string CoordDisplay => $"({Point.X}, {Point.Y})";
    public string ClickMode => Point.ClickMode;
    public string Status { get; set; } = "待测试";

    public ClickTestItem(ClickPoint point)
    {
        Point = point;
    }
}

/// <summary>
/// OCR 测试项
/// </summary>
public class OcrTestItem
{
    public OcrRegion Region { get; }
    public string Alias => Region.Alias;
    public string RegionDisplay => $"({Region.X},{Region.Y}) {Region.Width}x{Region.Height}";
    public string Result { get; set; } = "";
    public string Status { get; set; } = "待识别";

    public OcrTestItem(OcrRegion region)
    {
        Region = region;
    }
}
