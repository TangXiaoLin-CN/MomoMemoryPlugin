using System.Windows;
using System.Windows.Controls;
using MomoBackend.Models;

// Avoid ambiguity between WPF and WinForms
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace MomoBackend.Views.Dialogs;

/// <summary>
/// 点击区域编辑对话框 - WPF 版本
/// </summary>
public partial class ClickPointEditDialog : Window
{
    public ClickPoint? ClickPoint { get; private set; }

    public ClickPointEditDialog(ClickPoint? existing)
    {
        InitializeComponent();

        // 设置默认值
        ModeCombo.SelectedIndex = 0;
        ButtonCombo.SelectedIndex = 0;
        AutoRefreshOcrCheck.IsChecked = true;

        if (existing != null)
        {
            AliasText.Text = existing.Alias;
            XInput.Text = existing.X.ToString();
            YInput.Text = existing.Y.ToString();

            // 设置点击模式
            for (int i = 0; i < ModeCombo.Items.Count; i++)
            {
                if ((ModeCombo.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == existing.ClickMode)
                {
                    ModeCombo.SelectedIndex = i;
                    break;
                }
            }

            // 设置按键
            for (int i = 0; i < ButtonCombo.Items.Count; i++)
            {
                if ((ButtonCombo.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == existing.Button)
                {
                    ButtonCombo.SelectedIndex = i;
                    break;
                }
            }

            AutoRefreshOcrCheck.IsChecked = existing.AutoRefreshOcr;
            DelayInput.Text = existing.OcrRefreshDelay.ToString();
        }

        UpdateDelayVisibility();
    }

    private void AutoRefreshOcrCheck_Changed(object sender, RoutedEventArgs e)
    {
        UpdateDelayVisibility();
    }

    private void UpdateDelayVisibility()
    {
        if (DelayPanel != null)
        {
            DelayPanel.Visibility = AutoRefreshOcrCheck.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasText.Text))
        {
            MessageBox.Show("请输入别名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(XInput.Text, out int x) || !int.TryParse(YInput.Text, out int y))
        {
            MessageBox.Show("请输入有效的坐标", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(DelayInput.Text, out int delay))
        {
            delay = 500;
        }

        ClickPoint = new ClickPoint
        {
            Alias = AliasText.Text.Trim(),
            X = x,
            Y = y,
            ClickMode = (ModeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "fast_background",
            Button = (ButtonCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "left",
            AutoRefreshOcr = AutoRefreshOcrCheck.IsChecked == true,
            OcrRefreshDelay = delay
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
