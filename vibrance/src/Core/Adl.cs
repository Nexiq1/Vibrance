using System.Runtime.InteropServices;

namespace Vibrance;

internal readonly record struct AdlSaturation(int Current, int Default, int Min, int Max);

internal readonly record struct AdlDisplay(int AdapterIndex, int DisplayIndex, string GdiName, string MonitorName, AdlSaturation Saturation);

/// <summary>
/// AMD Display Library (atiadlxx.dll): the "Saturation" setting from Radeon Software's custom colour panel,
/// which is AMD's equivalent of Digital Vibrance (usually 0–200, 100 = normal).
/// </summary>
internal static class Adl
{
    private const int AmdVendorId = 0x1002;
    private const int ColorSaturation = 1 << 2;
    private const int DisplayConnected = 0x1;
    private const int DisplayMapped = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct AdapterInfo
    {
        public int Size;
        public int AdapterIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Udid;
        public int BusNumber;
        public int DeviceNumber;
        public int FunctionNumber;
        public int VendorId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AdapterName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DisplayName;
        public int Present;
        public int Exist;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DriverPath;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DriverPathExt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string PnpString;
        public int OsDisplayIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayId
    {
        public int LogicalIndex;
        public int PhysicalIndex;
        public int LogicalAdapterIndex;
        public int PhysicalAdapterIndex;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DisplayInfo
    {
        public DisplayId Id;
        public int ControllerIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ManufacturerName;
        public int DisplayType;
        public int OutputType;
        public int Connector;
        public int InfoMask;
        public int InfoValue;
    }

    private delegate IntPtr MallocCallback(int size);

    private delegate int MainControlCreateFn(MallocCallback callback, int enumConnectedAdapters, out IntPtr context);

    private delegate int NumberOfAdaptersFn(IntPtr context, out int count);

    private delegate int AdapterInfoFn(IntPtr context, IntPtr info, int inputSize);

    private delegate int DisplayInfoFn(IntPtr context, int adapterIndex, out int count, out IntPtr info, int forceDetect);

    private delegate int ColorGetFn(IntPtr context, int adapterIndex, int displayIndex, int colorType,
        out int current, out int defaultValue, out int min, out int max, out int step);

    private delegate int ColorSetFn(IntPtr context, int adapterIndex, int displayIndex, int colorType, int current);

    // ADL allocates its result buffers through this callback for as long as it is loaded, so it must stay referenced.
    private static readonly MallocCallback Malloc = size => Marshal.AllocHGlobal(size);

    private static bool _attempted;
    private static IntPtr _context;
    private static NumberOfAdaptersFn? _numberOfAdapters;
    private static AdapterInfoFn? _adapterInfo;
    private static DisplayInfoFn? _displayInfo;
    private static ColorGetFn? _colorGet;
    private static ColorSetFn? _colorSet;

    public static bool IsAvailable { get; private set; }

    public static bool Initialize()
    {
        if (_attempted)
            return IsAvailable;
        _attempted = true;

        // Only touch the driver library when an AMD card is present: copies of it linger after switching GPUs.
        if (!Native.HasDisplayAdapter("VEN_1002") || !NativeLibrary.TryLoad("atiadlxx.dll", out IntPtr library))
            return false;

        try
        {
            var create = Export<MainControlCreateFn>(library, "ADL2_Main_Control_Create");
            _numberOfAdapters = Export<NumberOfAdaptersFn>(library, "ADL2_Adapter_NumberOfAdapters_Get");
            _adapterInfo = Export<AdapterInfoFn>(library, "ADL2_Adapter_AdapterInfo_Get");
            _displayInfo = Export<DisplayInfoFn>(library, "ADL2_Display_DisplayInfo_Get");
            _colorGet = Export<ColorGetFn>(library, "ADL2_Display_Color_Get");
            _colorSet = Export<ColorSetFn>(library, "ADL2_Display_Color_Set");
            IsAvailable = Succeeded(create(Malloc, 1, out _context));
        }
        catch (EntryPointNotFoundException)
        {
            IsAvailable = false;
        }

        return IsAvailable;
    }

    public static List<AdlDisplay> EnumerateDisplays()
    {
        var result = new List<AdlDisplay>();
        if (!Initialize() || !Succeeded(_numberOfAdapters!(_context, out int count)) || count <= 0)
            return result;

        int adapterSize = Marshal.SizeOf<AdapterInfo>();
        int displaySize = Marshal.SizeOf<DisplayInfo>();
        IntPtr adapters = Marshal.AllocHGlobal(adapterSize * count);
        try
        {
            Marshal.Copy(new byte[adapterSize * count], 0, adapters, adapterSize * count);
            for (int i = 0; i < count; i++)
                Marshal.WriteInt32(adapters + i * adapterSize, adapterSize);
            if (!Succeeded(_adapterInfo!(_context, adapters, adapterSize * count)))
                return result;

            var seen = new HashSet<(int Adapter, int Display)>();
            for (int i = 0; i < count; i++)
            {
                var adapter = Marshal.PtrToStructure<AdapterInfo>(adapters + i * adapterSize);
                if (adapter.VendorId != AmdVendorId)
                    continue;
                if (!Succeeded(_displayInfo!(_context, adapter.AdapterIndex, out int displayCount, out IntPtr displays, 0)) || displays == IntPtr.Zero)
                    continue;

                try
                {
                    for (int j = 0; j < displayCount; j++)
                    {
                        var display = Marshal.PtrToStructure<DisplayInfo>(displays + j * displaySize);
                        const int active = DisplayConnected | DisplayMapped;
                        // Every adapter lists all displays of its GPU; keep the one mapped to this adapter's \\.\DISPLAYn.
                        if ((display.InfoValue & active) != active || display.Id.LogicalAdapterIndex != adapter.AdapterIndex)
                            continue;
                        if (!seen.Add((adapter.AdapterIndex, display.Id.LogicalIndex)))
                            continue;
                        if (TryGetSaturation(adapter.AdapterIndex, display.Id.LogicalIndex, out var saturation))
                            result.Add(new AdlDisplay(adapter.AdapterIndex, display.Id.LogicalIndex, adapter.DisplayName, display.DisplayName, saturation));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(displays);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(adapters);
        }

        return result;
    }

    public static bool TryGetSaturation(int adapterIndex, int displayIndex, out AdlSaturation saturation)
    {
        saturation = default;
        if (!IsAvailable)
            return false;
        if (!Succeeded(_colorGet!(_context, adapterIndex, displayIndex, ColorSaturation, out int current, out int defaultValue, out int min, out int max, out _)))
            return false;
        saturation = new AdlSaturation(current, defaultValue, min, max);
        return true;
    }

    public static bool SetSaturation(int adapterIndex, int displayIndex, int value) =>
        IsAvailable && Succeeded(_colorSet!(_context, adapterIndex, displayIndex, ColorSaturation, value));

    // ADL_OK = 0; positive codes are "OK with a warning".
    private static bool Succeeded(int status) => status >= 0;

    private static T Export<T>(IntPtr library, string name) where T : Delegate =>
        NativeLibrary.TryGetExport(library, name, out IntPtr pointer)
            ? Marshal.GetDelegateForFunctionPointer<T>(pointer)
            : throw new EntryPointNotFoundException(name);
}
