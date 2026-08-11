using System;
using System.IO;
using System.Threading;
using BluetoothUnlock.Shared;

namespace BluetoothUnlock.Service
{
    public sealed class BluetoothMonitor
    {
        public void Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var delaySeconds = 10;

                try
                {
                    var config = ConfigStore.Load();
                    delaySeconds = config.BluetoothProbeIntervalSeconds;

                    if (!config.BluetoothUnlockEnabled)
                    {
                        UpdateBluetoothStatus("disabled", false, null);
                    }
                    else if (string.IsNullOrWhiteSpace(config.BluetoothDeviceAddress) &&
                             string.IsNullOrWhiteSpace(config.BluetoothDeviceName))
                    {
                        UpdateBluetoothStatus("no-target", false, null);
                    }
                    else
                    {
                        var device = BluetoothDeviceScanner.FindTarget(config);
                        if (device == null)
                        {
                            UpdateBluetoothStatus("not-nearby", false, null);
                        }
                        else
                        {
                            var grantSeconds = config.BluetoothGrantSeconds;
                            ConfigStore.Update(updated =>
                            {
                                updated.BluetoothLastSeenUtc = DateTime.UtcNow;
                                updated.BluetoothLastStatus = "nearby";
                                updated.VerifiedUntilUtc = DateTime.UtcNow.AddSeconds(grantSeconds);
                            });
                            Log("Bluetooth target nearby: " + device.Name + " " + device.Address);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("Bluetooth monitor error: " + ex);
                    UpdateBluetoothStatus("error: " + ex.Message, false, null);
                }

                if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(delaySeconds)))
                {
                    return;
                }
            }
        }

        private static void UpdateBluetoothStatus(string status, bool updateSeen, DateTime? seenUtc)
        {
            ConfigStore.Update(config =>
            {
                config.BluetoothLastStatus = status;
                if (updateSeen && seenUtc.HasValue)
                {
                    config.BluetoothLastSeenUtc = seenUtc.Value;
                }
            });
        }

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(ConfigStore.ConfigDirectory);
                File.AppendAllText(
                    Path.Combine(ConfigStore.ConfigDirectory, "service.log"),
                    DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
