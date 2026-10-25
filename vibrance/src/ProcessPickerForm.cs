using System.Diagnostics;
using Vibrance.UI;

namespace Vibrance;

/// <summary>Lists windows that are open right now, so a running game can be picked without hunting for its exe.</summary>
internal sealed class ProcessPickerForm : DarkForm
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ApplicationFrameHost", "TextInputHost", "SystemSettings", "ShellExperienceHost",
        "StartMenuExperienceHost", "SearchHost", "LockApp", "Taskmgr",
    };

    private readonly Func<string, bool> _isAdded;
    private readonly PickerListBox _list = new()
    {
        EmptyText = "No windows found. Start the game and click “Refresh”.",
    };
    private readonly PillButton _add = new() { Text = "Add", Primary = true, BackColor = Theme.Background, Enabled = false };

    public ProcessPickerForm(Func<string, bool> isAdded)
    {
        _isAdded = isAdded;

        SuspendLayout();
        Text = "Add a running game";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 476);

        var hint = new Label
        {
            Text = "Start the game (its main menu is enough), come back here and pick its window from the list.",
            Location = new Point(20, 14),
            Size = new Size(380, 36),
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Background,
        };

        var card = new Card { Location = new Point(20, 54), Size = new Size(380, 352) };
        _list.Location = new Point(6, 6);
        _list.Size = new Size(368, 340);
        card.Controls.Add(_list);

        var refresh = new PillButton { Text = "Refresh", Location = new Point(20, 422), Size = new Size(96, 34), BackColor = Theme.Background };
        var cancel = new PillButton { Text = "Cancel", Location = new Point(210, 422), Size = new Size(90, 34), BackColor = Theme.Background, DialogResult = DialogResult.Cancel };
        _add.Location = new Point(306, 422);
        _add.Size = new Size(94, 34);

        Controls.AddRange(new Control[] { hint, card, refresh, cancel, _add });
        AcceptButton = _add;
        CancelButton = cancel;
        ResumeLayout(false);

        refresh.Click += (_, _) => LoadEntries();
        _add.Click += (_, _) => Accept();
        _list.DoubleClick += (_, _) => Accept();
        _list.SelectedIndexChanged += (_, _) => _add.Enabled = _list.SelectedItem is Entry { AlreadyAdded: false };

        LoadEntries();
    }

    public (string Name, string ProcessName)? Result { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ClearEntries();
        base.Dispose(disposing);
    }

    private void Accept()
    {
        if (_list.SelectedItem is not Entry { AlreadyAdded: false } entry)
            return;
        Result = (entry.Title, entry.ProcessName);
        DialogResult = DialogResult.OK;
    }

    private void LoadEntries()
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            int self = Environment.ProcessId;
            var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == self || Ignored.Contains(process.ProcessName) || entries.ContainsKey(process.ProcessName))
                            continue;
                        if (process.MainWindowHandle == IntPtr.Zero)
                            continue;
                        string title = process.MainWindowTitle.Trim();
                        if (title.Length == 0)
                            continue;
                        if (title.Length > 60)
                            title = title[..60];

                        string? path = Native.GetProcessPath((uint)process.Id);
                        entries[process.ProcessName] = new Entry(title, process.ProcessName, LoadIcon(path), _isAdded(process.ProcessName));
                    }
                    catch
                    {
                        // Process exited or denies access; skip it.
                    }
                }
            }

            ClearEntries();
            _list.BeginUpdate();
            foreach (var entry in entries.Values.OrderBy(e => e.AlreadyAdded).ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase))
                _list.Items.Add(entry);
            _list.EndUpdate();
            _add.Enabled = false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ClearEntries()
    {
        foreach (var entry in _list.Items.OfType<Entry>())
            entry.Icon?.Dispose();
        _list.Items.Clear();
    }

    private static Bitmap? LoadIcon(string? path)
    {
        if (path == null)
            return null;
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            return icon?.ToBitmap();
        }
        catch
        {
            return null;
        }
    }

    private sealed record Entry(string Title, string ProcessName, Bitmap? Icon, bool AlreadyAdded)
    {
        public override string ToString() => Title;
    }

    private sealed class PickerListBox : DarkListBox
    {
        public PickerListBox() : base(44)
        {
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count || Items[e.Index] is not Entry entry)
                return;

            var g = e.Graphics;
            DrawRowBackground(e);
            var bounds = e.Bounds;
            int pad = LogicalToDeviceUnits(10);
            int iconSize = LogicalToDeviceUnits(24);
            var iconRect = new Rectangle(bounds.X + pad, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);

            if (entry.Icon != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(entry.Icon, iconRect);
            }
            else
            {
                Theme.FillRounded(g, Theme.Surface2, iconRect, LogicalToDeviceUnits(6));
            }

            int x = iconRect.Right + LogicalToDeviceUnits(12);
            int width = Math.Max(0, bounds.Right - pad - x);
            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, entry.Title, Theme.Item, new Rectangle(x, bounds.Y + LogicalToDeviceUnits(4), width, LogicalToDeviceUnits(20)),
                entry.AlreadyAdded ? Theme.TextMuted : Theme.Text, flags);
            string detail = entry.ProcessName + ".exe" + (entry.AlreadyAdded ? " · already in the list" : "");
            TextRenderer.DrawText(g, detail, Theme.Small, new Rectangle(x, bounds.Y + LogicalToDeviceUnits(23), width, LogicalToDeviceUnits(16)), Theme.TextMuted, flags);
        }
    }
}
