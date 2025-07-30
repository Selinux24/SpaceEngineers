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
        public readonly bool IsRocketBase;

        public bool ShowExchanges = true;
        public bool ShowShips = true;
        public bool ShowOrders = true;
        public bool ShowExchangeRequests = true;
        public bool EnableLogs = false;

        public readonly string BaseCamera;
        public readonly string BaseWarehouses;
        public readonly string BaseDataLCDs;
        public readonly string BaseLogLCDs;

        public readonly int ExchangeNumWaypoints;
        public readonly double ExchangePathDistance; //Meters, distance from the dock to the first waypoint
        public readonly double ExchangeDockRequestTimeThr; //Seconds
        public readonly System.Text.RegularExpressions.Regex ExchangesRegex;
        public readonly string ExchangeUpperConnector;
        public readonly string ExchangeLowerConnector;
        public readonly string ExchangeSorterInput;
        public readonly string ExchangeSorterOutput;
        public readonly string ExchangeTimerPrepare;
        public readonly string ExchangeTimerUnload;

        public readonly int RequestStatusInterval; // seconds, how often to request status from ships
        public readonly int RequestDeliveryInterval; // seconds, how often to request deliveries
        public readonly int RequestReceptionInterval; // seconds, how often to request receptions

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel");
            BaseParking = ReadConfig(customData, "Parking");
            IsRocketBase = ReadConfig(customData, "IsRocketBase").ToLower() == "true";

            ShowExchanges = ReadConfig(customData, "ShowExchanges", "true") == "true";
            ShowShips = ReadConfig(customData, "ShowShips", "true") == "true";
            ShowOrders = ReadConfig(customData, "ShowOrders", "true") == "true";
            ShowExchangeRequests = ReadConfig(customData, "ShowExchangeRequests", "true") == "true";
            EnableLogs = ReadConfig(customData, "EnableLogs", "false") == "true";

            BaseCamera = ReadConfig(customData, "Camera");
            BaseWarehouses = ReadConfig(customData, "Warehouses");

            BaseDataLCDs = ReadConfig(customData, "DataLCDs");
            BaseLogLCDs = ReadConfig(customData, "LogLCDs");

            ExchangeNumWaypoints = ReadConfigInt(customData, "ExchangeNumWaypoints");
            ExchangePathDistance = ReadConfigDouble(customData, "ExchangePathDistance");
            ExchangeDockRequestTimeThr = ReadConfigDouble(customData, "ExchangeDockRequestTimeThr");
            ExchangesRegex = new System.Text.RegularExpressions.Regex(ReadConfig(customData, "ExchangeGroupName"));
            ExchangeUpperConnector = ReadConfig(customData, "ExchangeUpperConnector");
            ExchangeLowerConnector = ReadConfig(customData, "ExchangeLowerConnector");
            ExchangeSorterInput = ReadConfig(customData, "ExchangeSorterInput");
            ExchangeSorterOutput = ReadConfig(customData, "ExchangeSorterOutput");
            ExchangeTimerPrepare = ReadConfig(customData, "ExchangeTimerPrepare");
            ExchangeTimerUnload = ReadConfig(customData, "ExchangeTimerUnload");

            RequestStatusInterval = ReadConfigInt(customData, "RequestStatusInterval");
            RequestDeliveryInterval = ReadConfigInt(customData, "RequestDeliveryInterval");
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
                "IsRocketBase=false\n" +
                "\n" +
                "ShowExchanges=true\n" +
                "ShowShips=true\n" +
                "ShowOrders=true\n" +
                "ShowExchangeRequests=true\n" +
                "EnableLogs=false\n" +
                "\n" +
                "DataLCDs=[DELIVERY_DATA]\n" +
                "LogLCDs=[DELIVERY_LOG]\n" +
                "\n" +
                "Camera=Camera\n" +
                "Warehouses=Warehouse\n" +
                "\n" +
                "ExchangeNumWaypoints=5\n" +
                "ExchangePathDistance=150\n" +
                "ExchangeDockRequestTimeThr=900\n" +
                $"ExchangeGroupName={@"GR_\w+"}\n" +
                "ExchangeUpperConnector=Input\n" +
                "ExchangeLowerConnector=Output\n" +
                "ExchangeSorterInput=Input\n" +
                "ExchangeSorterOutput=Output\n" +
                "ExchangeTimerPrepare=Prepare\n" +
                "ExchangeTimerUnload=Unload\n" +
                "\n" +
                "RequestStatusInterval=10\n" +
                "RequestDeliveryInterval=60\n" +
                "RequestReceptionInterval=60\n";
        }
    }
}
