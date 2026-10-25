using System.Drawing.Drawing2D;

namespace Vibrance.UI;

/// <summary>Rounded panel that groups related controls.</summary>
internal class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(rect, LogicalToDeviceUnits(12));
        using var fill = new SolidBrush(Theme.Surface);
        using var border = new Pen(Theme.Border);
        g.FillPath(fill, path);
        g.DrawPath(border, path);
    }
}

/// <summary>Card showing what the app is doing right now: a coloured dot, a headline and a detail line.</summary>
internal sealed class StatusCard : Card
{
    private Color _dot = Theme.TextDim;
    private string _title = "";
    private string _detail = "";

    public void SetStatus(Color dot, string title, string detail)
    {
        if (_dot == dot && _title == title && _detail == detail)
            return;
        _dot = dot;
        _title = title;
        _detail = detail;
        AccessibleName = $"{title}. {detail}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pad = LogicalToDeviceUnits(18);
        float dot = LogicalToDeviceUnits(10);
        float cx = pad + dot / 2f;
        float cy = Height / 2f;
        using (var glow = new SolidBrush(Color.FromArgb(55, _dot)))
            g.FillEllipse(glow, cx - dot, cy - dot, dot * 2, dot * 2);
        using (var fill = new SolidBrush(_dot))
            g.FillEllipse(fill, cx - dot / 2f, cy - dot / 2f, dot, dot);

        int x = pad + (int)dot + LogicalToDeviceUnits(14);
        int width = Width - x - pad;
        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, _title, Theme.Status, new Rectangle(x, LogicalToDeviceUnits(7), width, LogicalToDeviceUnits(24)), Theme.Text, flags);
        TextRenderer.DrawText(g, _detail, Theme.Small, new Rectangle(x, LogicalToDeviceUnits(30), width, LogicalToDeviceUnits(18)), Theme.TextMuted, flags);
    }
}
