using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BluetoothUnlock.Shared;

namespace BluetoothUnlock.Service
{
    public sealed class PipeServer
    {
        public void Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var pipe = CreatePipe())
                    {
                        pipe.WaitForConnectionAsync(cancellationToken).GetAwaiter().GetResult();
                        HandleClient(pipe);
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Log("Pipe server error: " + ex);
                    Thread.Sleep(1000);
                }
            }
        }

        private static NamedPipeServerStream CreatePipe()
        {
            var security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            return new NamedPipeServerStream(
                PipeProtocol.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                4096,
                4096,
                security);
        }

        private static void HandleClient(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
            {
                var commandLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    writer.Write("ERR empty-command\nEND\n");
                    return;
                }

                var parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToUpperInvariant();

                if (command == "GETCRED")
                {
                    HandleGetCredential(writer);
                    return;
                }

                if (command == "GRANT" && parts.Length >= 2 && int.TryParse(parts[1], out var seconds))
                {
                    HandleGrant(writer, seconds);
                    return;
                }

                if (command == "STATUS")
                {
                    HandleStatus(writer);
                    return;
                }

                if (command == "CANUNLOCK")
                {
                    HandleCanUnlock(writer);
                    return;
                }

                writer.Write("ERR unknown-command\nEND\n");
            }
        }

        private static void HandleGetCredential(StreamWriter writer)
        {
            UnlockConfig config;
            try
            {
                config = ConfigStore.Load();
            }
            catch (Exception ex)
            {
                Log("Load config failed: " + ex);
                writer.Write("ERR config-load-failed\nEND\n");
                return;
            }

            if (!config.HasCredential)
            {
                writer.Write("ERR missing-credential\nEND\n");
                return;
            }

            if (!IsVerified(config))
            {
                writer.Write("ERR not-verified\nEND\n");
                return;
            }

            PlainCredential credential;
            try
            {
                credential = ConfigStore.GetCredential(config);
            }
            catch (Exception ex)
            {
                Log("Unprotect credential failed: " + ex);
                writer.Write("ERR credential-unprotect-failed\nEND\n");
                return;
            }

            writer.Write(PipeProtocol.FormatCredential(credential));
        }

        private static void HandleGrant(StreamWriter writer, int seconds)
        {
            try
            {
                if (seconds < 1 || seconds > 300)
                {
                    writer.Write("ERR invalid-grant-window\nEND\n");
                    return;
                }

                var config = ConfigStore.Load();
                config.VerifiedUntilUtc = DateTime.UtcNow.AddSeconds(seconds);
                ConfigStore.Save(config);
                writer.Write("OK\nEND\n");
            }
            catch (Exception ex)
            {
                Log("Grant failed: " + ex);
                writer.Write("ERR grant-failed\nEND\n");
            }
        }

        private static void HandleStatus(StreamWriter writer)
        {
            try
            {
                var config = ConfigStore.Load();
                writer.Write("OK\n");
                writer.Write("hasCredential:" + (config.HasCredential ? "1" : "0") + "\n");
                writer.Write("mode:" + config.VerifierMode + "\n");
                writer.Write("autoSubmit:" + (config.AutoSubmitOnVerified ? "1" : "0") + "\n");
                writer.Write("bluetoothEnabled:" + (config.BluetoothUnlockEnabled ? "1" : "0") + "\n");
                writer.Write("bluetoothDeviceName:" + config.BluetoothDeviceName + "\n");
                writer.Write("bluetoothDeviceAddress:" + BluetoothAddress.FormatWithSeparators(config.BluetoothDeviceAddress) + "\n");
                writer.Write("bluetoothProbeIntervalSeconds:" + config.BluetoothProbeIntervalSeconds + "\n");
                writer.Write("bluetoothGrantSeconds:" + config.BluetoothGrantSeconds + "\n");
                writer.Write("bluetoothLastSeenUtc:" + config.BluetoothLastSeenUtc.ToString("O") + "\n");
                writer.Write("bluetoothLastStatus:" + config.BluetoothLastStatus + "\n");
                writer.Write("verifiedUntilUtc:" + config.VerifiedUntilUtc.ToString("O") + "\n");
                writer.Write("verifiedNow:" + (IsVerified(config) ? "1" : "0") + "\n");
                writer.Write("END\n");
            }
            catch (Exception ex)
            {
                Log("Status failed: " + ex);
                writer.Write("ERR status-failed\nEND\n");
            }
        }

        private static void HandleCanUnlock(StreamWriter writer)
        {
            try
            {
                var config = ConfigStore.Load();
                if (config.HasCredential && config.AutoSubmitOnVerified && IsVerified(config))
                {
                    writer.Write("OK\nEND\n");
                }
                else
                {
                    writer.Write("ERR not-ready\nEND\n");
                }
            }
            catch (Exception ex)
            {
                Log("CanUnlock failed: " + ex);
                writer.Write("ERR canunlock-failed\nEND\n");
            }
        }

        private static bool IsVerified(UnlockConfig config)
        {
            if (config.VerifierMode == VerifierMode.AlwaysAllowTest)
            {
                return true;
            }

            return config.VerifiedUntilUtc > DateTime.UtcNow;
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
