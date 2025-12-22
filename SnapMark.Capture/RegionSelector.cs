using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnapMark.Capture;

public class RegionSelector : IDisposable
{
    private Form? _overlayForm;
    private Point _startPoint;
    private Point _endPoint;
    private bool _isSelecting = false;
    private bool _disposed = false;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    public event EventHandler<Rectangle>? RegionSelected;

    public void StartSelection()
    {
        _overlayForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Maximized,
            TopMost = true,
            BackColor = Color.Black,
            Opacity = 0.3,
            ShowInTaskbar = false,
            Cursor = Cursors.Cross
        };

        _overlayForm.MouseDown += OverlayForm_MouseDown;
        _overlayForm.MouseMove += OverlayForm_MouseMove;
        _overlayForm.MouseUp += OverlayForm_MouseUp;
        _overlayForm.KeyDown += OverlayForm_KeyDown;
        _overlayForm.Paint += OverlayForm_Paint;

        _overlayForm.Show();
        _overlayForm.Focus();
    }

    private void OverlayForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _startPoint = e.Location;
            _endPoint = e.Location;
            _isSelecting = true;
            _overlayForm?.Invalidate();
        }
    }

    private void OverlayForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            _endPoint = e.Location;
            _overlayForm?.Invalidate();
        }
    }

    private void OverlayForm_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _isSelecting)
        {
            _isSelecting = false;
            var rect = GetSelectionRectangle();
            if (rect.Width > 0 && rect.Height > 0)
            {
                RegionSelected?.Invoke(this, rect);
            }
            Dispose();
        }
    }

    private void OverlayForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Dispose();
        }
    }

    private void OverlayForm_Paint(object? sender, PaintEventArgs e)
    {
        if (_isSelecting)
        {
            var rect = GetSelectionRectangle();
            using var pen = new Pen(Color.White, 2);
            using var brush = new SolidBrush(Color.FromArgb(50, Color.White));
            
            // Draw selection rectangle
            e.Graphics.DrawRectangle(pen, rect);
            
            // Fill selection area with semi-transparent white
            e.Graphics.FillRectangle(brush, rect);
        }
    }

    private Rectangle GetSelectionRectangle()
    {
        int x = Math.Min(_startPoint.X, _endPoint.X);
        int y = Math.Min(_startPoint.Y, _endPoint.Y);
        int width = Math.Abs(_endPoint.X - _startPoint.X);
        int height = Math.Abs(_endPoint.Y - _startPoint.Y);

        return new Rectangle(x, y, width, height);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_overlayForm != null)
            {
                _overlayForm.Close();
                _overlayForm.Dispose();
                _overlayForm = null;
            }
            _disposed = true;
        }
    }
}


