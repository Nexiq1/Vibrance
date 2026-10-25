using Vibrance.UI;

namespace Vibrance;

internal sealed class MainForm : DarkForm
{
    private const int DefaultGamePercent = 80;

    private readonly AppSettings _settings;
    private readonly VibranceEngine _engine;
    private bool _allowShow;
    private bool _exiting;
    private bool _syncing;
    private bool _autostart;

    private readonly HeaderControl _header = new();
    private readonly ToggleSwitch _enabledToggle = new() { Text = "Enabled", BackColor = Theme.Background, AccessibleName = "Automatic switching" };
    private readonly StatusCard _status = new();
    private readonly SectionTitle _desktopTitle = new() { Text = "Desktop", Hint = "when you're not gaming" };
    private readonly Label _desktopValue = CreateValueLabel();
    private readonly SegmentedControl _monitorTabs = new();
    private readonly VibranceSlider _desktopSlider = new() { AccessibleName = "Desktop vibrance" };
    private readonly SectionTitle _gamesTitle = new() { Text = "Games", Hint = "switches automatically" };
    private readonly PillButton _addButton = new() { Text = "+  Add game", Primary = true };
    private readonly PillButton _removeButton = new() { Text = "Remove" };
    private readonly GameListBox _gameList = new() { EmptyText = "No games yet. Click “+ Add game” to add your first one." };
    private readonly SectionTitle _gameTitle = new();
    private readonly Label _gameValue = CreateValueLabel();
    private readonly VibranceSlider _gameSlider = new() { AccessibleName = "Game vibrance" };
    private readonly ToggleSwitch _autostartToggle = new() { Text = "Start with Windows (in the tray)" };
    private readonly ToggleSwitch _monitorToggle = new() { Text = "Only change the monitor the game is on" };
    private readonly ToggleSwitch _backgroundToggle = new() { Text = "Keep vibrance while the game runs in the background" };
    private readonly ToggleSwitch _filterToggle = new() { Text = "Windows filter instead of the driver (any GPU)" };
    private readonly Card _gamesCard = new();
    private readonly Card _optionsCard = new();
    private readonly Panel _divider = new() { BackColor = Theme.Border };
    private readonly ContextMenuStrip _addMenu = new();
    private readonly ContextMenuStrip _gameMenu = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _trayEnabledItem = new("Automatic switching");
    private readonly NotifyIcon _tray = new();
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 800 };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 1200 };
    private readonly Icon _trayIcon;
    private readonly Icon _trayIconOff;

    public MainForm(bool startInTray)
    {
        _allowShow = !startInTray;
        _settings = AppSettings.Load();
        ToolStripManager.Renderer = new DarkMenuRenderer();

        Icon = AppIcons.Load(SystemInformation.IconSize);
        _trayIcon = AppIcons.Load(SystemInformation.SmallIconSize);
        _trayIconOff = AppIcons.Grayscale(_trayIcon);

        SuspendLayout();
        Text = "Vibrance";
        ClientSize = new Size(480, 720);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        ResumeLayout(false);
        PerformLayout();

        _engine = new VibranceEngine(_settings);
        if (_settings.IsNew)
            AddFirstRunGames();

        _gameList.IsActive = game => ReferenceEquals(game, _engine.ActiveGame);
        _autostart = Autostart.IsEnabled;
        Autostart.RefreshPath();

        BuildMenus();
        WireEvents();
        _engine.StateChanged += (_, _) => RefreshState();
        _engine.SettingsChanged += (_, _) => ScheduleSave();

        RefreshGames(null);
        // Persists the desktop levels detected for each monitor right away.
        _settings.Save();
    }

    private GameProfile? SelectedGame => _gameList.SelectedItem as GameProfile;

    private DisplayTarget? SelectedDisplay =>
        _monitorTabs.SelectedIndex < _engine.Displays.Count ? _engine.Displays[_monitorTabs.SelectedIndex] : null;

    public void ShowFromTray()
    {
        _allowShow = true;
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Started with --tray: create the window (timers and hooks need it) but keep it hidden.
        if (!_allowShow)
        {
            value = false;
            if (!IsHandleCreated)
                CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_settings.TrayHintShown)
            {
                _settings.TrayHintShown = true;
                ScheduleSave();
                _tray.ShowBalloonTip(4000, "Vibrance is still running",
                    "The icon is next to the clock. Click it to open the window, or choose “Exit” from its menu.", ToolTipIcon.None);
            }
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _saveTimer.Stop();
        _previewTimer.Stop();
        _engine.Dispose();
        _settings.Save();
        _tray.Visible = false;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _trayIcon.Dispose();
            _trayIconOff.Dispose();
            _saveTimer.Dispose();
            _previewTimer.Dispose();
            _addMenu.Dispose();
            _gameMenu.Dispose();
            _trayMenu.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        _header.Location = new Point(22, 14);
        _header.Size = new Size(306, 58);
        _enabledToggle.Location = new Point(334, 26);
        _enabledToggle.Size = new Size(128, 30);

        _status.Location = new Point(20, 78);
        _status.Size = new Size(440, 56);

        var desktopCard = new Card { Location = new Point(20, 144), Size = new Size(440, 118) };
        _desktopTitle.Location = new Point(16, 12);
        _desktopTitle.Size = new Size(300, 24);
        _desktopValue.Location = new Point(324, 11);
        _desktopValue.Size = new Size(100, 26);
        _monitorTabs.Location = new Point(16, 44);
        _monitorTabs.Size = new Size(408, 30);
        _desktopSlider.Location = new Point(1, 78);
        _desktopSlider.Size = new Size(438, 34);
        desktopCard.Controls.AddRange(new Control[] { _desktopTitle, _desktopValue, _monitorTabs, _desktopSlider });

        _gamesCard.Location = new Point(20, 272);
        _gamesCard.Size = new Size(440, 296);
        _gamesTitle.Location = new Point(16, 14);
        _gamesTitle.Size = new Size(190, 24);
        _addButton.Location = new Point(216, 11);
        _addButton.Size = new Size(124, 30);
        _removeButton.Location = new Point(346, 11);
        _removeButton.Size = new Size(78, 30);
        _gameList.Location = new Point(8, 50);
        _gameList.Size = new Size(424, 168);
        _divider.Location = new Point(16, 228);
        _divider.Size = new Size(408, 1);
        _gameTitle.Location = new Point(16, 238);
        _gameTitle.Size = new Size(300, 24);
        _gameValue.Location = new Point(324, 237);
        _gameValue.Size = new Size(100, 26);
        _gameSlider.Location = new Point(1, 258);
        _gameSlider.Size = new Size(438, 34);
        _gamesCard.Controls.AddRange(new Control[] { _gamesTitle, _addButton, _removeButton, _gameList, _divider, _gameTitle, _gameValue, _gameSlider });

        foreach (var slider in new[] { _desktopSlider, _gameSlider })
        {
            slider.Minimum = (int)DisplayTarget.NeutralPercent;
            slider.Maximum = 100;
        }

        _optionsCard.Location = new Point(20, 578);
        _optionsCard.Size = new Size(440, 128);
        var toggles = new[] { _autostartToggle, _monitorToggle, _backgroundToggle, _filterToggle };
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i].Location = new Point(14, 8 + i * 28);
            toggles[i].Size = new Size(412, 30);
        }
        _optionsCard.Controls.AddRange(toggles);

        Controls.AddRange(new Control[] { _header, _enabledToggle, _status, desktopCard, _gamesCard, _optionsCard });
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToScreen();
    }

    /// <summary>On short screens (laptops at 125–150% scaling) the game list gives up rows so the window still fits.</summary>
    private void FitToScreen()
    {
        int overflow = Height - (Screen.FromControl(this).WorkingArea.Height - LogicalToDeviceUnits(12));
        int shrink = Math.Min(overflow, _gameList.Height - _gameList.ItemHeight * 2);
        if (shrink <= 0)
            return;

        _gameList.Height -= shrink;
        foreach (var control in new Control[] { _divider, _gameTitle, _gameValue, _gameSlider })
            control.Top -= shrink;
        _gamesCard.Height -= shrink;
        _optionsCard.Top -= shrink;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height - shrink);
        CenterToScreen();
    }

    private void BuildMenus()
    {
        _gameMenu.Items.Add("Rename…", null, (_, _) => RenameSelectedGame());
        _gameMenu.Items.Add("Remove from list", null, (_, _) => RemoveSelectedGame());
        _gameList.ContextMenuStrip = _gameMenu;

        var open = new ToolStripMenuItem("Open Vibrance", null, (_, _) => ShowFromTray());
        open.Font = new Font(open.Font, FontStyle.Bold);
        _trayEnabledItem.Click += (_, _) => SetEnabled(!_settings.Enabled);
        _trayMenu.Items.AddRange(new ToolStripItem[]
        {
            open,
            _trayEnabledItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()),
        });

        _tray.ContextMenuStrip = _trayMenu;
        _tray.Icon = _trayIcon;
        _tray.Text = "Vibrance";
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowFromTray();
        };
        _tray.Visible = true;
    }

    private void WireEvents()
    {
        _enabledToggle.CheckedChanged += (_, _) =>
        {
            if (!_syncing)
                SetEnabled(_enabledToggle.Checked);
        };

        _monitorTabs.SelectedIndexChanged += (_, _) => RefreshState();
        _desktopSlider.ValueChangedByUser += (_, _) =>
        {
            if (SelectedDisplay is not { } display)
                return;
            _engine.SetDesktopLevel(display, _desktopSlider.Value);
            ScheduleSave();
            RefreshState();
        };

        _addButton.Click += (_, _) => ShowAddMenu();
        _removeButton.Click += (_, _) => RemoveSelectedGame();
        _gameList.SelectedIndexChanged += (_, _) => RefreshState();
        _gameList.DoubleClick += (_, _) => RenameSelectedGame();
        _gameList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
                RemoveSelectedGame();
            else if (e.KeyCode == Keys.F2)
                RenameSelectedGame();
        };
        _gameList.MouseDown += (_, e) =>
        {
            // Right-click selects the row under the cursor before its menu opens.
            if (e.Button == MouseButtons.Right && _gameList.IndexFromPoint(e.Location) is int index and >= 0)
                _gameList.SelectedIndex = index;
        };
        _gameMenu.Opening += (_, e) => e.Cancel = SelectedGame == null;

        _gameSlider.ValueChangedByUser += (_, _) =>
        {
            if (SelectedGame is not { } game)
                return;
            game.Level = _gameSlider.Value;
            // Show the game's vibrance live while adjusting it; it reverts shortly after letting go.
            _engine.SetPreview(game.Level, Handle);
            RestartPreviewTimer();
            ScheduleSave();
            RefreshState();
        };
        _gameSlider.DragEnded += (_, _) => RestartPreviewTimer();
        _previewTimer.Tick += (_, _) =>
        {
            if (_gameSlider.IsDragging)
                return;
            _previewTimer.Stop();
            _engine.SetPreview(null);
        };

        _autostartToggle.CheckedChanged += (_, _) =>
        {
            if (_syncing)
                return;
            try
            {
                Autostart.Set(_autostartToggle.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Couldn't change the autostart setting: {ex.Message}", "Vibrance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _autostart = Autostart.IsEnabled;
            RefreshState();
        };
        _monitorToggle.CheckedChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _settings.OnlyGameMonitor = _monitorToggle.Checked;
            _engine.Update();
            ScheduleSave();
        };
        _backgroundToggle.CheckedChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _settings.KeepWhileRunning = _backgroundToggle.Checked;
            _engine.Update();
            ScheduleSave();
        };
        _filterToggle.CheckedChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _engine.SetUseWindowsFilter(_filterToggle.Checked);
            ScheduleSave();
            RefreshState();
        };

        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settings.Save();
        };
    }

    private void RefreshState()
    {
        _syncing = true;
        try
        {
            bool available = _engine.Available;
            bool filter = _engine.UsesWindowsFilter;
            var displays = _engine.Displays;

            string adapter = _engine.AdapterName.Length > 0 ? _engine.AdapterName : "graphics card";
            _header.Subtitle = filter ? $"Vibrance for games · {adapter} · Windows filter" : $"Vibrance for games · {adapter}";
            _enabledToggle.Checked = _settings.Enabled;
            _trayEnabledItem.Checked = _settings.Enabled;
            _autostartToggle.Checked = _autostart;
            _backgroundToggle.Checked = _settings.KeepWhileRunning;
            // The Windows filter always covers every monitor; without driver support it is the only option.
            _monitorToggle.Checked = _settings.OnlyGameMonitor && !filter;
            _monitorToggle.Enabled = !filter;
            _filterToggle.Checked = filter;
            _filterToggle.Enabled = _engine.HardwareSupported;
            _desktopTitle.Hint = filter ? "when you're not gaming · filter covers all monitors" : "when you're not gaming";

            _monitorTabs.SetItems(displays.Select(d => $"{d.Name}  {Math.Round(_engine.DesktopLevel(d))}%"), _monitorTabs.SelectedIndex);
            _monitorTabs.Enabled = available;
            var display = SelectedDisplay;
            _desktopSlider.Enabled = display != null;
            if (display != null)
                _desktopSlider.Value = (int)Math.Round(_engine.DesktopLevel(display));
            SetValueLabel(_desktopValue, display != null ? _engine.DesktopLevel(display) : null);

            var game = SelectedGame;
            _removeButton.Enabled = game != null;
            _gameTitle.Text = game?.Name ?? "Pick a game from the list";
            _gameTitle.Hint = game != null ? "vibrance in this game" : "";
            _gameSlider.Enabled = game != null && available;
            if (game != null)
                _gameSlider.Value = (int)Math.Round(game.Level);
            SetValueLabel(_gameValue, game?.Level);
            _gameList.Invalidate();

            var (dot, title) = _engine.Mode switch
            {
                VibranceMode.Unavailable => (Theme.Danger, "Can't change colors"),
                VibranceMode.Preview => (Theme.Accent, $"Preview · {Math.Round(_engine.PreviewLevel ?? 50)}%"),
                VibranceMode.Disabled => (Theme.TextDim, "Automatic switching is off"),
                VibranceMode.Game => (Theme.GameActive, $"In game: {_engine.ActiveGame?.Name}"),
                _ => (Theme.DesktopActive, "Desktop · waiting for a game from the list"),
            };
            string detail = _engine.Mode == VibranceMode.Unavailable
                ? "The graphics driver doesn't support saturation and the Windows filter is unavailable."
                : string.Join("   ·   ", displays.Select(d => $"{d.Name} {Math.Round(d.ToPercent(d.Target))}%"));
            _status.SetStatus(dot, title, detail);

            string tip = $"Vibrance – {title}";
            _tray.Text = tip.Length > 127 ? tip[..127] : tip;
            _tray.Icon = _settings.Enabled && available ? _trayIcon : _trayIconOff;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void SetValueLabel(Label label, double? percent)
    {
        if (percent is not double value)
        {
            label.Text = "–";
            label.ForeColor = Theme.TextDim;
            return;
        }

        float t = (float)((value - DisplayTarget.NeutralPercent) / (100 - DisplayTarget.NeutralPercent));
        label.Text = $"{Math.Round(value)}%";
        label.ForeColor = Theme.Lerp(Theme.Text, Theme.RampAt(t), 0.7f);
    }

    private void SetEnabled(bool enabled)
    {
        _settings.Enabled = enabled;
        _engine.Update();
        ScheduleSave();
        RefreshState();
    }

    private void AddFirstRunGames()
    {
        foreach (var preset in GameCatalog.Presets.Where(p => GameCatalog.FirstRunGames.Contains(p.Name)))
        {
            _settings.Games.Add(new GameProfile
            {
                Name = preset.Name,
                Processes = preset.Processes.ToList(),
                Level = NewGameLevel(),
            });
        }
        _engine.Update();
    }

    /// <summary>80%, or the brightest desktop level if that is higher — adding a game should never make it duller than the desktop.</summary>
    private double NewGameLevel() => Math.Round(Math.Max(
        DefaultGamePercent,
        _engine.Displays.Select(_engine.DesktopLevel).DefaultIfEmpty(DisplayTarget.NeutralPercent).Max()));

    private void ShowAddMenu()
    {
        foreach (var item in _addMenu.Items.Cast<ToolStripItem>().ToList())
            item.Dispose();

        _addMenu.Items.Add("From open windows…", null, (_, _) => AddFromRunning());
        _addMenu.Items.Add("Choose an .exe file…", null, (_, _) => AddFromFile());
        _addMenu.Items.Add(new ToolStripSeparator());

        var popular = new ToolStripMenuItem("Popular games");
        foreach (var preset in GameCatalog.Presets)
        {
            bool added = _settings.Games.Exists(g => preset.Processes.Any(g.Matches));
            var item = new ToolStripMenuItem(preset.Name) { Checked = added, Enabled = !added };
            item.Click += (_, _) => AddGame(preset.Name, preset.Processes);
            popular.DropDownItems.Add(item);
        }
        _addMenu.Items.Add(popular);

        _addMenu.Show(_addButton, new Point(0, _addButton.Height + LogicalToDeviceUnits(4)));
    }

    private void AddFromRunning()
    {
        using var picker = new ProcessPickerForm(name => _settings.Games.Exists(g => g.Matches(name)));
        if (picker.ShowDialog(this) == DialogResult.OK && picker.Result is { } result)
            AddGame(result.Name, new[] { result.ProcessName });
    }

    private void AddFromFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the game's .exe file",
            Filter = "Games and programs (*.exe)|*.exe",
            CheckFileExists = true,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddGame(GameCatalog.NameFromFile(dialog.FileName), new[] { dialog.FileName });
    }

    private void AddGame(string name, IEnumerable<string> processes)
    {
        var names = processes
            .Select(GameProfile.NormalizeProcess)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
            return;

        if (_settings.Games.Find(g => names.Any(g.Matches)) is { } existing)
        {
            _gameList.SelectedItem = existing;
            return;
        }

        var game = new GameProfile
        {
            Name = name.Length > 60 ? name[..60] : name,
            Processes = names,
            Level = NewGameLevel(),
        };
        _settings.Games.Add(game);
        RefreshGames(game);
        _engine.Update();
        ScheduleSave();
    }

    private void RemoveSelectedGame()
    {
        if (SelectedGame is not { } game)
            return;

        int index = _gameList.SelectedIndex;
        _settings.Games.Remove(game);
        RefreshGames(_settings.Games.Count > 0 ? _settings.Games[Math.Min(index, _settings.Games.Count - 1)] : null);
        _engine.Update();
        ScheduleSave();
    }

    private void RenameSelectedGame()
    {
        if (SelectedGame is not { } game)
            return;
        if (InputDialog.Show(this, "Rename", "Game name in the list:", game.Name) is string name)
        {
            game.Name = name;
            RefreshGames(game);
            ScheduleSave();
        }
    }

    private void RefreshGames(GameProfile? select)
    {
        select ??= SelectedGame;
        _gameList.BeginUpdate();
        _gameList.Items.Clear();
        foreach (var game in _settings.Games)
            _gameList.Items.Add(game);
        _gameList.EndUpdate();

        if (select != null && _settings.Games.Contains(select))
            _gameList.SelectedItem = select;
        else if (_gameList.Items.Count > 0)
            _gameList.SelectedIndex = 0;
        RefreshState();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void RestartPreviewTimer()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void ExitApp()
    {
        _exiting = true;
        Close();
    }

    private static Label CreateValueLabel() => new()
    {
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleRight,
        Font = Theme.Value,
        ForeColor = Theme.Text,
        BackColor = Theme.Surface,
        UseMnemonic = false,
    };
}
