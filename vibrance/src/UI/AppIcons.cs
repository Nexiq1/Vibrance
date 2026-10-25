using System.Drawing.Imaging;

namespace Vibrance.UI;

internal static class AppIcons
{
    public static Icon Load(Size size)
    {
        using var stream = typeof(AppIcons).Assembly.GetManifestResourceStream("Vibrance.app.ico");
        return stream != null ? new Icon(stream, size) : (Icon)SystemIcons.Application.Clone();
    }

    /// <summary>Faded grey copy for the tray while automatic switching is off.</summary>
    public static Icon Grayscale(Icon icon)
    {
        using var source = icon.ToBitmap();
        using var gray = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(gray))
        using (var attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(new ColorMatrix(new[]
            {
                new[] { 0.30f, 0.30f, 0.30f, 0f, 0f },
                new[] { 0.59f, 0.59f, 0.59f, 0f, 0f },
                new[] { 0.11f, 0.11f, 0.11f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0.7f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f },
            }));
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }

        IntPtr handle = gray.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }
}
