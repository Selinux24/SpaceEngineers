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
        public readonly List<string> Listeners;
        public readonly string OutputCargo;
        public readonly string InventoryCargo;
        public readonly string TimerOpen;
        public readonly string TimerClose;
        public readonly string WildcardLCDs;

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel");
            Listeners = ReadConfigList(customData, "Listeners");
            OutputCargo = ReadConfig(customData, "OutputCargo");
            InventoryCargo = ReadConfig(customData, "InventoryCargo");
            TimerOpen = ReadConfig(customData, "TimerOpen");
            TimerClose = ReadConfig(customData, "TimerClose");
            WildcardLCDs = ReadConfig(customData, "WildcardLCDs", "[INV]");
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
        List<string> ReadConfigList(string customData, string name)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.AppendLine($"{name} not set.");
                return new List<string>();
            }
            return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
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
                "Listeners=name1,name2,name3\n" +
                "OutputCargo=name\n" +
                "InventoryCargo=name\n" +
                "TimerOpen=name\n" +
                "TimerClose=name\n" +
                "WildcardLCDs=[INV]";
        }
    }
}
