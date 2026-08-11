using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BluetoothUnlock.Shared
{
    public static class BluetoothDeviceScanner
    {
        private const int NameLength = 248;

        public static List<BluetoothDeviceInfo> FindDevices(bool issueInquiry)
        {
            var search = new BluetoothDeviceSearchParams
            {
                dwSize = Marshal.SizeOf(typeof(BluetoothDeviceSearchParams)),
                fReturnAuthenticated = true,
                fReturnRemembered = true,
                fReturnUnknown = issueInquiry,
                fReturnConnected = true,
                fIssueInquiry = issueInquiry,
                cTimeoutMultiplier = issueInquiry ? (byte)4 : (byte)1,
                hRadio = IntPtr.Zero,
            };

            var nativeInfo = new NativeBluetoothDeviceInfo
            {
                dwSize = Marshal.SizeOf(typeof(NativeBluetoothDeviceInfo)),
            };

            var result = new List<BluetoothDeviceInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var handle = BluetoothFindFirstDevice(ref search, ref nativeInfo);
            if (handle == IntPtr.Zero)
            {
                return result;
            }

            try
            {
                do
                {
                    AddDevice(result, seen, nativeInfo);
                    nativeInfo = new NativeBluetoothDeviceInfo
                    {
                        dwSize = Marshal.SizeOf(typeof(NativeBluetoothDeviceInfo)),
                    };
                }
                while (BluetoothFindNextDevice(handle, ref nativeInfo));
            }
            finally
            {
                BluetoothFindDeviceClose(handle);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        public static BluetoothDeviceInfo FindTarget(UnlockConfig config)
        {
            if (config == null || !config.BluetoothUnlockEnabled)
            {
                return null;
            }

            var targetAddress = BluetoothAddress.Normalize(config.BluetoothDeviceAddress);
            var targetName = (config.BluetoothDeviceName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(targetAddress) && string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            foreach (var device in FindDevices(true))
            {
                if (!IsNearby(device))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(targetAddress) &&
                    string.Equals(BluetoothAddress.Normalize(device.Address), targetAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return device;
                }

                if (string.IsNullOrWhiteSpace(targetAddress) &&
                    !string.IsNullOrWhiteSpace(targetName) &&
                    string.Equals(device.Name, targetName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return device;
                }
            }

            return null;
        }

        public static bool IsNearby(BluetoothDeviceInfo device)
        {
            if (device == null)
            {
                return false;
            }

            if (device.Connected)
            {
                return true;
            }

            return device.LastSeenUtc >= DateTime.UtcNow.AddMinutes(-2);
        }

        private static void AddDevice(
            List<BluetoothDeviceInfo> result,
            HashSet<string> seen,
            NativeBluetoothDeviceInfo nativeInfo)
        {
            var address = BluetoothAddress.Format(nativeInfo.Address);
            if (!seen.Add(address))
            {
                return;
            }

            result.Add(new BluetoothDeviceInfo
            {
                Address = address,
                Name = nativeInfo.szName ?? "",
                Connected = nativeInfo.fConnected,
                Remembered = nativeInfo.fRemembered,
                Authenticated = nativeInfo.fAuthenticated,
                LastSeenUtc = ConvertSystemTime(nativeInfo.stLastSeen),
            });
        }

        private static DateTime ConvertSystemTime(SystemTime value)
        {
            if (value.wYear < 2000)
            {
                return DateTime.MinValue;
            }

            try
            {
                return new DateTime(
                    value.wYear,
                    value.wMonth,
                    value.wDay,
                    value.wHour,
                    value.wMinute,
                    value.wSecond,
                    value.wMilliseconds,
                    DateTimeKind.Utc);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern IntPtr BluetoothFindFirstDevice(
            ref BluetoothDeviceSearchParams searchParams,
            ref NativeBluetoothDeviceInfo deviceInfo);

        [DllImport("bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BluetoothFindNextDevice(
            IntPtr findHandle,
            ref NativeBluetoothDeviceInfo deviceInfo);

        [DllImport("bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BluetoothFindDeviceClose(IntPtr findHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct BluetoothDeviceSearchParams
        {
            public int dwSize;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnAuthenticated;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnRemembered;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnUnknown;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fReturnConnected;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fIssueInquiry;

            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeBluetoothDeviceInfo
        {
            public int dwSize;
            public ulong Address;
            public uint ulClassofDevice;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fConnected;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fRemembered;

            [MarshalAs(UnmanagedType.Bool)]
            public bool fAuthenticated;

            public SystemTime stLastSeen;
            public SystemTime stLastUsed;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameLength)]
            public string szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }
    }
}
