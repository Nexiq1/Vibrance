using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vibrance;

internal sealed class GameProfile
{
    public string Name { get; set; } = "";

    /// <summary>Process names without ".exe", e.g. "cs2".</summary>
    public List<string> Processes { get; set; } = new();

    /// <summary>Vibrance in percent: 50 = normal colours, 100 = maximum (NVIDIA Control Panel scale).</summary>
    public double Level { get; set; } = 80;

    public bool Matches(string processName) =>
        Processes.Exists(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeProcess(string exeOrPath)
    {
        string name = Path.GetFileName(exeOrPath.Trim());
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    public override string ToString() => Name;
}

internal sealed class AppSettings
{
    // 1: levels were raw NVIDIA values (0–63). 2: levels are percentages, independent of the graphics card.
    private const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int Version { get; set; }

    /// <summary>Desktop level in percent per monitor, keyed by <see cref="DisplayTarget.Key"/>.</summary>
    public Dictionary<string, double> DesktopLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Use the Windows colour filter even when the graphics driver supports vibrance itself.</summary>
    public bool UseWindowsFilter { get; set; }

    public bool Enabled { get; set; } = true;

    public bool OnlyGameMonitor { get; set; } = true;

    public bool KeepWhileRunning { get; set; }

    public bool TrayHintShown { get; set; }

    public List<GameProfile> Games { get; set; } = new();

    [JsonIgnore]
    public bool IsNew { get; private set; }

    [JsonIgnore]
    public string FilePath { get; private set; } = "";

    /// <summary>
    /// Uses settings.json next to the exe when it exists (portable mode, e.g. on a USB stick),
    /// otherwise %APPDATA%\Vibrance\settings.json.
    /// </summary>
    public static AppSettings Load()
    {
        string portable = Path.Combine(AppContext.BaseDirectory, "settings.json");
        string path = File.Exists(portable)
            ? portable
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vibrance", "settings.json");

        AppSettings? settings = null;
        try
        {
            if (File.Exists(path))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            // A broken file should not stop the app; it gets rewritten on the next save.
        }

        if (settings == null)
        {
            settings = new AppSettings { IsNew = !File.Exists(path), Version = CurrentVersion };
        }
        else
        {
            settings.DesktopLevels = new Dictionary<string, double>(settings.DesktopLevels ?? new(), StringComparer.OrdinalIgnoreCase);
            settings.Games ??= new();
            foreach (var game in settings.Games)
            {
                game.Processes = (game.Processes ?? new())
                    .Select(GameProfile.NormalizeProcess)
                    .Where(p => p.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (string.IsNullOrWhiteSpace(game.Name) && game.Processes.Count > 0)
                    game.Name = game.Processes[0];
            }
            settings.Games.RemoveAll(g => g.Processes.Count == 0);

            if (settings.Version < CurrentVersion)
            {
                foreach (string key in settings.DesktopLevels.Keys.ToList())
                    settings.DesktopLevels[key] = PercentFromNvidiaRaw(settings.DesktopLevels[key]);
                foreach (var game in settings.Games)
                    game.Level = PercentFromNvidiaRaw(game.Level);
                settings.Version = CurrentVersion;
            }
        }

        settings.FilePath = path;
        return settings;
    }

    private static double PercentFromNvidiaRaw(double raw) => Math.Round(50 + 50 * Math.Clamp(raw, 0, 63) / 63, 2);

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // Read-only folder or similar; settings simply don't persist.
        }
    }
}
