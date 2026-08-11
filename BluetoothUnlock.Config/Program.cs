using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using BluetoothUnlock.Shared;

namespace BluetoothUnlock.Config
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || HasArg(args, "--help") || HasArg(args, "-h"))
                {
                    PrintUsage();
                    return 0;
                }

                var command = args[0].ToLowerInvariant();
                var options = ParseOptions(args);

                switch (command)
                {
                    case "set-credential":
                        return SetCredential(options);
                    case "set-mode":
                        return SetMode(options);
                    case "grant":
                        return Grant(options);
                    case "status":
                        return Status();
                    case "clear":
                        ConfigStore.Save(new UnlockConfig());
                        Console.WriteLine("Configuration cleared.");
                        return 0;
                    default:
                        Console.Error.WriteLine("Unknown command: " + args[0]);
                        PrintUsage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int SetCredential(Dictionary<string, string> options)
        {
            var username = Require(options, "username");
            var password = Require(options, "password");
            options.TryGetValue("domain", out var domain);

            ConfigStore.SetCredential(domain, username, password);
            Console.WriteLine("Credential saved to " + ConfigStore.ConfigPath);
            return 0;
        }

        private static int SetMode(Dictionary<string, string> options)
        {
            var modeText = Require(options, "mode");
            if (!Enum.TryParse<VerifierMode>(modeText, true, out var mode))
            {
                throw new ArgumentException("Invalid mode. Use ManualTtl or AlwaysAllowTest.");
            }

            var config = ConfigStore.Load();
            config.VerifierMode = mode;
            ConfigStore.Save(config);
            Console.WriteLine("Mode set to " + mode + ".");
            return 0;
        }

        private static int Grant(Dictionary<string, string> options)
        {
            var seconds = 30;
            if (options.TryGetValue("seconds", out var value) && !int.TryParse(value, out seconds))
            {
                throw new ArgumentException("--seconds must be an integer.");
            }

            var response = SendPipeCommand("GRANT " + seconds);
            Console.Write(response);
            return response.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }

        private static int Status()
        {
            try
            {
                Console.Write(SendPipeCommand("STATUS"));
            }
            catch (IOException)
            {
                var config = ConfigStore.Load();
                Console.WriteLine("Service pipe unavailable.");
                Console.WriteLine("Config path: " + ConfigStore.ConfigPath);
                Console.WriteLine("Has credential: " + config.HasCredential);
                Console.WriteLine("Mode: " + config.VerifierMode);
                Console.WriteLine("Verified until UTC: " + config.VerifiedUntilUtc.ToString("O"));
            }

            return 0;
        }

        private static string SendPipeCommand(string command)
        {
            using (var pipe = new NamedPipeClientStream(".", PipeProtocol.PipeName, PipeDirection.InOut))
            {
                pipe.Connect(3000);
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                {
                    writer.WriteLine(command);
                    var builder = new StringBuilder();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        builder.AppendLine(line);
                        if (line == "END")
                        {
                            break;
                        }
                    }

                    return builder.ToString();
                }
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < args.Length; i++)
            {
                var key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                key = key.Substring(2);
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[key] = "true";
                }
                else
                {
                    options[key] = args[++i];
                }
            }

            return options;
        }

        private static string Require(Dictionary<string, string> options, string key)
        {
            if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Missing --" + key + ".");
            }

            return value;
        }

        private static bool HasArg(string[] args, string value)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("BluetoothUnlock.Config");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  set-credential --domain . --username alice --password secret");
            Console.WriteLine("  set-mode --mode ManualTtl");
            Console.WriteLine("  set-mode --mode AlwaysAllowTest");
            Console.WriteLine("  grant --seconds 30");
            Console.WriteLine("  status");
            Console.WriteLine("  clear");
        }
    }
}
