using System;
using System.Linq;
using System.Text;

namespace IngameScript
{
    class Config
    {
        readonly StringBuilder errors = new StringBuilder();

        public readonly string Channel;
        public readonly bool InGravity;

        public bool ShowShips = true;
        public bool ShowExchanges = true;
        public bool ShowExchangeRequests = true;

        public bool EnableLogs = false;
        public bool EnableRequestStatus = true;
        public bool EnableRequestExchange = true;
        public bool EnableRefreshLCDs = false;

        public readonly System.Text.RegularExpressions.Regex DataLCDs;
        public readonly System.Text.RegularExpressions.Regex LogLCDs;

        public readonly int ExchangeNumWaypoints;
        public readonly double ExchangePathDistance; //Meters, distance from the dock to the first waypoint
        public readonly double ExchangeDockRequestTimeThr; //Seconds
        public readonly System.Text.RegularExpressions.Regex ExchangesRegex;
        public readonly string ExchangeMainConnector;
        public readonly string ExchangeOtherConnector;
        public readonly TimeSpan ExchangeRequestTimeOut;

        public readonly TimeSpan RequestStatusInterval; // seconds, how often to request status from ships
        public readonly TimeSpan RequestReceptionInterval; // seconds, how often to request receptions
        public readonly TimeSpan RefreshLCDsInterval; // seconds, how often to refresh LCDs

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel");
            InGravity = ReadConfigBool(customData, "InGravity");

            ShowShips = ReadConfigBool(customData, "ShowShips", true);
            ShowExchanges = ReadConfigBool(customData, "ShowExchanges", true);
            ShowExchangeRequests = ReadConfigBool(customData, "ShowExchangeRequests", true);

            EnableLogs = ReadConfigBool(customData, "EnableLogs", false);
            EnableRequestStatus = ReadConfigBool(customData, "EnableRequestStatus", true);
            EnableRequestExchange = ReadConfigBool(customData, "EnableRequestExchange", true);
            EnableRefreshLCDs = ReadConfigBool(customData, "EnableRefreshLCDs", false);

            DataLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "DataLCDs")}(?:\.(\d+))?\]");
            LogLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "LogLCDs")}(?:\.(\d+))?\]");

            ExchangeNumWaypoints = ReadConfigInt(customData, "ExchangeNumWaypoints");
            ExchangePathDistance = ReadConfigDouble(customData, "ExchangePathDistance");
            ExchangeDockRequestTimeThr = ReadConfigDouble(customData, "ExchangeDockRequestTimeThr");
            ExchangesRegex = new System.Text.RegularExpressions.Regex(ReadConfig(customData, "ExchangeGroupName"));
            ExchangeMainConnector = ReadConfig(customData, "ExchangeMainConnector");
            ExchangeOtherConnector = ReadConfig(customData, "ExchangeOtherConnector");
            ExchangeRequestTimeOut = TimeSpan.FromSeconds(ReadConfigInt(customData, "ExchangeRequestTimeOut"));

            RequestStatusInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RequestStatusInterval"));
            RequestReceptionInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RequestReceptionInterval"));
            RefreshLCDsInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RefreshLCDsInterval", 10));
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
        bool ReadConfigBool(string customData, string name, bool? defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!defaultValue.HasValue)
                {
                    errors.AppendLine($"{name} not set.");
                }

                return defaultValue ?? false;
            }

            return bool.Parse(value.Trim());
        }
        int ReadConfigInt(string customData, string name, int? defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!defaultValue.HasValue)
                {
                    errors.AppendLine($"{name} not set.");
                }

                return defaultValue ?? 0;
            }

            return int.Parse(value.Trim());
        }
        double ReadConfigDouble(string customData, string name, double? defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!defaultValue.HasValue)
                {
                    errors.AppendLine($"{name} not set.");
                }

                return defaultValue ?? 0;
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
                "InGravity=false\n" +
                "\n" +
                "ShowShips=true\n" +
                "ShowExchanges=true\n" +
                "ShowExchangeRequests=true\n" +
                "\n" +
                "EnableLogs=false\n" +
                "EnableRequestStatus=true\n" +
                "EnableRequestExchange=true\n" +
                "EnableRefreshLCDs=false\n" +
                "\n" +
                "DataLCDs=DELIVERY_DATA\n" +
                "LogLCDs=DELIVERY_LOG\n" +
                "\n" +
                "ExchangeNumWaypoints=5\n" +
                "ExchangePathDistance=150\n" +
                "ExchangeDockRequestTimeThr=900\n" +
                $"ExchangeGroupName={@"GR_\w+"}\n" +
                "ExchangeMainConnector=Input\n" +
                "ExchangeOtherConnector=Output\n" +
                "ExchangeRequestTimeOut=300\n" +
                "\n" +
                "RequestStatusInterval=30\n" +
                "RequestReceptionInterval=60\n" +
                "RefreshLCDsInterval=10";
        }
    }
}
