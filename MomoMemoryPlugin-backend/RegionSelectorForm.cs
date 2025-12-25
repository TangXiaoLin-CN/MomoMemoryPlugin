using System.Drawing;
using System.Drawing.Drawing2D;

namespace MomoBackend;

/// <summary>
/// 区域选择器 - 允许用户在截图上框选区域
/// </summary>
public class RegionSelectorForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly int _offsetX;  // 客户区偏移
    private readonly int _offsetY;

    private Point _startPoint;
    private Point _currentPoint;
    private bool _isSelecting;
    private Rectangle _selectedRegion;

    /// <summary>
    /// 选择的区域（客户区坐标）
    /// </summary>
    public Rectangle SelectedRegion => _selectedRegion;

    /// <summary>
    /// 是否已选择区域
    /// </summary>
    public bool HasSelection { get; private set; }

    public RegionSelectorForm(Bitmap screenshot, int clientOffsetX, int clientOffsetY)
    {
        _screenshot = screenshot;
        _offsetX = clientOffsetX;
        _offsetY = clientOffsetY;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "框选识别区域 (拖动鼠标框选，ESC取消，回车确认)";
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.DoubleBuffered = true;
        this.KeyPreview = true;

        // 设置窗口大小（限制最大尺寸）
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        int maxWidth = Math.Min(_screenshot.Width + 20, workingArea.Width - 100);
        int maxHeight = Math.Min(_screenshot.Height + 60, workingArea.Height - 100);
        this.Size = new Size(maxWidth, maxHeight);

        // 创建可滚动的图片容器
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        var pictureBox = new PictureBox
        {
            Image = _screenshot,
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(0, 0)
        };

        // 绑定鼠标事件
        pictureBox.MouseDown += PictureBox_MouseDown;
        pictureBox.MouseMove += PictureBox_MouseMove;
        pictureBox.MouseUp += PictureBox_MouseUp;
        pictureBox.Paint += PictureBox_Paint;

        panel.Controls.Add(pictureBox);
        this.Controls.Add(panel);

        // 添加提示标签
        var tipLabel = new Label
        {
            Text = "提示：拖动鼠标框选区域 | 回车确认 | ESC取消 | 右键重新选择",
            Dock = DockStyle.Bottom,
            Height = 25,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(240, 240, 240)
        };
        this.Controls.Add(tipLabel);

        // 键盘事件
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                HasSelection = false;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
            else if (e.KeyCode == Keys.Enter && _selectedRegion.Width > 0 && _selectedRegion.Height > 0)
            {
                HasSelection = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        };
    }

    private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isSelecting = true;
            _startPoint = e.Location;
            _currentPoint = e.Location;
            _selectedRegion = Rectangle.Empty;
        }
        else if (e.Button == MouseButtons.Right)
        {
            // 右键重置选择
            _isSelecting = false;
            _selectedRegion = Rectangle.Empty;
            (sender as Control)?.Invalidate();
        }
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            _currentPoint = e.Location;
            UpdateSelectedRegion();
            (sender as Control)?.Invalidate();
        }
    }

    private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _isSelecting)
        {
            _isSelecting = false;
            _currentPoint = e.Location;
            UpdateSelectedRegion();
            (sender as Control)?.Invalidate();
        }
    }

    private void UpdateSelectedRegion()
    {
        int x = Math.Min(_startPoint.X, _currentPoint.X);
        int y = Math.Min(_startPoint.Y, _currentPoint.Y);
        int width = Math.Abs(_currentPoint.X - _startPoint.X);
        int height = Math.Abs(_currentPoint.Y - _startPoint.Y);

        // 转换为客户区坐标
        _selectedRegion = new Rectangle(
            x - _offsetX,
            y - _offsetY,
            width,
            height
        );
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        if (_selectedRegion.Width > 0 && _selectedRegion.Height > 0)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 绘制半透明遮罩（选区外部）
            using var dimBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0));

            // 计算屏幕坐标的选区
            var screenRect = new Rectangle(
                _selectedRegion.X + _offsetX,
                _selectedRegion.Y + _offsetY,
                _selectedRegion.Width,
                _selectedRegion.Height
            );

            // 遮罩上方
            g.FillRectangle(dimBrush, 0, 0, _screenshot.Width, screenRect.Top);
            // 遮罩下方
            g.FillRectangle(dimBrush, 0, screenRect.Bottom, _screenshot.Width, _screenshot.Height - screenRect.Bottom);
            // 遮罩左边
            g.FillRectangle(dimBrush, 0, screenRect.Top, screenRect.Left, screenRect.Height);
            // 遮罩右边
            g.FillRectangle(dimBrush, screenRect.Right, screenRect.Top, _screenshot.Width - screenRect.Right, screenRect.Height);

            // 绘制选区边框
            using var pen = new Pen(Color.Red, 2);
            pen.DashStyle = DashStyle.Dash;
            g.DrawRectangle(pen, screenRect);

            // 绘制尺寸信息
            var sizeText = $"{_selectedRegion.Width} x {_selectedRegion.Height}";
            var coordText = $"({_selectedRegion.X}, {_selectedRegion.Y})";

            using var font = new Font("Microsoft YaHei", 10, FontStyle.Bold);
            using var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.White);

            // 尺寸标签
            var sizeSize = g.MeasureString(sizeText, font);
            var sizeRect = new RectangleF(
                screenRect.X + (screenRect.Width - sizeSize.Width) / 2,
                screenRect.Y + (screenRect.Height - sizeSize.Height) / 2,
                sizeSize.Width + 10,
                sizeSize.Height + 4
            );
            g.FillRectangle(bgBrush, sizeRect);
            g.DrawString(sizeText, font, textBrush, sizeRect.X + 5, sizeRect.Y + 2);

            // 坐标标签
            var coordSize = g.MeasureString(coordText, font);
            var coordRect = new RectangleF(
                screenRect.X,
                screenRect.Y - coordSize.Height - 6,
                coordSize.Width + 10,
                coordSize.Height + 4
            );
            if (coordRect.Y < 0) coordRect.Y = screenRect.Bottom + 2;
            g.FillRectangle(bgBrush, coordRect);
            g.DrawString(coordText, font, textBrush, coordRect.X + 5, coordRect.Y + 2);
        }

        // 绘制十字线（跟随鼠标）
        if (_isSelecting)
        {
            using var crossPen = new Pen(Color.FromArgb(150, 255, 255, 0), 1);
            crossPen.DashStyle = DashStyle.Dot;
            e.Graphics.DrawLine(crossPen, _currentPoint.X, 0, _currentPoint.X, _screenshot.Height);
            e.Graphics.DrawLine(crossPen, 0, _currentPoint.Y, _screenshot.Width, _currentPoint.Y);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
    }
}
