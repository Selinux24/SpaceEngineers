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
        public readonly List<Route> Routes = new List<Route>();

        public readonly string OutputCargo;
        public readonly string InventoryCargo;
        public readonly string TimerOpen;
        public readonly string TimerClose;
        public readonly string Connector;

        public readonly string WildcardLCDs;

        public Config(string customData)
        {
            Channel = ReadConfig(customData, "Channel", true);

            Listeners = ReadConfigList(customData, "Listeners", true);
            Routes = ReadRoutes(customData, "Routes");

            OutputCargo = ReadConfig(customData, "OutputCargo");
            InventoryCargo = ReadConfig(customData, "InventoryCargo");
            TimerOpen = ReadConfig(customData, "TimerOpen");
            TimerClose = ReadConfig(customData, "TimerClose");
            Connector = ReadConfig(customData, "Connector", true);

            WildcardLCDs = ReadConfig(customData, "WildcardLCDs", true, "[INV]");
        }
        string ReadConfig(string customData, string name, bool required = false, string defaultValue = null)
        {
            var value = ReadConfigLine(customData, name, required, defaultValue);
            if (string.IsNullOrWhiteSpace(value) && required)
            {
                return null;
            }

            return value;
        }
        List<string> ReadConfigList(string customData, string name, bool required = false)
        {
            var value = ReadConfigLine(customData, name, required);
            if (string.IsNullOrWhiteSpace(value) && required)
            {
                return new List<string>();
            }

            return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }
        string ReadConfigLine(string customData, string name, bool required = false, string defaultValue = null)
        {
            string[] lines = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? defaultValue;
            if (string.IsNullOrWhiteSpace(value) && required)
            {
                errors.AppendLine($"{name} not set.");
            }

            return value;
        }
        List<Route> ReadRoutes(string customData, string name)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<Route>();
            }

            var routes = new List<Route>();
            var lines = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            foreach (var line in lines)
            {
                Route r;
                if (!Route.Read(line, out r)) continue;
                routes.Add(r);
            }
            return routes;
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
                "\n" +
                "Listeners=name1,name2,name3\n" +
                "Routes=Name=name|LoadBase=name|LoadBaseOnPlanet=false|ToLoadBaseWaypoints=x0:y0:z0;x1:y1:z1;xN:yN:zN|UnloadBase=name|UnloadBaseOnPlanet=false|ToUnloadBaseWaypoints=x0:y0:z0;x1:y1:z1;xN:yN:zN,Name=name|LoadBase=name|LoadBaseOnPlanet=false|ToLoadBaseWaypoints=x0:y0:z0;x1:y1:z1;xN:yN:zN|UnloadBase=name|UnloadBaseOnPlanet=false|ToUnloadBaseWaypoints=x0:y0:z0;x1:y1:z1;xN:yN:zN\n" +
                "\n" +
                "OutputCargo=name\n" +
                "InventoryCargo=name\n" +
                "TimerOpen=name\n" +
                "TimerClose=name\n" +
                "Connector=name\n" +
                "\n" +
                "WildcardLCDs=[INV]\n";
        }
    }
}
