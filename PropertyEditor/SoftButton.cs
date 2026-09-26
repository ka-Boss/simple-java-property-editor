namespace PropertyEditor;

sealed class SoftButton : Control
{
    private readonly Color _fill;
    private readonly Color _border;
    private readonly Color _hover;
    private bool _hovering;

    public SoftButton(string text, Color fill, Color border, Color foreground, Color surface, EventHandler click)
    {
        Text = text;
        _fill = fill;
        _border = border;
        _hover = ControlPaint.Light(fill, 0.35f);
        ForeColor = foreground;
        BackColor = surface;
        Font = new Font("Yu Gothic UI", 9F);
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 8, 0);
        var measured = TextRenderer.MeasureText(text, Font);
        Size = new Size(measured.Width + 24, 28);
        Click += click;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovering = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = Rounded(bounds, bounds.Height / 2);
        using var brush = new SolidBrush(_hovering ? _hover : _fill);
        using var pen = new Pen(_border);
        e.Graphics.Clear(BackColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
