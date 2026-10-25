namespace Vibrance;

internal enum OutputKind
{
    Nvidia,
    Amd,
    WindowsFilter,
}

/// <summary>
/// One screen whose vibrance the app controls. Settings and the UI use percentages in NVIDIA Control Panel
/// terms (50 = normal colours, 100 = maximum); every output maps them onto its own raw driver scale.
/// </summary>
internal abstract class DisplayTarget
{
    public const double NeutralPercent = 50;

    /// <summary>GDI name such as \\.\DISPLAY1, or null for the Windows filter, which covers every monitor.</summary>
    public required string? DeviceName { get; init; }

    /// <summary>Stable monitor id used to remember its desktop level.</summary>
    public required string Key { get; init; }

    public required string Name { get; set; }

    public abstract OutputKind Kind { get; }

    /// <summary>Raw level last read from or written to the driver.</summary>
    public int Level { get; set; }

    /// <summary>Raw level this output should be at right now.</summary>
    public int Target { get; set; }

    public abstract int ToRaw(double percent);

    public abstract double ToPercent(int raw);

    public abstract bool TryRead(out int raw);

    public abstract bool TryWrite(int raw);

    protected static (string Name, string Key) Identify(string gdiName, Dictionary<string, MonitorName> monitors, string? driverName = null)
    {
        monitors.TryGetValue(gdiName, out var monitor);
        string name = monitor is { FriendlyName.Length: > 0 } ? monitor.FriendlyName
            : !string.IsNullOrWhiteSpace(driverName) ? driverName.Trim()
            : FallbackName(gdiName);
        string key = monitor is { Key.Length: > 0 } ? monitor.Key : gdiName;
        return (name, key);
    }

    private static string FallbackName(string deviceName)
    {
        string digits = new(deviceName.Where(char.IsDigit).ToArray());
        return digits.Length > 0 ? $"Monitor {digits}" : "Monitor";
    }
}

/// <summary>NVIDIA Digital Vibrance: 0 = 50% (normal) … 63 = 100%. The classic call can't go below normal.</summary>
internal sealed class NvidiaDisplay : DisplayTarget
{
    public required IntPtr Handle { get; init; }

    public required int Min { get; init; }

    public required int Max { get; init; }

    public override OutputKind Kind => OutputKind.Nvidia;

    public override int ToRaw(double percent) =>
        Math.Clamp((int)Math.Round(Min + (percent - NeutralPercent) / 50 * (Max - Min)), Min, Max);

    public override double ToPercent(int raw) =>
        Max <= Min ? NeutralPercent : NeutralPercent + 50.0 * (raw - Min) / (Max - Min);

    public override bool TryRead(out int raw)
    {
        bool ok = NvApi.TryGetLevel(Handle, out var level);
        raw = level.Current;
        return ok;
    }

    public override bool TryWrite(int raw) => NvApi.SetLevel(Handle, raw);

    public static List<DisplayTarget> Discover(Dictionary<string, MonitorName> monitors)
    {
        var result = new List<DisplayTarget>();
        if (!NvApi.Initialize())
            return result;

        foreach (var nv in NvApi.EnumerateDisplays())
        {
            if (!NvApi.TryGetLevel(nv.Handle, out var level))
                continue;
            var (name, key) = Identify(nv.DeviceName, monitors);
            result.Add(new NvidiaDisplay
            {
                Handle = nv.Handle,
                DeviceName = nv.DeviceName,
                Key = key,
                Name = name,
                Min = level.Min,
                Max = level.Max,
                Level = level.Current,
                Target = level.Current,
            });
        }

        return result;
    }
}

/// <summary>AMD Radeon saturation: Default (100) = 50%, Max (200) = 100%; below 50% it desaturates.</summary>
internal sealed class AmdDisplay : DisplayTarget
{
    public required int AdapterIndex { get; init; }

    public required int DisplayIndex { get; init; }

    public required int Min { get; init; }

    public required int Default { get; init; }

    public required int Max { get; init; }

    public override OutputKind Kind => OutputKind.Amd;

    public override int ToRaw(double percent)
    {
        double raw = percent >= NeutralPercent
            ? Default + (percent - NeutralPercent) / 50 * (Max - Default)
            : Default - (NeutralPercent - percent) / 50 * (Default - Min);
        return Math.Clamp((int)Math.Round(raw), Min, Max);
    }

    public override double ToPercent(int raw)
    {
        if (raw >= Default)
            return Max > Default ? NeutralPercent + 50.0 * (raw - Default) / (Max - Default) : NeutralPercent;
        return Default > Min ? NeutralPercent - 50.0 * (Default - raw) / (Default - Min) : NeutralPercent;
    }

    public override bool TryRead(out int raw)
    {
        bool ok = Adl.TryGetSaturation(AdapterIndex, DisplayIndex, out var saturation);
        raw = saturation.Current;
        return ok;
    }

    public override bool TryWrite(int raw) => Adl.SetSaturation(AdapterIndex, DisplayIndex, raw);

    public static List<DisplayTarget> Discover(Dictionary<string, MonitorName> monitors)
    {
        var result = new List<DisplayTarget>();
        foreach (var amd in Adl.EnumerateDisplays())
        {
            var (name, key) = Identify(amd.GdiName, monitors, amd.MonitorName);
            result.Add(new AmdDisplay
            {
                AdapterIndex = amd.AdapterIndex,
                DisplayIndex = amd.DisplayIndex,
                DeviceName = amd.GdiName,
                Key = key,
                Name = name,
                Min = amd.Saturation.Min,
                Default = amd.Saturation.Default,
                Max = amd.Saturation.Max,
                Level = amd.Saturation.Current,
                Target = amd.Saturation.Current,
            });
        }

        return result;
    }
}

/// <summary>The Windows colour filter: one output for all monitors. Raw level = percent × 10.</summary>
internal sealed class WindowsFilterDisplay : DisplayTarget
{
    public override OutputKind Kind => OutputKind.WindowsFilter;

    public override int ToRaw(double percent) => (int)Math.Round(Math.Clamp(percent, 0, 100) * 10);

    public override double ToPercent(int raw) => raw / 10.0;

    public override bool TryRead(out int raw)
    {
        raw = ToRaw(ColorFilter.CurrentPercent);
        return true;
    }

    public override bool TryWrite(int raw) => ColorFilter.Apply(ToPercent(raw));

    public static List<DisplayTarget> Discover()
    {
        if (!ColorFilter.IsSupported)
            return new List<DisplayTarget>();

        int raw = (int)Math.Round(ColorFilter.CurrentPercent * 10);
        return new List<DisplayTarget>
        {
            new WindowsFilterDisplay { DeviceName = null, Key = "windows-filter", Name = "All monitors", Level = raw, Target = raw },
        };
    }
}
