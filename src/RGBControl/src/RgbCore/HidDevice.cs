using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RgbCore
{
    /// <summary>
    /// Acceso mínimo a dispositivos HID de Windows (hid.dll + SetupAPI), sin dependencias externas.
    /// </summary>
    public sealed class HidDevice : IDisposable
    {
        public string Path { get; private set; }
        public ushort UsagePage { get; private set; }
        public ushort Usage { get; private set; }
        public int FeatureReportLength { get; private set; }

        private SafeFileHandle handle;

        private HidDevice() { }

        public static List<HidDevice> Enumerate(ushort vid, ushort pid)
        {
            var result = new List<HidDevice>();
            string needle = string.Format("vid_{0:x4}&pid_{1:x4}", vid, pid);

            foreach (string path in EnumerateHidPaths())
            {
                if (path.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var h = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.IsInvalid)
                {
                    // Algunas colecciones (teclado/ratón) no se pueden abrir en lectura/escritura; se ignoran.
                    h.Dispose();
                    continue;
                }

                var dev = new HidDevice { Path = path, handle = h };
                IntPtr preparsed;
                if (Native.HidD_GetPreparsedData(h, out preparsed))
                {
                    Native.HIDP_CAPS caps;
                    if (Native.HidP_GetCaps(preparsed, out caps) == Native.HIDP_STATUS_SUCCESS)
                    {
                        dev.UsagePage = caps.UsagePage;
                        dev.Usage = caps.Usage;
                        dev.FeatureReportLength = caps.FeatureReportByteLength;
                    }
                    Native.HidD_FreePreparsedData(preparsed);
                }
                result.Add(dev);
            }
            return result;
        }

        /// <summary>Envía un feature report. El primer byte del buffer es el ID del report.</summary>
        public void SetFeature(byte[] data)
        {
            byte[] buf = Fit(data);
            if (!Native.HidD_SetFeature(handle, buf, buf.Length))
                throw new IOException("HidD_SetFeature falló", new Win32Exception(Marshal.GetLastWin32Error()));
        }

        /// <summary>Lee un feature report con el ID indicado.</summary>
        public byte[] GetFeature(byte reportId)
        {
            byte[] buf = new byte[FeatureReportLength];
            buf[0] = reportId;
            if (!Native.HidD_GetFeature(handle, buf, buf.Length))
                throw new IOException("HidD_GetFeature falló", new Win32Exception(Marshal.GetLastWin32Error()));
            return buf;
        }

        private byte[] Fit(byte[] data)
        {
            if (data.Length == FeatureReportLength)
                return data;
            byte[] buf = new byte[FeatureReportLength];
            Array.Copy(data, buf, Math.Min(data.Length, buf.Length));
            return buf;
        }

        public void Dispose()
        {
            if (handle != null)
                handle.Dispose();
        }

        private static IEnumerable<string> EnumerateHidPaths()
        {
            Guid hidGuid;
            Native.HidD_GetHidGuid(out hidGuid);
            IntPtr set = Native.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == IntPtr.Zero || set == new IntPtr(-1))
                yield break;

            try
            {
                var iface = new Native.SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(iface);
                for (uint i = 0; Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref iface); i++)
                {
                    int required;
                    Native.SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        // cbSize de SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 en 64 bits, 6 en 32 bits.
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (Native.SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, required, out required, IntPtr.Zero))
                            yield return Marshal.PtrToStringUni(detail + 4);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
        }

        private static class Native
        {
            public const uint GENERIC_READ = 0x80000000;
            public const uint GENERIC_WRITE = 0x40000000;
            public const uint FILE_SHARE_READ = 1;
            public const uint FILE_SHARE_WRITE = 2;
            public const uint OPEN_EXISTING = 3;
            public const uint DIGCF_PRESENT = 0x2;
            public const uint DIGCF_DEVICEINTERFACE = 0x10;
            public const int HIDP_STATUS_SUCCESS = 0x00110000;

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DEVICE_INTERFACE_DATA
            {
                public int cbSize;
                public Guid InterfaceClassGuid;
                public int Flags;
                public IntPtr Reserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HIDP_CAPS
            {
                public ushort Usage;
                public ushort UsagePage;
                public ushort InputReportByteLength;
                public ushort OutputReportByteLength;
                public ushort FeatureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
                public ushort[] Reserved;
                public ushort NumberLinkCollectionNodes;
                public ushort NumberInputButtonCaps;
                public ushort NumberInputValueCaps;
                public ushort NumberInputDataIndices;
                public ushort NumberOutputButtonCaps;
                public ushort NumberOutputValueCaps;
                public ushort NumberOutputDataIndices;
                public ushort NumberFeatureButtonCaps;
                public ushort NumberFeatureValueCaps;
                public ushort NumberFeatureDataIndices;
            }

            [DllImport("hid.dll")]
            public static extern void HidD_GetHidGuid(out Guid guid);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] buffer, int length);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_GetFeature(SafeFileHandle h, byte[] buffer, int length);

            [DllImport("hid.dll")]
            public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);

            [DllImport("hid.dll")]
            public static extern bool HidD_FreePreparsedData(IntPtr data);

            [DllImport("hid.dll")]
            public static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);

            [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

            [DllImport("setupapi.dll")]
            public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, uint index,
                ref SP_DEVICE_INTERFACE_DATA data);

            [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data,
                IntPtr detail, int detailSize, out int requiredSize, IntPtr devInfo);

            [DllImport("setupapi.dll")]
            public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
                uint creation, uint flags, IntPtr template);
        }
    }
}
