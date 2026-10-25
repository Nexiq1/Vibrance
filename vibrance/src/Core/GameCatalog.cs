using System.Diagnostics;

namespace Vibrance;

internal sealed record GamePreset(string Name, string[] Processes);

internal static class GameCatalog
{
    public static readonly GamePreset[] Presets =
    {
        new("Counter-Strike 2", new[] { "cs2" }),
        new("Valorant", new[] { "VALORANT-Win64-Shipping" }),
        new("Fortnite", new[] { "FortniteClient-Win64-Shipping" }),
        new("Apex Legends", new[] { "r5apex", "r5apex_dx12" }),
        new("League of Legends", new[] { "League of Legends" }),
        new("Overwatch 2", new[] { "Overwatch" }),
        new("Marvel Rivals", new[] { "Marvel-Win64-Shipping" }),
        new("Deadlock", new[] { "deadlock" }),
        new("Dota 2", new[] { "dota2" }),
        new("Rainbow Six Siege", new[] { "RainbowSix", "RainbowSix_Vulkan" }),
        new("PUBG: Battlegrounds", new[] { "TslGame" }),
        new("Rust", new[] { "RustClient" }),
        new("Call of Duty", new[] { "cod" }),
        new("Escape from Tarkov", new[] { "EscapeFromTarkov" }),
        new("The Finals", new[] { "Discovery" }),
        new("Rocket League", new[] { "RocketLeague" }),
        new("GTA V", new[] { "GTA5", "GTA5_Enhanced" }),
        new("Minecraft (Java)", new[] { "javaw" }),
    };

    /// <summary>Games added on the very first start, so the app does something useful right away.</summary>
    public static readonly string[] FirstRunGames = { "Counter-Strike 2", "Valorant" };

    /// <summary>A readable name for a picked .exe: its file description, then product name, then the file name.</summary>
    public static string NameFromFile(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            foreach (string? candidate in new[] { info.FileDescription, info.ProductName })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Trim().Length > 1)
                    return candidate.Trim();
            }
        }
        catch
        {
            // Fall through to the file name.
        }

        return Path.GetFileNameWithoutExtension(path);
    }
}
