using Microsoft.Win32;

namespace Vibrance;

/// <summary>Per-user "start with Windows" entry under HKCU\...\Run; starts straight into the tray.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Vibrance";

    private static string Command => $"\"{Environment.ProcessPath}\" --tray";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Keeps the entry pointing at this exe if the user moved it since enabling autostart.</summary>
    public static void RefreshPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is string current && current != Command)
                Set(true);
        }
        catch
        {
            // Not critical.
        }
    }
}
