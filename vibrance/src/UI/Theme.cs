using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Vibrance.UI;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(14, 14, 19);
    public static readonly Color Surface = Color.FromArgb(23, 23, 31);
    public static readonly Color Surface2 = Color.FromArgb(35, 35, 47);
    public static readonly Color SurfaceHover = Color.FromArgb(45, 45, 60);
    public static readonly Color Border = Color.FromArgb(39, 39, 53);
    public static readonly Color Track = Color.FromArgb(46, 46, 61);
    public static readonly Color Text = Color.FromArgb(242, 242, 247);
    public static readonly Color TextMuted = Color.FromArgb(150, 150, 170);
    public static readonly Color TextDim = Color.FromArgb(96, 96, 114);
    public static readonly Color Accent = Color.FromArgb(124, 92, 255);
    public static readonly Color GameActive = Color.FromArgb(118, 185, 0);
    public static readonly Color DesktopActive = Color.FromArgb(96, 165, 250);
    public static readonly Color Danger = Color.FromArgb(248, 113, 113);

    // Saturation ramp: washed-out on the left, fully vivid on the right — the slider literally shows what it does.
    private static readonly Color[] RampColors =
    {
        Color.FromArgb(92, 89, 120),
        Color.FromArgb(124, 92, 255),
        Color.FromArgb(224, 64, 251),
        Color.FromArgb(255, 77, 141),
    };

    private static readonly float[] RampPositions = { 0f, 0.38f, 0.72f, 1f };

    public static readonly Font Title = new("Segoe UI Semibold", 20f);
    public static readonly Font Heading = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Status = new("Segoe UI Semibold", 11f);
    public static readonly Font Value = new("Segoe UI Semibold", 12f);
    public static readonly Font Item = new("Segoe UI Semibold", 9.75f);
    public static readonly Font Button = new("Segoe UI Semibold", 9f);
    public static readonly Font Body = new("Segoe UI", 9f);
    public static readonly Font Small = new("Segoe UI", 8.25f);

    public static ColorBlend RampBlend() => new() { Colors = RampColors, Positions = RampPositions };

    public static ColorBlend BrandBlend() => new()
    {
        Colors = new[] { RampColors[1], RampColors[2], RampColors[3] },
        Positions = new[] { 0f, 0.55f, 1f },
    };

    /// <summary>Colour of the saturation ramp at <paramref name="t"/> (0–1).</summary>
    public static Color RampAt(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        for (int i = 1; i < RampPositions.Length; i++)
        {
            if (t <= RampPositions[i])
            {
                float local = (t - RampPositions[i - 1]) / (RampPositions[i] - RampPositions[i - 1]);
                return Lerp(RampColors[i - 1], RampColors[i], local);
            }
        }
        return RampColors[^1];
    }

    public static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.A + (b.A - a.A) * t),
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < 1f)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Color color, RectangleF rect, float radius)
    {
        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    /// <summary>Dark title bar (Windows 10 1809+) with a caption colour matching the window (Windows 11).</summary>
    public static void StyleTitleBar(IntPtr hwnd)
    {
        int on = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        int caption = ColorRef(Background);
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        int border = ColorRef(Border);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    /// <summary>Dark native scrollbars for list boxes.</summary>
    public static void DarkScrollbars(Control control) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}
