using System;
using System.Linq;
using System.Text;

namespace IngameScript
{
    class Config
    {
        readonly StringBuilder errors = new StringBuilder();

        public readonly string Channel;

        public bool EnableLogs = true;

        public readonly System.Text.RegularExpressions.Regex WildcardShipInfo;
        public readonly System.Text.RegularExpressions.Regex WildcardLogLCDs;

        public readonly string ShipTimerDock;
        public readonly string ShipRemoteControlDock;
        public readonly string ShipConnectorDock;

        public readonly double GyrosThr; //Alignment accuracy
        public readonly double GyrosSpeed; //Gyroscope speed

        public readonly int AlignTicks;
        public readonly double AlignExchangeApproachingSpeed; //Maximum approach speed to the connector
        public readonly double AlignExchangeSlowdownDistance = 50.0; //Braking distance to the first point of the connector
        public readonly double AlignExchangeDistanceThr; //Accuracy of approximation to the first point of the connector
        public readonly double AlignSpeedWaypointFirst = 10.0; //Maximum speed at first waypoint
        public readonly double AlignSpeedWaypoints = 5.0; //Maximum speed between waypoints
        public readonly double AlignSpeedWaypointLast = 1.0; //Maximum speed at the last waypoint
        public readonly double AlignDistanceThrWaypoints; //Approximation accuracy between waypoints

        public Config(string customData)
        {
            Channel = Utils.ReadConfig(customData, "Channel");

            EnableLogs = ReadConfig(customData, "EnableLogs", "false") == "true";

            WildcardShipInfo = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardShipInfo")}(?:\.(\d+))?\]");
            WildcardLogLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardLogLCDs")}(?:\.(\d+))?\]");

            ShipTimerDock = ReadConfig(customData, "ShipTimerDock", "");
            ShipRemoteControlDock = ReadConfig(customData, "ShipRemoteControlDock");
            ShipConnectorDock = ReadConfig(customData, "ShipConnectorDock");

            GyrosThr = ReadConfigDouble(customData, "GyrosThr");
            GyrosSpeed = ReadConfigDouble(customData, "GyrosSpeed");

            AlignTicks = ReadConfigInt(customData, "AlignTicks");
            AlignExchangeApproachingSpeed = ReadConfigDouble(customData, "AlignExchangeApproachingSpeed");
            AlignExchangeSlowdownDistance = ReadConfigDouble(customData, "AlignExchangeSlowdownDistance");
            AlignExchangeDistanceThr = ReadConfigDouble(customData, "AlignExchangeDistanceThr");
            AlignSpeedWaypointFirst = ReadConfigDouble(customData, "AlignSpeedWaypointFirst");
            AlignSpeedWaypoints = ReadConfigDouble(customData, "AlignSpeedWaypoints");
            AlignSpeedWaypointLast = ReadConfigDouble(customData, "AlignSpeedWaypointLast");
            AlignDistanceThrWaypoints = ReadConfigDouble(customData, "AlignDistanceThrWaypoints");
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
        int ReadConfigInt(string customData, string name, int? defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!defaultValue.HasValue)
                {
                    errors.AppendLine($"{name} not set.");
                }

                return 0;
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

                return 0;
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
                "\n" +
                "EnableLogs=false\n" +
                "\n" +
                "WildcardShipInfo=[DOCK_INFO]\n" +
                "WildcardLogLCDs=[DOCK_LOG]\n" +
                "\n" +
                "ShipTimerDock=Timer Block Docking\n" +
                "ShipRemoteControlDock=Remote Control Docking\n" +
                "ShipConnectorDock=Connector Docking\n" +
                "\n" +
                "GyrosThr=0.001\n" +
                "GyrosSpeed=2.0\n" +
                "\n" +
                "AlignTicks=1\n" +
                "AlignExchangeApproachingSpeed=5\n" +
                "AlignExchangeSlowdownDistance=50.0\n" +
                "AlignExchangeDistanceThr=200.0\n" +
                "AlignSpeedWaypointFirst=10.0\n" +
                "AlignSpeedWaypoints=5.0\n" +
                "AlignSpeedWaypointLast=1.0\n" +
                "AlignDistanceThrWaypoints=0.5\n";
        }
    }
}
