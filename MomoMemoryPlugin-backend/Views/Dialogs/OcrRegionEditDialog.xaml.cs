using System.Windows;
using MomoBackend.Models;

// Avoid ambiguity between WPF and WinForms
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace MomoBackend.Views.Dialogs;

/// <summary>
/// OCR 区域编辑对话框 - WPF 版本
/// </summary>
public partial class OcrRegionEditDialog : Window
{
    public OcrRegion? OcrRegion { get; private set; }

    public OcrRegionEditDialog(OcrRegion? existing)
    {
        InitializeComponent();

        // 设置默认值
        WidthInput.Text = "200";
        HeightInput.Text = "50";

        if (existing != null)
        {
            AliasText.Text = existing.Alias;
            XInput.Text = existing.X.ToString();
            YInput.Text = existing.Y.ToString();
            WidthInput.Text = Math.Max(10, existing.Width).ToString();
            HeightInput.Text = Math.Max(10, existing.Height).ToString();

            LangCombo.SelectedIndex = existing.Language switch
            {
                "zh" => 1,
                "en" => 2,
                _ => 0
            };

            EnabledCheck.IsChecked = existing.Enabled;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasText.Text))
        {
            MessageBox.Show("请输入别名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(XInput.Text, out int x) ||
            !int.TryParse(YInput.Text, out int y) ||
            !int.TryParse(WidthInput.Text, out int width) ||
            !int.TryParse(HeightInput.Text, out int height))
        {
            MessageBox.Show("请输入有效的数值", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (width < 10 || height < 10)
        {
            MessageBox.Show("宽度和高度至少为10", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        OcrRegion = new OcrRegion
        {
            Alias = AliasText.Text.Trim(),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Language = LangCombo.SelectedIndex switch
            {
                1 => "zh",
                2 => "en",
                _ => "auto"
            },
            Enabled = EnabledCheck.IsChecked == true
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
