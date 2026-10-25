using System.Runtime.InteropServices;
using System.Text;

namespace Vibrance;

internal static class Native
{
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint devNum, ref DISPLAY_DEVICE displayDevice, uint flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    public static uint GetWindowProcessId(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return 0;
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    /// <summary>Full exe path using only PROCESS_QUERY_LIMITED_INFORMATION, which also works for elevated and anti-cheat protected games.</summary>
    public static string? GetProcessPath(uint pid)
    {
        IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero)
            return null;

        try
        {
            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            return QueryFullProcessImageName(process, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>GDI device name (e.g. \\.\DISPLAY1) of the monitor that shows most of the window.</summary>
    public static string? GetMonitorDeviceName(IntPtr hwnd)
    {
        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return null;

        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return GetMonitorInfo(monitor, ref info) ? info.szDevice : null;
    }

    /// <summary>Name of the graphics card driving a GDI display, e.g. "NVIDIA GeForce RTX 4070".</summary>
    public static string? GetAdapterName(string gdiDeviceName) =>
        DisplayAdapters().FirstOrDefault(d => string.Equals(d.DeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase)).DeviceString;

    /// <summary>Name of the graphics card driving the primary monitor.</summary>
    public static string? GetPrimaryAdapterName() =>
        DisplayAdapters().FirstOrDefault(d => (d.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0).DeviceString;

    /// <summary>Whether a graphics adapter with this PCI vendor id (e.g. "VEN_1002" for AMD) is installed.</summary>
    public static bool HasDisplayAdapter(string pciVendor) =>
        DisplayAdapters().Any(d => d.DeviceID?.Contains(pciVendor, StringComparison.OrdinalIgnoreCase) == true);

    private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;

    private static List<DISPLAY_DEVICE> DisplayAdapters()
    {
        var adapters = new List<DISPLAY_DEVICE>();
        var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            adapters.Add(device);
            device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        }

        return adapters;
    }
}
