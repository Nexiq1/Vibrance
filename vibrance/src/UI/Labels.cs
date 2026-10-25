using System.Drawing.Drawing2D;

namespace Vibrance.UI;

/// <summary>App name in the brand gradient with a muted subtitle under it.</summary>
internal sealed class HeaderControl : Control
{
    private string _subtitle = "";

    public HeaderControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Background;
        Text = "Vibrance";
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            if (_subtitle == value)
                return;
            _subtitle = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Drawn as a path so the gradient fills the glyphs themselves.
        float emPixels = Theme.Title.SizeInPoints * DeviceDpi / 72f;
        using var path = new GraphicsPath();
        path.AddString(Text, Theme.Title.FontFamily, (int)Theme.Title.Style, emPixels, new PointF(0, 0), StringFormat.GenericTypographic);
        var bounds = path.GetBounds();
        using (var shift = new Matrix())
        {
            shift.Translate(LogicalToDeviceUnits(1) - bounds.X, LogicalToDeviceUnits(4) - bounds.Y);
            path.Transform(shift);
        }
        bounds = path.GetBounds();

        using (var brush = new LinearGradientBrush(new RectangleF(bounds.X - 1, bounds.Y, bounds.Width + 2, bounds.Height), Color.Black, Color.Black, LinearGradientMode.Horizontal))
        {
            brush.InterpolationColors = Theme.BrandBlend();
            g.FillPath(brush, path);
        }

        var subtitleRect = new Rectangle(0, (int)bounds.Bottom + LogicalToDeviceUnits(8), Width, LogicalToDeviceUnits(18));
        TextRenderer.DrawText(g, _subtitle, Theme.Small, subtitleRect, Theme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>A heading with an optional muted hint right after it on the same line.</summary>
internal sealed class SectionTitle : Control
{
    private string _hint = "";

    public SectionTitle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = Theme.Heading;
    }

    public string Hint
    {
        get => _hint;
        set
        {
            if (_hint == value)
                return;
            _hint = value;
            Invalidate();
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        Size titleSize = TextRenderer.MeasureText(g, Text, Font, Size, flags | TextFormatFlags.NoPadding);
        int titleWidth = Math.Min(titleSize.Width, Width);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, titleWidth + LogicalToDeviceUnits(2), Height), ForeColor, flags | TextFormatFlags.NoPadding);

        if (_hint.Length > 0)
        {
            int x = titleWidth + LogicalToDeviceUnits(8);
            TextRenderer.DrawText(g, _hint, Theme.Small, new Rectangle(x, LogicalToDeviceUnits(1), Math.Max(0, Width - x), Height), Theme.TextMuted, flags | TextFormatFlags.NoPadding);
        }
    }
}
