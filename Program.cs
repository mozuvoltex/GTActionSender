namespace GTActionSender
{
    using CsvHelper;
    using Microsoft.Extensions.Configuration;
    using Rug.Osc;
    using System.Globalization;
    using System.Net;

    /*
     * TODO:
     * multi-target support via targetList
     * input validation
     * error checking
     */

    internal readonly struct Instructions(float maxSpeed, List<Tuple<float, uint>> actions)
    {
        public readonly float m_maxSpeed = maxSpeed;
        public readonly List<Tuple<float, uint>> m_actions = actions;
    }

    internal class Program
    {
        private static Instructions ParseInstructions(string filepath)
        {
            List<Tuple<float, uint>> actions = [];

            using StreamReader reader = new(filepath);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();

            // maxSpeed
            csv.Read();
            float maxSpeed = Math.Clamp(csv.GetField<float>(0), 0.0f, 1.0f);

            // actions
            while (csv.Read())
            {
                float intensity = Math.Clamp(csv.GetField<float>(0), 0.0f, 1.0f);
                uint duration = csv.GetField<uint>(1);
                actions.Add(new(intensity, duration));
            }

            return new(maxSpeed, actions);
        }

        private static void Main(string[] args)
        {
            IConfiguration argsConfig = new ConfigurationBuilder().AddCommandLine(args).Build();
            string iniPath = argsConfig["iniPath"] ?? string.Empty;
            string csvPath = argsConfig["headpatCsv"] ?? string.Empty;

            IConfiguration iniConfig = new ConfigurationBuilder().AddIniFile(iniPath).Build();
            IConfigurationSection iniSetupSection = iniConfig.GetRequiredSection("Setup");
            int oscPort = int.Parse(iniSetupSection["port_rx"] ?? string.Empty);
            string targetListText = iniSetupSection["proximity_parameters_multi"] ?? string.Empty;
            string maxSpeedParameter = iniSetupSection["max_speed_parameter"] ?? string.Empty;
            List<string> targetList = [.. targetListText.Split(' ')];
            string oscTarget = string.Format("/avatar/parameters/{0}", targetList[0]);

            Instructions instructions = ParseInstructions(csvPath);
            if (instructions.m_actions.Count == 0)
            {
                Console.WriteLine("No instructions read!");
                return;
            }

            OscSender m_Sender = new(IPAddress.Loopback, oscPort);
            m_Sender.Connect();

            // send max speed command
            Console.WriteLine("Setting {0} to {1:P0}", maxSpeedParameter, instructions.m_maxSpeed);
            string maxSpeedTarget = string.Format("/avatar/parameters/{0}", maxSpeedParameter);
            m_Sender.Send(new OscMessage(maxSpeedTarget, instructions.m_maxSpeed));

            // send actions
            foreach (Tuple<float, uint> action in instructions.m_actions)
            {
                Console.WriteLine("Sending intensity {0} for {1} milliseconds", action.Item1, action.Item2);
                m_Sender.Send(new OscMessage(oscTarget, action.Item1));
                Thread.Sleep((int)action.Item2);
            }

            // send an off command and disconnect
            Console.WriteLine("Shutting down");
            m_Sender.Send(new OscMessage(oscTarget, 0.0f));
            m_Sender.Close();
        }
    }
}
