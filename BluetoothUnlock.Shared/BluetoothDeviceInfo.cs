using System;

namespace BluetoothUnlock.Shared
{
    public sealed class BluetoothDeviceInfo
    {
        public string Address { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Connected { get; set; }
        public bool Remembered { get; set; }
        public bool Authenticated { get; set; }
        public DateTime LastSeenUtc { get; set; } = DateTime.MinValue;

        public override string ToString()
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "未命名设备" : Name;
            var connected = Connected ? "  已连接" : "";
            return name + "  " + BluetoothAddress.FormatWithSeparators(Address) + connected;
        }
    }

    public static class BluetoothAddress
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var chars = new char[12];
            var count = 0;
            foreach (var c in value)
            {
                if (Uri.IsHexDigit(c))
                {
                    if (count >= chars.Length)
                    {
                        break;
                    }

                    chars[count++] = char.ToUpperInvariant(c);
                }
            }

            if (count != chars.Length)
            {
                return value.Trim().ToUpperInvariant();
            }

            return new string(chars);
        }

        public static string Format(ulong address)
        {
            return
                ((address >> 40) & 0xff).ToString("X2") +
                ((address >> 32) & 0xff).ToString("X2") +
                ((address >> 24) & 0xff).ToString("X2") +
                ((address >> 16) & 0xff).ToString("X2") +
                ((address >> 8) & 0xff).ToString("X2") +
                (address & 0xff).ToString("X2");
        }

        public static string FormatWithSeparators(string address)
        {
            var normalized = Normalize(address);
            if (normalized.Length != 12)
            {
                return normalized;
            }

            return
                normalized.Substring(0, 2) + ":" +
                normalized.Substring(2, 2) + ":" +
                normalized.Substring(4, 2) + ":" +
                normalized.Substring(6, 2) + ":" +
                normalized.Substring(8, 2) + ":" +
                normalized.Substring(10, 2);
        }
    }
}
