using System.Drawing.Drawing2D;

namespace Vibrance.UI;

/// <summary>Owner-drawn list box with dark scrollbars, DPI-aware row height and a message when empty.</summary>
internal class DarkListBox : ListBox
{
    private const int WM_PAINT = 0x000F;
    private readonly int _logicalItemHeight;

    public DarkListBox(int itemHeight)
    {
        _logicalItemHeight = itemHeight;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = itemHeight;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
    }

    public string EmptyText { get; set; } = "";

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = LogicalToDeviceUnits(_logicalItemHeight);
        Theme.DarkScrollbars(this);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ItemHeight = LogicalToDeviceUnits(_logicalItemHeight);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_PAINT && Items.Count == 0 && EmptyText.Length > 0)
        {
            using var g = CreateGraphics();
            var rect = Rectangle.Inflate(ClientRectangle, -LogicalToDeviceUnits(24), 0);
            TextRenderer.DrawText(g, EmptyText, Theme.Body, rect, Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Items.Count == 0)
            Invalidate();
    }

    /// <summary>Paints the row background (rounded highlight when selected) and returns whether the row is selected.</summary>
    protected bool DrawRowBackground(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var background = new SolidBrush(BackColor))
            g.FillRectangle(background, e.Bounds);

        if (selected)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
            Theme.FillRounded(g, Theme.Surface2, rect, LogicalToDeviceUnits(8));
        }

        return selected;
    }
}

/// <summary>The game list: name, exe names, a green dot while the game is active and its vibrance on the right.</summary>
internal sealed class GameListBox : DarkListBox
{
    public GameListBox() : base(42)
    {
    }

    public Func<GameProfile, bool>? IsActive { get; set; }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count || Items[e.Index] is not GameProfile game)
            return;

        var g = e.Graphics;
        bool selected = DrawRowBackground(e);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = e.Bounds;
        int pad = LogicalToDeviceUnits(12);
        int x = bounds.X + pad;
        bool active = IsActive?.Invoke(game) == true;

        if (active)
        {
            float dot = LogicalToDeviceUnits(8);
            float cy = bounds.Y + bounds.Height / 2f;
            using (var glow = new SolidBrush(Color.FromArgb(60, Theme.GameActive)))
                g.FillEllipse(glow, x - dot / 2f, cy - dot, dot * 2, dot * 2);
            using (var fill = new SolidBrush(Theme.GameActive))
                g.FillEllipse(fill, x, cy - dot / 2f, dot, dot);
            x += (int)dot + LogicalToDeviceUnits(10);
        }

        string percent = $"{Math.Round(game.Level)}%";
        Size percentSize = TextRenderer.MeasureText(g, percent, Theme.Button, Size.Empty, TextFormatFlags.NoPadding);
        int pillWidth = percentSize.Width + LogicalToDeviceUnits(16);
        int pillHeight = LogicalToDeviceUnits(22);
        var pill = new Rectangle(bounds.Right - pad - pillWidth, bounds.Y + (bounds.Height - pillHeight) / 2, pillWidth, pillHeight);
        Theme.FillRounded(g, Color.FromArgb(selected ? 80 : 50, Theme.Accent), pill, pillHeight / 2f);
        TextRenderer.DrawText(g, percent, Theme.Button, pill, Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        int textWidth = Math.Max(0, pill.Left - LogicalToDeviceUnits(10) - x);
        TextRenderer.DrawText(g, game.Name, Theme.Item, new Rectangle(x, bounds.Y + LogicalToDeviceUnits(3), textWidth, LogicalToDeviceUnits(20)), Theme.Text, flags);

        string detail = string.Join(", ", game.Processes.Select(p => p + ".exe"));
        if (active)
            detail = "active now · " + detail;
        TextRenderer.DrawText(g, detail, Theme.Small, new Rectangle(x, bounds.Y + LogicalToDeviceUnits(21), textWidth, LogicalToDeviceUnits(16)),
            active ? Theme.GameActive : Theme.TextMuted, flags);
    }
}
