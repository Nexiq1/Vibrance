using System.Diagnostics;
using Microsoft.Win32;

namespace Vibrance;

internal enum VibranceMode
{
    Unavailable,
    Disabled,
    Desktop,
    Game,
    Preview,
}

/// <summary>
/// Watches the foreground window and keeps every output at the right vibrance:
/// the game's level while a listed game is focused, the monitor's desktop level otherwise.
/// Uses the graphics driver (NVIDIA, AMD) where it can, the Windows colour filter everywhere else.
/// Everything runs on the UI thread.
/// </summary>
internal sealed class VibranceEngine : IDisposable
{
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromSeconds(3);

    // How long after a switch the app keeps re-applying levels: games and display mode changes
    // can reset vibrance a moment later. After that, desktop changes made elsewhere are adopted.
    private static readonly TimeSpan EnforceAfterSwitch = TimeSpan.FromSeconds(8);

    private readonly AppSettings _settings;
    private readonly SynchronizationContext _ui;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Native.WinEventDelegate _foregroundChanged;
    private readonly IntPtr _foregroundHook;
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private List<DisplayTarget> _displays = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private DateTime _enforceUntil = DateTime.MinValue;
    private uint _cachedPid;
    private string? _cachedName;
    private double? _preview;
    private IntPtr _previewWindow;
    private bool _disposed;

    public VibranceEngine(AppSettings settings)
    {
        _settings = settings;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        RefreshDisplays();

        // Kept in a field: the hook calls back into this delegate for the lifetime of the app.
        _foregroundChanged = (_, _, _, _, _, _, _) => Update();
        _foregroundHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundChanged, 0, 0, Native.WINEVENT_OUTOFCONTEXT);

