using System.Drawing.Drawing2D;

namespace Vibrance.UI;

/// <summary>Rounded button; <see cref="Primary"/> ones use the brand gradient.</summary>
internal sealed class PillButton : Button
{
    private bool _hover;
    private bool _pressed;

    public PillButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = Theme.Button;
        UseMnemonic = false;
    }

    public bool Primary { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _pressed = e.Button == MouseButtons.Left;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = LogicalToDeviceUnits(8);
        using var path = Theme.RoundedRect(rect, radius);

        if (Primary && Enabled)
        {
            using var brush = new LinearGradientBrush(rect, Color.Black, Color.Black, LinearGradientMode.Horizontal);
            brush.InterpolationColors = Theme.BrandBlend();
            g.FillPath(brush, path);
            if (_pressed || _hover)
            {
                using var overlay = new SolidBrush(_pressed ? Color.FromArgb(40, Color.Black) : Color.FromArgb(28, Color.White));
                g.FillPath(overlay, path);
            }
        }
        else
        {
            Color fill = !Enabled ? Theme.Surface2 : _pressed ? Theme.Border : _hover ? Theme.SurfaceHover : Theme.Surface2;
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
        }

        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(Color.FromArgb(170, Color.White), LogicalToDeviceUnits(1));
            using var focus = Theme.RoundedRect(RectangleF.Inflate(rect, -1.5f, -1.5f), radius - 1);
            g.DrawPath(pen, focus);
        }

        Color text = !Enabled ? Theme.TextDim : Primary ? Color.White : ForeColor;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>On/off switch with its label to the right. Behaves like a CheckBox (Space toggles, screen readers see a check box).</summary>
internal sealed class ToggleSwitch : CheckBox
{
    private bool _hover;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Cursor = Cursors.Hand;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        UseMnemonic = false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float w = LogicalToDeviceUnits(36);
        float h = LogicalToDeviceUnits(20);
        var track = new RectangleF(LogicalToDeviceUnits(3), (Height - h) / 2f, w, h);

        using (var path = Theme.RoundedRect(track, h / 2f))
        {
            if (Checked && Enabled)
            {
                using var brush = new LinearGradientBrush(track, Color.Black, Color.Black, LinearGradientMode.Horizontal);
                brush.InterpolationColors = Theme.BrandBlend();
                g.FillPath(brush, path);
            }
            else
            {
                Color fill = !Enabled ? Theme.Surface2 : _hover ? Theme.SurfaceHover : Color.FromArgb(56, 56, 72);
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
            }
        }

        float margin = LogicalToDeviceUnits(3);
        float knob = h - margin * 2;
        float knobX = Checked ? track.Right - knob - margin : track.Left + margin;
        using (var brush = new SolidBrush(Enabled ? Color.White : Theme.TextMuted))
            g.FillEllipse(brush, knobX, track.Top + margin, knob, knob);

        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(Color.FromArgb(170, Color.White), LogicalToDeviceUnits(1));
            using var focus = Theme.RoundedRect(RectangleF.Inflate(track, 2.5f, 2.5f), h / 2f + 2.5f);
            g.DrawPath(pen, focus);
        }

        int textX = (int)track.Right + LogicalToDeviceUnits(12);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(textX, 0, Math.Max(0, Width - textX), Height), Enabled ? ForeColor : Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>Row of equal-width tabs, used to pick which monitor the desktop slider edits.</summary>
internal sealed class SegmentedControl : Control
{
    private string[] _items = Array.Empty<string>();
    private int _selected;
    private int _hover = -1;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        TabStop = true;
        BackColor = Theme.Surface;
        Font = Theme.Button;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PageTabList;
    }

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex => _selected;

    public int Count => _items.Length;

    public void SetItems(IEnumerable<string> items, int selected)
    {
        var next = items.ToArray();
        int nextSelected = next.Length == 0 ? 0 : Math.Clamp(selected, 0, next.Length - 1);
        if (next.SequenceEqual(_items) && nextSelected == _selected)
            return;
        _items = next;
        _selected = nextSelected;
        AccessibleName = _items.Length > 0 ? _items[_selected] : "";
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hover = IndexAt(e.X);
        if (hover != _hover)
        {
            _hover = hover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            Select(IndexAt(e.X));
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left)
            Select(_selected - 1);
        else if (e.KeyCode == Keys.Right)
            Select(_selected + 1);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var outer = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = LogicalToDeviceUnits(8);
        Theme.FillRounded(g, Theme.Background, outer, radius);
        if (_items.Length == 0)
            return;

        float pad = LogicalToDeviceUnits(3);
        float segment = (outer.Width - pad * 2) / _items.Length;
        for (int i = 0; i < _items.Length; i++)
        {
            var rect = new RectangleF(outer.X + pad + segment * i, outer.Y + pad, segment, outer.Height - pad * 2);
            if (i == _selected)
                Theme.FillRounded(g, Theme.Surface2, rect, radius - pad);
            else if (i == _hover)
                Theme.FillRounded(g, Color.FromArgb(26, 26, 35), rect, radius - pad);

            if (i == _selected && Focused && ShowFocusCues)
            {
                using var pen = new Pen(Color.FromArgb(150, Color.White));
                using var focus = Theme.RoundedRect(RectangleF.Inflate(rect, -1, -1), radius - pad - 1);
                g.DrawPath(pen, focus);
            }

            TextRenderer.DrawText(g, _items[i], Font, Rectangle.Round(rect), i == _selected ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }

    private int IndexAt(int x)
    {
        if (_items.Length == 0)
            return -1;
        float pad = LogicalToDeviceUnits(3);
        float segment = (Width - 1f - pad * 2) / _items.Length;
        return Math.Clamp((int)((x - pad) / segment), 0, _items.Length - 1);
    }

    private void Select(int index)
    {
        if (_items.Length == 0)
            return;
        index = Math.Clamp(index, 0, _items.Length - 1);
        if (index == _selected)
            return;
        _selected = index;
        AccessibleName = _items[_selected];
        Invalidate();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
