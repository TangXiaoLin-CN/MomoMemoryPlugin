namespace MomoBackend;

/// <summary>
/// 全屏遮挡窗口 - 用于遮挡鼠标移动过程
/// </summary>
public class OverlayForm : Form
{
    public OverlayForm()
    {
        // 无边框全屏
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        this.TopMost = true;
        this.ShowInTaskbar = false;

        // 半透明黑色背景
        this.BackColor = Color.Black;
        this.Opacity = 0.01; // 几乎透明，但足以遮挡
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // WS_EX_NOACTIVATE - 不激活窗口
            cp.ExStyle |= 0x08000000;
            return cp;
        }
    }
}
