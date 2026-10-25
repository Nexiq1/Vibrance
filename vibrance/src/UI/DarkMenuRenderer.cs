using System.Drawing.Drawing2D;

namespace Vibrance.UI;

/// <summary>Dark look for every context menu (installed globally through ToolStripManager.Renderer).</summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColors())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextDim;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.TextMuted;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = e.ImageRectangle;
        if (rect.Width <= 0)
            rect = new Rectangle(4, (e.Item.Height - 16) / 2, 16, 16);

        Theme.FillRounded(g, e.Item.Enabled ? Theme.Accent : Theme.Surface2, rect, 4);
        using var pen = new Pen(Color.White, Math.Max(1.6f, rect.Width / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[]
        {
            new PointF(rect.Left + rect.Width * 0.26f, rect.Top + rect.Height * 0.52f),
            new PointF(rect.Left + rect.Width * 0.44f, rect.Top + rect.Height * 0.70f),
            new PointF(rect.Left + rect.Width * 0.76f, rect.Top + rect.Height * 0.32f),
        });
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Surface2;
        public override Color MenuItemSelected => Theme.Surface2;
        public override Color MenuItemSelectedGradientBegin => Theme.Surface2;
        public override Color MenuItemSelectedGradientEnd => Theme.Surface2;
        public override Color MenuItemPressedGradientBegin => Theme.Surface2;
        public override Color MenuItemPressedGradientMiddle => Theme.Surface2;
        public override Color MenuItemPressedGradientEnd => Theme.Surface2;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Surface;
        public override Color CheckBackground => Theme.Accent;
        public override Color CheckSelectedBackground => Theme.Accent;
        public override Color CheckPressedBackground => Theme.Accent;
    }
}
