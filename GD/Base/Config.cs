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

        public readonly string BaseCamera = "Camera";
        public readonly string BaseWarehouses = "Warehouse";
        public readonly string BaseDataLCDs = "[DELIVERY_DATA]";
        public readonly string BaseLogLCDs = "[DELIVERY_LOG]";

        public readonly System.Text.RegularExpressions.Regex ExchangesRegex = new System.Text.RegularExpressions.Regex(@"GR_\w+");
        public readonly string ExchangeUpperConnector = "Input";
        public readonly string ExchangeLowerConnector = "Output";
        public readonly string ExchangeSorterInput = "Input";
        public readonly string ExchangeSorterOutput = "Output";
        public readonly string ExchangeTimerPrepare = "Prepare";
        public readonly string ExchangeTimerUnload = "Unload";

        public readonly int RequestStatusInterval = 10; // seconds, how often to request status from ships
        public readonly int RequestDeliveryInterval = 60; // seconds, how often to request deliveries
        public readonly int RequestReceptionInterval = 60; // seconds, how often to request receptions

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel");
            BaseParking = ReadConfig(customData, "Parking");
            IsRocketBase = ReadConfig(customData, "IsRocketBase").ToLower() == "true";

            BaseCamera = ReadConfig(customData, "Camera");
            BaseWarehouses = ReadConfig(customData, "Warehouses");

            BaseDataLCDs = ReadConfig(customData, "DataLCDs");
            BaseLogLCDs = ReadConfig(customData, "LogLCDs");

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
        string ReadConfig(string customData, string name)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
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
                "DataLCDs=[DELIVERY_DATA]\n" +
                "LogLCDs=[DELIVERY_LOG]\n" +
                "\n" +
                "Camera=Camera\n" +
                "Warehouses=Warehouse\n" +
                "\n" +
                @"ExchangeGroupName=GR_\w+" + "\n" +
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
