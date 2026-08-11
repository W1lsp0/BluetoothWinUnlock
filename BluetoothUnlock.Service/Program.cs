using System;
using System.ServiceProcess;
using System.Threading;

namespace BluetoothUnlock.Service
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (Environment.UserInteractive || HasArg(args, "--console"))
            {
                using (var cancellation = new CancellationTokenSource())
                {
                    var server = new PipeServer();
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        cancellation.Cancel();
                    };

                    Console.WriteLine("BluetoothUnlock service console mode started.");
                    server.Run(cancellation.Token);
                }
                return;
            }

            ServiceBase.Run(new BluetoothUnlockWindowsService());
        }

        private static bool HasArg(string[] args, string value)
        {
            if (args == null)
            {
                return false;
            }

            foreach (var arg in args)
            {
                if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
