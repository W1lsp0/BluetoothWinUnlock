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
                    var bluetoothMonitor = new BluetoothMonitor();
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        cancellation.Cancel();
                    };

                    Console.WriteLine("BluetoothUnlock service console mode started.");
                    var bluetoothTask = System.Threading.Tasks.Task.Factory.StartNew(
                        () => bluetoothMonitor.Run(cancellation.Token),
                        cancellation.Token,
                        System.Threading.Tasks.TaskCreationOptions.LongRunning,
                        System.Threading.Tasks.TaskScheduler.Default);
                    server.Run(cancellation.Token);
                    bluetoothTask.Wait(3000);
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
