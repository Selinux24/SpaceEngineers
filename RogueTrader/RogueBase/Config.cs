using System;
using System.Linq;
using System.Text;

namespace IngameScript
{
    class Config
    {
        readonly StringBuilder errors = new StringBuilder();

        public readonly string Channel;
        public readonly string BaseParking;
        public readonly bool InGravity;

        public bool ShowShips = true;
        public bool ShowExchanges = true;
        public bool ShowExchangeRequests = true;
        public bool EnableLogs = false;

        public readonly string BaseCamera;

        public readonly string BaseDataLCDs;
        public readonly string BaseLogLCDs;

        public readonly int ExchangeNumWaypoints;
        public readonly double ExchangePathDistance; //Meters, distance from the dock to the first waypoint
        public readonly double ExchangeDockRequestTimeThr; //Seconds
        public readonly System.Text.RegularExpressions.Regex ExchangesRegex;
        public readonly string ExchangeMainConnector;
        public readonly string ExchangeOtherConnector;

        public readonly int RequestStatusInterval; // seconds, how often to request status from ships
        public readonly int RequestReceptionInterval; // seconds, how often to request receptions

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel");
            BaseParking = ReadConfig(customData, "Parking");
            InGravity = ReadConfig(customData, "InGravity").ToLower() == "true";

            ShowShips = ReadConfig(customData, "ShowShips", "true") == "true";
            ShowExchanges = ReadConfig(customData, "ShowExchanges", "true") == "true";
            ShowExchangeRequests = ReadConfig(customData, "ShowExchangeRequests", "true") == "true";
            EnableLogs = ReadConfig(customData, "EnableLogs", "false") == "true";

            BaseDataLCDs = ReadConfig(customData, "DataLCDs");
            BaseLogLCDs = ReadConfig(customData, "LogLCDs");

            BaseCamera = ReadConfig(customData, "Camera");

            ExchangeNumWaypoints = ReadConfigInt(customData, "ExchangeNumWaypoints");
            ExchangePathDistance = ReadConfigDouble(customData, "ExchangePathDistance");
            ExchangeDockRequestTimeThr = ReadConfigDouble(customData, "ExchangeDockRequestTimeThr");
            ExchangesRegex = new System.Text.RegularExpressions.Regex(ReadConfig(customData, "ExchangeGroupName"));
            ExchangeMainConnector = ReadConfig(customData, "ExchangeMainConnector");
            ExchangeOtherConnector = ReadConfig(customData, "ExchangeOtherConnector");

            RequestStatusInterval = ReadConfigInt(customData, "RequestStatusInterval");
            RequestReceptionInterval = ReadConfigInt(customData, "RequestReceptionInterval");
        }
        string ReadConfig(string customData, string name, string defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (defaultValue != null)
                {
                    return defaultValue;
                }

                errors.AppendLine($"{name} not set.");
            }

            return value;
        }
        int ReadConfigInt(string customData, string name, int defaultValue = 0)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.AppendLine($"{name} not set.");

                return defaultValue;
            }

            return int.Parse(value);
        }
        double ReadConfigDouble(string customData, string name, double defaultValue = 0)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.AppendLine($"{name} not set.");

                return defaultValue;
            }

            return double.Parse(value.Trim());
        }
        static string ReadConfigLine(string customData, string name)
        {
            string[] lines = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }

        public bool IsValid()
        {
            return errors.Length == 0;
        }
        public string GetErrors()
        {
            return errors.ToString();
        }

        public static string GetDefault()
        {
            return
                "Channel=name\n" +
                "Parking=x:y:z\n" +
                "InGravity=false\n" +
                "\n" +
                "ShowShips=true\n" +
                "ShowExchanges=true\n" +
                "ShowExchangeRequests=true\n" +
                "EnableLogs=false\n" +
                "\n" +
                "DataLCDs=[DELIVERY_DATA]\n" +
                "LogLCDs=[DELIVERY_LOG]\n" +
                "\n" +
                "Camera=Camera\n" +
                "\n" +
                "ExchangeNumWaypoints=5\n" +
                "ExchangePathDistance=150\n" +
                "ExchangeDockRequestTimeThr=900\n" +
                $"ExchangeGroupName={@"GR_\w+"}\n" +
                "ExchangeMainConnector=Input\n" +
                "ExchangeOtherConnector=Output\n" +
                "\n" +
                "RequestStatusInterval=10\n" +
                "RequestReceptionInterval=60\n";
        }
    }
}
