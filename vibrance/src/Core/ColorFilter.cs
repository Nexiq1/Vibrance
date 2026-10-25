using System.Runtime.InteropServices;

namespace Vibrance;

/// <summary>
/// Saturation applied by Windows itself to the whole desktop through the Magnification API's colour matrix.
/// Works with any graphics card (Intel, laptops, cards without driver support), but on every monitor at once.
/// Windows removes the effect when the process exits, so a crash can't leave the screen oversaturated.
/// </summary>
internal static class ColorFilter
{
    // Saturation multiplier at 100%; 50% leaves colours untouched.
    private const float MaxSaturation = 2.0f;

    private static bool _initialized;

    public static bool IsSupported { get; } = CheckSupport();

    /// <summary>Currently applied level in percent (50 = off).</summary>
    public static double CurrentPercent { get; private set; } = 50;

    public static bool Apply(double percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        try
        {
            if (Math.Abs(percent - 50) < 0.05)
            {
                // Fully released at neutral, so the desktop composes exactly as without the app.
                if (_initialized)
                {
                    var identity = Matrix(1f);
                    MagSetFullscreenColorEffect(ref identity);
                    MagUninitialize();
                    _initialized = false;
                }
                CurrentPercent = 50;
                return true;
            }

            if (!_initialized)
            {
                if (!MagInitialize())
                    return false;
                _initialized = true;
            }

            var effect = Matrix(SaturationFor(percent));
            if (!MagSetFullscreenColorEffect(ref effect))
                return false;
            CurrentPercent = percent;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static void Reset() => Apply(50);

    private static float SaturationFor(double percent) =>
        percent >= 50 ? 1f + (float)((percent - 50) / 50) * (MaxSaturation - 1f) : (float)(percent / 50);

    /// <summary>Row-vector colour matrix (like GDI+ ColorMatrix) that scales saturation around Rec. 709 luminance.</summary>
    private static MagColorEffect Matrix(float s)
    {
        const float r = 0.2126f, g = 0.7152f, b = 0.0722f;
        float ir = r * (1 - s), ig = g * (1 - s), ib = b * (1 - s);
        return new MagColorEffect
        {
            Transform = new[]
            {
                ir + s, ir, ir, 0f, 0f,
                ig, ig + s, ig, 0f, 0f,
                ib, ib, ib + s, 0f, 0f,
                0f, 0f, 0f, 1f, 0f,
                0f, 0f, 0f, 0f, 1f,
            },
        };
    }

    private static bool CheckSupport() =>
        NativeLibrary.TryLoad("Magnification.dll", out IntPtr library)
        && NativeLibrary.TryGetExport(library, "MagSetFullscreenColorEffect", out _);

    [StructLayout(LayoutKind.Sequential)]
    private struct MagColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Transform;
    }

    [DllImport("Magnification.dll")]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll")]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll")]
    private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect effect);
}
