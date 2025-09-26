using System;
using System.Collections.Generic;
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
        public bool ShowPlans = false;

        public bool EnableLogs = false;
        public bool EnableRequestStatus = true;
        public bool EnableRequestExchange = true;
        public bool EnableRefreshLCDs = false;

        public readonly System.Text.RegularExpressions.Regex DataLCDs;
        public readonly System.Text.RegularExpressions.Regex LogLCDs;

        public readonly List<ExchangeConfig> Exchanges = new List<ExchangeConfig>();

        public readonly TimeSpan ExchangeRequestTimeOut;
        public readonly string ExchangeMainConnector;
        public readonly string ExchangeOtherConnector;
        public readonly string ExchangeTimerLoad;
        public readonly string ExchangeTimerUnload;
        public readonly string ExchangeTimerFree;

        public readonly TimeSpan RequestStatusInterval; // seconds, how often to request status from ships
        public readonly TimeSpan RequestReceptionInterval; // seconds, how often to request receptions
        public readonly TimeSpan RefreshLCDsInterval; // seconds, how often to refresh LCDs

        public readonly double DockRequestMaxDistance; // meters, max distance from the dock to request docking

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel");
            InGravity = ReadConfigBool(customData, "InGravity");

            ShowShips = ReadConfigBool(customData, "ShowShips", true);
            ShowExchanges = ReadConfigBool(customData, "ShowExchanges", true);
            ShowExchangeRequests = ReadConfigBool(customData, "ShowExchangeRequests", true);
            ShowPlans = ReadConfigBool(customData, "ShowPlans", false);

            EnableLogs = ReadConfigBool(customData, "EnableLogs", false);
            EnableRequestStatus = ReadConfigBool(customData, "EnableRequestStatus", true);
            EnableRequestExchange = ReadConfigBool(customData, "EnableRequestExchange", true);
            EnableRefreshLCDs = ReadConfigBool(customData, "EnableRefreshLCDs", false);

            DataLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "DataLCDs")}(?:\.(\d+))?\]");
            LogLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "LogLCDs")}(?:\.(\d+))?\]");

            Exchanges = ReadExchanges(customData, "Exchanges");
            ExchangeRequestTimeOut = TimeSpan.FromSeconds(ReadConfigInt(customData, "ExchangeRequestTimeOut"));
            ExchangeMainConnector = ReadConfig(customData, "ExchangeMainConnector");
            ExchangeOtherConnector = ReadConfig(customData, "ExchangeOtherConnector");
            ExchangeTimerLoad = ReadConfig(customData, "ExchangeTimerLoad", "Timer Load");
            ExchangeTimerUnload = ReadConfig(customData, "ExchangeTimerUnload", "Timer Unload");
            ExchangeTimerFree = ReadConfig(customData, "ExchangeTimerFree", "Timer Free");

            RequestStatusInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RequestStatusInterval"));
            RequestReceptionInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RequestReceptionInterval"));
            RefreshLCDsInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RefreshLCDsInterval", 10));
      
            DockRequestMaxDistance = ReadConfigDouble(customData, "DockRequestMaxDistance", 2000);
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
        static List<ExchangeConfig> ReadExchanges(string customData, string name)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<ExchangeConfig>();
            }

            var exchanges = new List<ExchangeConfig>();
            var lines = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            foreach (var line in lines)
            {
                ExchangeConfig ex;
                if (!ExchangeConfig.Read(line, out ex)) continue;
                exchanges.Add(ex);
            }
            return exchanges;
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
                "ShowPlans=false\n" +
                "\n" +
                "EnableLogs=false\n" +
                "EnableRequestStatus=true\n" +
                "EnableRequestExchange=true\n" +
                "EnableRefreshLCDs=false\n" +
                "\n" +
                "DataLCDs=DELIVERY_DATA\n" +
                "LogLCDs=DELIVERY_LOG\n" +
                "\n" +
                "Exchanges=type1:5:150,type2:5:150,type3:5:150\n" +
                "ExchangeRequestTimeOut=300\n" +
                "ExchangeMainConnector=Input\n" +
                "ExchangeOtherConnector=Output\n" +
                "ExchangeTimerLoad=Timer Load\n" +
                "ExchangeTimerUnload=Timer Unload\n" +
                "ExchangeTimerFree=Timer Free\n" +
                "\n" +
                "RequestStatusInterval=30\n" +
                "RequestReceptionInterval=60\n" +
                "RefreshLCDsInterval=10\n" +
                "\n" +
                "DockRequestMaxDistance=2000.0\n";
        }
    }
}
