using System.Runtime.InteropServices;
using System.Text;

namespace Vibrance;

internal readonly record struct DvcLevel(int Current, int Min, int Max);

internal readonly record struct NvDisplay(IntPtr Handle, string DeviceName);

/// <summary>
/// Digital Vibrance Control through nvapi64.dll — the same driver calls the NVIDIA Control Panel uses.
/// Functions are resolved by id through nvapi_QueryInterface, which is what the public NVAPI SDK does internally.
/// </summary>
internal static class NvApi
{
    private const int StatusOk = 0;

    private const uint IdInitialize = 0x0150E828;
    private const uint IdEnumNvidiaDisplayHandle = 0x9ABDD40D;
    private const uint IdGetAssociatedNvidiaDisplayName = 0x22A78B05;
    private const uint IdGetDvcInfo = 0x4085DE45;
    private const uint IdSetDvcLevel = 0x172409B4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NvDvcInfo
    {
        public uint Version;
        public int CurrentLevel;
        public int MinLevel;
        public int MaxLevel;
    }

    // MAKE_NVAPI_VERSION(NV_DISPLAY_DVC_INFO, 1)
    private static readonly uint DvcInfoVersion = (uint)Marshal.SizeOf<NvDvcInfo>() | (1u << 16);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumNvidiaDisplayHandleFn(uint index, out IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetAssociatedNvidiaDisplayNameFn(IntPtr handle, [Out] byte[] name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDvcInfoFn(IntPtr handle, uint outputId, ref NvDvcInfo info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetDvcLevelFn(IntPtr handle, uint outputId, int level);

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    private static EnumNvidiaDisplayHandleFn? _enumDisplays;
    private static GetAssociatedNvidiaDisplayNameFn? _getDisplayName;
    private static GetDvcInfoFn? _getDvcInfo;
    private static SetDvcLevelFn? _setDvcLevel;
    private static bool _attempted;

    public static bool IsAvailable { get; private set; }

    public static string? Error { get; private set; }

    public static bool Initialize()
    {
        if (_attempted)
            return IsAvailable;
        _attempted = true;

        try
        {
            var initialize = Resolve<InitializeFn>(IdInitialize);
            _enumDisplays = Resolve<EnumNvidiaDisplayHandleFn>(IdEnumNvidiaDisplayHandle);
            _getDisplayName = Resolve<GetAssociatedNvidiaDisplayNameFn>(IdGetAssociatedNvidiaDisplayName);
            _getDvcInfo = Resolve<GetDvcInfoFn>(IdGetDvcInfo);
            _setDvcLevel = Resolve<SetDvcLevelFn>(IdSetDvcLevel);

            int status = initialize();
            if (status != StatusOk)
            {
                Error = $"The NVIDIA driver refused to initialize (code {status}).";
                return false;
            }

            IsAvailable = true;
            Error = null;
        }
        catch (DllNotFoundException)
        {
            Error = "NVIDIA driver not found (nvapi64.dll).";
        }
        catch (EntryPointNotFoundException)
        {
            Error = "This NVIDIA driver doesn't support Digital Vibrance.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        return IsAvailable;
    }

    public static List<NvDisplay> EnumerateDisplays()
    {
        var displays = new List<NvDisplay>();
        if (!IsAvailable)
            return displays;

        for (uint i = 0; i < 32; i++)
        {
            if (_enumDisplays!(i, out IntPtr handle) != StatusOk)
                break;

            var buffer = new byte[64];
            string name = "";
            if (_getDisplayName!(handle, buffer) == StatusOk)
            {
                int length = Array.IndexOf(buffer, (byte)0);
                name = Encoding.ASCII.GetString(buffer, 0, length < 0 ? buffer.Length : length);
            }

            displays.Add(new NvDisplay(handle, name));
        }

        return displays;
    }

    public static bool TryGetLevel(IntPtr display, out DvcLevel level)
    {
        level = default;
        if (!IsAvailable)
            return false;

        var info = new NvDvcInfo { Version = DvcInfoVersion };
        if (_getDvcInfo!(display, 0, ref info) != StatusOk)
            return false;

        level = new DvcLevel(info.CurrentLevel, info.MinLevel, info.MaxLevel);
        return true;
    }

    public static bool SetLevel(IntPtr display, int level) =>
        IsAvailable && _setDvcLevel!(display, 0, level) == StatusOk;

    private static T Resolve<T>(uint id) where T : Delegate
    {
        IntPtr pointer = QueryInterface(id);
        if (pointer == IntPtr.Zero)
            throw new EntryPointNotFoundException($"NvAPI 0x{id:X8}");
        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }
}
