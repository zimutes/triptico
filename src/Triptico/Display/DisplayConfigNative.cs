using System.Runtime.InteropServices;

namespace Triptico.Display;

/// <summary>
/// Interop com a API "Connecting and Configuring Displays" (CCD) do Windows:
/// QueryDisplayConfig / SetDisplayConfig / DisplayConfigGetDeviceInfo.
/// </summary>
internal static class DisplayConfigNative
{
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_INSUFFICIENT_BUFFER = 122;

    public const uint QDC_ALL_PATHS = 0x1;
    public const uint QDC_ONLY_ACTIVE_PATHS = 0x2;

    public const uint SDC_TOPOLOGY_SUPPLIED = 0x10;
    public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x20;
    public const uint SDC_VALIDATE = 0x40;
    public const uint SDC_APPLY = 0x80;
    public const uint SDC_SAVE_TO_DATABASE = 0x200;
    public const uint SDC_ALLOW_CHANGES = 0x400;
    public const uint SDC_ALLOW_PATH_ORDER_CHANGES = 0x2000;

    public const uint DISPLAYCONFIG_PATH_ACTIVE = 0x1;
    public const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;

    public const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;
    public const uint DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2;

    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID : IEquatable<LUID>
    {
        public uint LowPart;
        public int HighPart;

        public long Value => ((long)HighPart << 32) | LowPart;
        public bool Equals(LUID other) => LowPart == other.LowPart && HighPart == other.HighPart;
        public override bool Equals(object? obj) => obj is LUID l && Equals(l);
        public override int GetHashCode() => Value.GetHashCode();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public int outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public int targetAvailable; // BOOL
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public uint pixelFormat;
        public POINTL position;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public uint infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
        [FieldOffset(16)] public DISPLAYCONFIG_TARGET_MODE targetMode;
        [FieldOffset(16)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public int outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(
        uint numPathArrayElements, [In] DISPLAYCONFIG_PATH_INFO[]? pathArray,
        uint numModeInfoArrayElements, [In] DISPLAYCONFIG_MODE_INFO[]? modeInfoArray,
        uint flags);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    public static string OutputTechnologyName(int tech) => tech switch
    {
        0 => "VGA",
        4 => "DVI",
        5 => "HDMI",
        6 => "LVDS",
        10 => "DisplayPort",
        11 => "eDP",
        15 => "Miracast",
        16 or 17 => "Virtual",
        18 => "USB-C",
        unchecked((int)0x80000000) => "Interno",
        _ => "Outro",
    };
}
