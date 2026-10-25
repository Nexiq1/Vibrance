namespace Vibrance;

internal static class Program
{
    private const string InstanceName = "Vibrance-3f9b2c71-single-instance";
    private const string ShowSignalName = "Vibrance-3f9b2c71-show";

    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = new Mutex(initiallyOwned: true, InstanceName, out bool firstInstance);
        if (!firstInstance)
        {
            // Already running (usually in the tray): bring that window up instead of starting a second copy.
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ShowSignalName);
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
            return;
        }

        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        ApplicationConfiguration.Initialize();

        bool startInTray = args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
        using var form = new MainForm(startInTray);
        var registration = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) =>
        {
            if (form.IsHandleCreated)
                form.BeginInvoke(new Action(form.ShowFromTray));
        }, null, Timeout.Infinite, executeOnlyOnce: false);

        Application.Run(form);
        registration.Unregister(null);
    }
}
