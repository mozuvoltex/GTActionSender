namespace GTActionSender
{
    using CsvHelper;
    using Microsoft.Extensions.Configuration;
    using Rug.Osc;
    using System;
    using System.Globalization;
    using System.Net;

    /*
     * TODO:
     * input validation
     * error checking
     */

    internal class JsonEntry
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

        private static List<Action> ParseJsonToActions(string filepath, string maxSpeedParameter, List<string> targetList)
        {
            using StreamReader reader = new(filepath);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);

            List<Action> actions = [];
            IEnumerable<JsonEntry>? records = csv.GetRecords<JsonEntry>();
            foreach (JsonEntry entry in records)
            {
                string target;
                if (entry.ActionType == 's')
                    target = UniversalPrefix + maxSpeedParameter;
                else if (entry.ActionType == 'p')
                    target = UniversalPrefix + targetList[entry.DeviceIndex];
                else
                    return [];

                float clampedValue = Math.Clamp(entry.Value, 0.0f, 1.0f);
                OscMessage message = new(target, clampedValue);
                actions.Add(new Action { Message = message, Delay = entry.Delay });
            }

            return actions;
        }

        private static void Main(string[] args)
        {
            IConfiguration argsConfig = new ConfigurationBuilder().AddCommandLine(args).Build();
            string iniPath = argsConfig["iniPath"] ?? string.Empty;
            string csvPath = argsConfig["headpatCsv"] ?? string.Empty;

            Console.WriteLine("Parsing ini from {0}", iniPath);
            IConfiguration iniConfig = new ConfigurationBuilder().AddIniFile(iniPath).Build();
            IConfigurationSection iniSetupSection = iniConfig.GetRequiredSection("Setup");
            int oscPort = int.Parse(iniSetupSection["port_rx"] ?? string.Empty);
            string maxSpeedParameter = iniSetupSection["max_speed_parameter"] ?? string.Empty;
            string targetListText = iniSetupSection["proximity_parameters_multi"] ?? string.Empty;          
            List<string> targetList = [.. targetListText.Split(' ')];

            Console.WriteLine("Parsing csv from {0}", csvPath);
            List<Action> actions = ParseJsonToActions(csvPath, maxSpeedParameter, targetList);
            if (actions.Count == 0)
            {
                Console.WriteLine("No instructions read!");
                return;
            }

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
