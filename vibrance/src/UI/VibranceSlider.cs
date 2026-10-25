using System.Drawing.Drawing2D;

namespace Vibrance.UI;

/// <summary>
/// Horizontal slider whose filled track goes from washed-out to vivid.
/// Setting <see cref="Value"/> from code raises nothing; only the user's own changes raise <see cref="ValueChangedByUser"/>.
/// </summary>
internal sealed class VibranceSlider : Control
{
    private int _minimum;
    private int _maximum = 63;
    private int _value;
    private bool _dragging;
    private bool _hover;

    public VibranceSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.Slider;
    }

    public event EventHandler? ValueChangedByUser;

    public event EventHandler? DragEnded;

    public bool IsDragging => _dragging;

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            _maximum = Math.Max(_maximum, value);
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum);
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, _minimum, _maximum);
            if (clamped == _value)
                return;
            _value = clamped;
            Invalidate();
        }
    }

    public float Fraction => _maximum == _minimum ? 0f : (float)(_value - _minimum) / (_maximum - _minimum);

    private float ThumbRadius => LogicalToDeviceUnits(9);

    private RectangleF Track
    {
        get
        {
            float inset = ThumbRadius + LogicalToDeviceUnits(7);
            float height = LogicalToDeviceUnits(6);
            return new RectangleF(inset, (Height - height) / 2f, Math.Max(1f, Width - inset * 2), height);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        Focus();
        _dragging = true;
        SetFromUser(ValueAt(e.X));
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
            SetFromUser(ValueAt(e.X));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        EndDrag();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        EndDrag();
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

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        SetFromUser(_value + Math.Sign(e.Delta));
        if (e is HandledMouseEventArgs handled)
            handled.Handled = true;
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int? next = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _value - 1,
            Keys.Right or Keys.Up => _value + 1,
            Keys.PageDown => _value - 5,
            Keys.PageUp => _value + 5,
            Keys.Home => _minimum,
            Keys.End => _maximum,
            _ => null,
        };
        if (next is int value)
        {
            SetFromUser(value);
            e.Handled = true;
        }
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = Track;
        float radius = track.Height / 2f;
        Theme.FillRounded(g, Theme.Track, track, radius);

        float thumbX = track.Left + track.Width * Fraction;
        float cy = track.Top + track.Height / 2f;

        if (thumbX - track.Left >= 1f)
        {
            var filled = new RectangleF(track.Left, track.Top, thumbX - track.Left, track.Height);
            using var path = Theme.RoundedRect(filled, radius);
            if (Enabled)
            {
                using var brush = new LinearGradientBrush(new RectangleF(track.Left - 1, track.Top, track.Width + 2, track.Height),
                    Color.Black, Color.Black, LinearGradientMode.Horizontal);
                brush.InterpolationColors = Theme.RampBlend();
                g.FillPath(brush, path);
            }
            else
            {
                using var brush = new SolidBrush(Theme.TextDim);
                g.FillPath(brush, path);
            }
        }

        float r = ThumbRadius;
        Color color = Enabled ? Theme.RampAt(Fraction) : Theme.TextDim;
        if (Enabled && (_hover || _dragging || Focused))
        {
            float halo = r + LogicalToDeviceUnits(_dragging ? 6 : 4);
            using var haloBrush = new SolidBrush(Color.FromArgb(_dragging ? 75 : 45, color));
            g.FillEllipse(haloBrush, thumbX - halo, cy - halo, halo * 2, halo * 2);
        }

        using (var knob = new SolidBrush(Enabled ? Color.White : Theme.TextMuted))
            g.FillEllipse(knob, thumbX - r, cy - r, r * 2, r * 2);

        float inner = r * 0.48f;
        using (var center = new SolidBrush(color))
            g.FillEllipse(center, thumbX - inner, cy - inner, inner * 2, inner * 2);
    }

    private int ValueAt(int x)
    {
        var track = Track;
        float t = Math.Clamp((x - track.Left) / track.Width, 0f, 1f);
        return _minimum + (int)Math.Round(t * (_maximum - _minimum));
    }

    private void SetFromUser(int value)
    {
        if (!Enabled)
            return;
        int clamped = Math.Clamp(value, _minimum, _maximum);
        if (clamped == _value)
            return;
        _value = clamped;
        Invalidate();
        ValueChangedByUser?.Invoke(this, EventArgs.Empty);
    }

    private void EndDrag()
    {
        if (!_dragging)
            return;
        _dragging = false;
        Invalidate();
        DragEnded?.Invoke(this, EventArgs.Empty);
    }
}
