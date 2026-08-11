using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace BluetoothUnlock.Service
{
    public sealed class BluetoothUnlockWindowsService : ServiceBase
    {
        private CancellationTokenSource _cancellation;
        private Task _serverTask;

        public BluetoothUnlockWindowsService()
        {
            ServiceName = "BluetoothUnlock";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _cancellation = new CancellationTokenSource();
            var server = new PipeServer();
            _serverTask = Task.Factory.StartNew(
                () => server.Run(_cancellation.Token),
                _cancellation.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        protected override void OnStop()
        {
            _cancellation?.Cancel();
            _serverTask?.Wait(3000);
            _cancellation?.Dispose();
            _cancellation = null;
            _serverTask = null;
        }
    }
}
