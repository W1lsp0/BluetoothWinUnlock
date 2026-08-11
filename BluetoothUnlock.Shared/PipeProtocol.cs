using System;
using System.Text;

namespace BluetoothUnlock.Shared
{
    public static class PipeProtocol
    {
        public const int ProtocolVersion = 2;
        public const string PipeName = "BluetoothUnlock";
        public const string PipePath = @"\\.\pipe\BluetoothUnlock";

        public static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? ""));
        }

        public static string FormatCredential(PlainCredential credential)
        {
            return "OK\n" +
                   "domain:" + Encode(credential.Domain) + "\n" +
                   "username:" + Encode(credential.Username) + "\n" +
                   "password:" + Encode(credential.Password) + "\n" +
                   "END\n";
        }
    }
}
