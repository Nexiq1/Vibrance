namespace Vibrance.UI;

/// <summary>Small dark prompt for a single line of text (used to rename a game).</summary>
internal static class InputDialog
{
    public static string? Show(IWin32Window owner, string title, string prompt, string initial)
    {
        using var form = new DarkForm
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
        };

        form.SuspendLayout();
        form.ClientSize = new Size(360, 148);

        var label = new Label
        {
            Text = prompt,
            Location = new Point(20, 16),
            Size = new Size(320, 20),
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Background,
        };

        var box = new TextBox
        {
            Text = initial,
            Location = new Point(20, 42),
            Size = new Size(320, 28),
            BackColor = Theme.Surface2,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10.5f),
            MaxLength = 80,
        };

        var cancel = new PillButton { Text = "Cancel", Location = new Point(156, 96), Size = new Size(88, 32), BackColor = Theme.Background, DialogResult = DialogResult.Cancel };
        var ok = new PillButton { Text = "Save", Primary = true, Location = new Point(252, 96), Size = new Size(88, 32), BackColor = Theme.Background, DialogResult = DialogResult.OK };

        form.Controls.AddRange(new Control[] { label, box, cancel, ok });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.ResumeLayout(false);
        form.Shown += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };

        return form.ShowDialog(owner) == DialogResult.OK && box.Text.Trim().Length > 0 ? box.Text.Trim() : null;
    }
}
