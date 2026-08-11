using System;
using System.Threading;

namespace BluetoothUnlock.Service
{
    internal static class BluetoothProbeCoordinator
    {
        private static readonly AutoResetEvent ProbeRequested = new AutoResetEvent(false);

        public static void RequestProbe()
        {
            ProbeRequested.Set();
        }

        public static bool WaitForCancellationOrProbe(CancellationToken cancellationToken, TimeSpan timeout)
        {
            var index = WaitHandle.WaitAny(
                new[] { cancellationToken.WaitHandle, ProbeRequested },
                timeout);
            return index == 0;
        }
    }
}
