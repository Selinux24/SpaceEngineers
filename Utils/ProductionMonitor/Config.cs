using System;
using System.Linq;
using System.Text;

namespace IngameScript
{
    class Config
    {
        readonly StringBuilder errors = new StringBuilder();

        public readonly string AssemblerName;

        public readonly string TimerOn;
        public readonly string TimerOff;

        public readonly bool UseAllGrids;

        public Config(string customData)
        {
            AssemblerName = ReadConfig(customData, "AssemblerName", AssemblerName);

            TimerOn = ReadConfig(customData, "TimerOn");
            TimerOff = ReadConfig(customData, "TimerOff");

            UseAllGrids = ReadConfigBool(customData, "UseAllGrids");
        }
        string ReadConfig(string customData, string name, string defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (defaultValue == null)
                {
                    errors.AppendLine($"{name} not set.");
                }

                return defaultValue;
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
                "AssemblerName=Assembler\n" +
                "\n" +
                "TimerOn=Timer On\n" +
                "TimerOff=Timer Off\n" +
                "\n" +
                "UseAllGrids=false";
        }
    }
}
