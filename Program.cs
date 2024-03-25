namespace GTActionSender
{
    using CsvHelper;
    using Microsoft.Extensions.Configuration;
    using Rug.Osc;
    using System;
    using System.Globalization;
    using System.Net;

    internal class CsvEntry
    {
        public char ActionType { get; set; }
        public int DeviceIndex { get; set; }
        public float Value { get; set; }
        public int Delay { get; set; }
    }

    internal class Action
    {
        public required OscMessage Message { get; set; }
        public int Delay { get; set; }
    }

    internal class Program
    {
        private static readonly string UniversalPrefix = "/avatar/parameters/";

        private static void PrintHelp()
        {
            Console.WriteLine("GTActionSender.exe --iniPath \"C:\\path\\to\\config.ini\" --headpatCsv \"C:\\path\\to\\headpat.csv\"");
            Console.WriteLine();
            Console.WriteLine("iniPath:     should be the same config.ini file that the GiggleTech OSC router uses");
            Console.WriteLine("headpatCsv:  path to a valid csv file as described in the README");
        }

        private static List<Action> ParseJsonToActions(string filepath, string maxSpeedParameter, List<string> targetList)
        {
            using StreamReader reader = new(filepath);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);

            List<Action> actions = [];
            IEnumerable<CsvEntry>? records = csv.GetRecords<CsvEntry>();
            foreach (CsvEntry entry in records)
            {
                if (entry.DeviceIndex < 0 || entry.DeviceIndex >= targetList.Count)
                    throw new Exception(string.Format("Out-of-bounds device index {0}", entry.DeviceIndex));

                string target;
                if (entry.ActionType == 's')
                    target = UniversalPrefix + maxSpeedParameter;
                else if (entry.ActionType == 'p')
                    target = UniversalPrefix + targetList[entry.DeviceIndex];
                else
                    throw new Exception(string.Format("Unrecognized ActionType {0}", entry.ActionType));

                float clampedValue = Math.Clamp(entry.Value, 0.0f, 1.0f);
                int clampedDelay = Math.Clamp(entry.Delay, 0, int.MaxValue);
                OscMessage message = new(target, clampedValue);
                actions.Add(new Action { Message = message, Delay = clampedDelay });
            }

            return actions;
        }

        private static void Main(string[] args)
        {
            IConfiguration argsConfig = new ConfigurationBuilder().AddCommandLine(args).Build();
            string iniPath = argsConfig["iniPath"] ?? string.Empty;
            string csvPath = argsConfig["headpatCsv"] ?? string.Empty;

            if (iniPath == string.Empty || csvPath == string.Empty)
            {
                PrintHelp();
                return;
            }

            Console.WriteLine("Parsing ini from {0}", iniPath);
            IConfiguration iniConfig = new ConfigurationBuilder().AddIniFile(iniPath).Build();
            IConfigurationSection iniSetupSection = iniConfig.GetRequiredSection("Setup");
            int oscPort = int.Parse(iniSetupSection["port_rx"] ?? string.Empty);
            string maxSpeedParameter = iniSetupSection["max_speed_parameter"] ?? string.Empty;
            if (maxSpeedParameter == string.Empty)
                throw new Exception("max_speed_parameter missing in config.ini");
            string targetListText = iniSetupSection["proximity_parameters_multi"] ?? string.Empty;
            if (targetListText == string.Empty)
                throw new Exception("proximity_parameters_multi missing in config.ini");
            List<string> targetList = [.. targetListText.Split(' ')];

            Console.WriteLine("Parsing csv from {0}", csvPath);
            List<Action> actions = ParseJsonToActions(csvPath, maxSpeedParameter, targetList);
            if (actions.Count == 0)
                throw new Exception("No actions parsed from csv file!");

            OscSender oscSender = new(IPAddress.Loopback, oscPort);
            oscSender.Connect();

            // send all commands parsed
            foreach (Action action in actions)
            {
                Console.WriteLine("Sending {0}", action.Message);
                oscSender.Send(action.Message);

                if (action.Delay > 0)
                {
                    Console.WriteLine("Waiting {0}ms", action.Delay);
                    Thread.Sleep(action.Delay);
                }
            }

            // send a stop command to all devices and disconnect
            Console.WriteLine("Shutting down");
            foreach (string target in targetList)
                oscSender.Send(new OscMessage(UniversalPrefix + target, 0.0f));

            oscSender.Close();
        }
    }
}
