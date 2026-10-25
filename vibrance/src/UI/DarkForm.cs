namespace Vibrance.UI;

/// <summary>Base for every window: dark colours, dark title bar, layout in 96-DPI units scaled by WinForms.</summary>
internal class DarkForm : Form
{
    public DarkForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.StyleTitleBar(Handle);
    }
}