        // The timer catches what the hook can't: games resetting vibrance on a mode switch,
        // windows moving between monitors and games started while already focused.
        _timer.Tick += (_, _) => Update(verify: true);
        _timer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        Update(verify: true);
    }

    public event EventHandler? StateChanged;

    /// <summary>Raised when a desktop level was changed outside the app and adopted, so it should be saved.</summary>
    public event EventHandler? SettingsChanged;

    public IReadOnlyList<DisplayTarget> Displays => _displays;

    /// <summary>Graphics card name, e.g. "GeForce RTX 4070".</summary>
    public string AdapterName { get; private set; } = "";

    /// <summary>Whether the graphics driver itself can change vibrance on at least one monitor.</summary>
    public bool HardwareSupported { get; private set; }

    public bool UsesWindowsFilter => _displays.Count > 0 && _displays[0].Kind == OutputKind.WindowsFilter;

    public VibranceMode Mode { get; private set; } = VibranceMode.Unavailable;

    public GameProfile? ActiveGame { get; private set; }

    public double? PreviewLevel => _preview;

    public bool Available => _displays.Count > 0;

    /// <summary>Desktop level of a monitor in percent.</summary>
    public double DesktopLevel(DisplayTarget display) =>
        _settings.DesktopLevels.TryGetValue(display.Key, out double level) ? level : display.ToPercent(display.Level);

    public void SetDesktopLevel(DisplayTarget display, double percent)
    {
        _settings.DesktopLevels[display.Key] = Math.Clamp(percent, 0, 100);
        Update();
    }

    public void SetUseWindowsFilter(bool use)
    {
        if (_settings.UseWindowsFilter == use)
            return;
        _settings.UseWindowsFilter = use;
        RefreshDisplays();
        Update(verify: true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Temporarily shows <paramref name="percent"/>, so the user sees a game's vibrance while dragging its slider.
    /// With "only the game monitor" on, the preview goes to the monitor under <paramref name="window"/>.
    /// </summary>
    public void SetPreview(double? percent, IntPtr window = default)
    {
        _preview = percent;
        _previewWindow = window;
        Update();
    }

    public void Update(bool verify = false)
    {
        if (_disposed)
            return;

        if (_displays.Count == 0 && verify && DateTime.UtcNow - _lastRefresh > RefreshCooldown)
            RefreshDisplays();
        if (_displays.Count == 0)
        {
            SetState(VibranceMode.Unavailable, null, changed: false);
            return;
        }

        var (game, gameWindow) = _settings.Enabled && _preview is null ? FindActiveGame() : (null, IntPtr.Zero);

        string? focusMonitor = null;
        if (_settings.OnlyGameMonitor)
        {
            IntPtr window = _preview is null ? gameWindow : _previewWindow;
            if (window != IntPtr.Zero)
                focusMonitor = Native.GetMonitorDeviceName(window);
        }

        bool changed = false;
        bool failed = false;
        bool adopted = false;
        bool enforcing = DateTime.UtcNow < _enforceUntil;
        foreach (var display in _displays)
        {
            // The Windows filter has no monitor of its own: it always covers the game.
            bool focused = display.DeviceName == null || focusMonitor == null
                || string.Equals(focusMonitor, display.DeviceName, StringComparison.OrdinalIgnoreCase);
            bool showsDesktop = !(focused && (_preview != null || game != null));
            int target = display.ToRaw(showsDesktop ? DesktopLevel(display) : _preview ?? game!.Level);

            bool targetChanged = display.Target != target;
            if (targetChanged)
            {
                display.Target = target;
                changed = true;
                _enforceUntil = DateTime.UtcNow + EnforceAfterSwitch;
                enforcing = true;
            }

            if (verify)
            {
                if (!display.TryRead(out int current))
                {
                    failed = true;
                    continue;
                }
                display.Level = current;
            }

            if (display.Level == target)
                continue;

            // Changed in the NVIDIA App / Radeon Software while showing the desktop: that becomes the new
            // desktop level instead of being fought. Game and preview levels are always held.
            if (verify && showsDesktop && !enforcing)
            {
                _settings.DesktopLevels[display.Key] = Math.Round(display.ToPercent(display.Level), 2);
                display.Target = display.Level;
                changed = true;
                adopted = true;
                continue;
            }

            if (display.TryWrite(target))
                display.Level = target;
            else
                failed = true;
        }

        if (adopted)
            SettingsChanged?.Invoke(this, EventArgs.Empty);

        // Driver handles go stale after driver resets or monitor changes.
        if (failed && DateTime.UtcNow - _lastRefresh > RefreshCooldown)
        {
            RefreshDisplays();
            changed = true;
        }

        var mode = _preview != null ? VibranceMode.Preview
            : !_settings.Enabled ? VibranceMode.Disabled
            : game != null ? VibranceMode.Game
            : VibranceMode.Desktop;
        SetState(mode, game, changed);
    }

    public void RefreshDisplays()
    {
        _lastRefresh = DateTime.UtcNow;
        var monitors = DisplayNames.GetMonitors();

        var hardware = new List<DisplayTarget>();
        hardware.AddRange(NvidiaDisplay.Discover(monitors));
        hardware.AddRange(AmdDisplay.Discover(monitors));
        HardwareSupported = hardware.Count > 0;

        var displays = _settings.UseWindowsFilter || hardware.Count == 0 ? WindowsFilterDisplay.Discover() : hardware;
        if (!displays.Exists(d => d.Kind == OutputKind.WindowsFilter))
            ColorFilter.Reset();

        foreach (var display in displays)
        {
            // A monitor seen for the first time keeps whatever was set in the driver's own panel.
            _settings.DesktopLevels.TryAdd(display.Key, Math.Round(display.ToPercent(display.Level), 2));
        }

        foreach (var sameModel in displays.GroupBy(d => d.Name).Where(g => g.Count() > 1))
        {
            int number = 1;
            foreach (var display in sameModel)
                display.Name = $"{display.Name} ({number++})";
        }

        _displays = displays;
        string? adapter = hardware.Count > 0 ? Native.GetAdapterName(hardware[0].DeviceName!) : null;
        AdapterName = CleanAdapterName(adapter ?? Native.GetPrimaryAdapterName());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _timer.Dispose();
        if (_foregroundHook != IntPtr.Zero)
            Native.UnhookWinEvent(_foregroundHook);
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;

        // Never leave a game's vibrance on the desktop after quitting.
        foreach (var display in _displays)
            display.TryWrite(display.ToRaw(DesktopLevel(display)));
        ColorFilter.Reset();
    }

    private (GameProfile? Game, IntPtr Window) FindActiveGame()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        uint pid = Native.GetWindowProcessId(foreground);

        // While our own window is in front, the desktop levels being edited are what should show.
        if (pid == _ownPid)
            return (null, IntPtr.Zero);

        if (pid != 0 && ProcessName(pid) is string name && FindGame(name) is GameProfile game)
            return (game, foreground);

        return _settings.KeepWhileRunning ? FindRunningGame() : (null, IntPtr.Zero);
    }

    private GameProfile? FindGame(string processName) => _settings.Games.Find(g => g.Matches(processName));

    private (GameProfile? Game, IntPtr Window) FindRunningGame()
    {
        if (_settings.Games.Count == 0)
            return (null, IntPtr.Zero);

        Process[] processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                if (FindGame(process.ProcessName) is GameProfile game)
                    return (game, _settings.OnlyGameMonitor ? MainWindowOf(process) : IntPtr.Zero);
            }
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        return (null, IntPtr.Zero);
    }

    private static IntPtr MainWindowOf(Process process)
    {
        try
        {
            return process.MainWindowHandle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private string? ProcessName(uint pid)
    {
        if (pid == _cachedPid)
            return _cachedName;

        string? name = null;
        if (Native.GetProcessPath(pid) is string path)
        {
            name = Path.GetFileNameWithoutExtension(path);
        }
        else
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
            }
            catch
            {
                // The process exited in the meantime.
            }
        }

        _cachedPid = pid;
        _cachedName = name;
        return name;
    }

    private void SetState(VibranceMode mode, GameProfile? game, bool changed)
    {
        if (!changed && mode == Mode && ReferenceEquals(game, ActiveGame))
            return;

        Mode = mode;
        ActiveGame = game;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Post(() =>
    {
        // Resolution / fullscreen switches may reset vibrance; hold the levels for a moment.
        _enforceUntil = DateTime.UtcNow + EnforceAfterSwitch;
        RefreshDisplays();
        Update(verify: true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    });

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Post(() =>
            {
                RefreshDisplays();
                Update(verify: true);
                StateChanged?.Invoke(this, EventArgs.Empty);
            });
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect)
            Post(() => Update(verify: true));
    }

    // SystemEvents fire on their own thread.
    private void Post(Action action) => _ui.Post(_ =>
    {
        if (!_disposed)
            action();
    }, null);

    private static string CleanAdapterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        name = name.Replace("(R)", "").Replace("(TM)", "").Replace("  ", " ").Trim();
        return name.StartsWith("NVIDIA ", StringComparison.OrdinalIgnoreCase) ? name[7..] : name;
    }
}
