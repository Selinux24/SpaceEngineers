using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    class Config
    {
        readonly StringBuilder errors = new StringBuilder();

        public readonly string Channel;

        public bool EnableLogs = true;

        public readonly string WildcardShipId;
        public readonly string WildcardShipInfo;
        public readonly string WildcardLogLCDs;

        public readonly string TimerPilot;
        public readonly string TimerLock;
        public readonly string TimerUnlock;
        public readonly string TimerLoad;
        public readonly string TimerUnload;
        public readonly string TimerWaiting;
        public readonly string RemoteControlPilot;
        public readonly string CameraPilot;
        public readonly string RemoteControlAlign;
        public readonly string RemoteControlLanding;
        public readonly string ConnectorA;
        public readonly string Antenna;

        public readonly double MaxLoad;
        public readonly double MinLoad;

        public readonly Route Route;

        public readonly int NavigationTicks;
        public readonly double DockingSpeedWaypointFirst;
        public readonly double DockingSpeedWaypointLast;
        public readonly double DockingSpeedWaypoints;
        public readonly double DockingSlowdownDistance;
        public readonly double DockingDistanceThrWaypoints;

        public readonly double GyrosThr;
        public readonly double GyrosSpeed;

        public Config(string customData)
        {
            Channel = Utils.ReadConfig(customData, "Channel");

            EnableLogs = ReadConfigBool(customData, "EnableLogs");

            WildcardShipId = ReadConfig(customData, "WildcardShipId");
            WildcardShipInfo = ReadConfig(customData, "WildcardShipInfo");
            WildcardLogLCDs = ReadConfig(customData, "WildcardLogLCDs");

            TimerPilot = ReadConfig(customData, "TimerPilot", "");
            TimerLock = ReadConfig(customData, "TimerLock");
            TimerUnlock = ReadConfig(customData, "TimerUnlock");
            TimerLoad = ReadConfig(customData, "TimerLoad", "");
            TimerUnload = ReadConfig(customData, "TimerUnload", "");
            TimerWaiting = ReadConfig(customData, "TimerWaiting");
            RemoteControlPilot = ReadConfig(customData, "RemoteControlPilot");
            CameraPilot = ReadConfig(customData, "CameraPilot");
            RemoteControlAlign = ReadConfig(customData, "RemoteControlAlign");
            RemoteControlLanding = ReadConfig(customData, "RemoteControlLanding");
            ConnectorA = ReadConfig(customData, "ConnectorA");
            Antenna = ReadConfig(customData, "Antenna");

            MaxLoad = ReadConfigDouble(customData, "MaxLoad", 1);
            MinLoad = ReadConfigDouble(customData, "MinLoad", 0);

            Route = new Route(
                ReadConfig(customData, "RouteLoadBase"),
                ReadConfig(customData, "RouteUnloadBase"),
                ReadConfigVectorList(customData, "RouteToLoadBaseWaypoints", new List<Vector3D>()),
                ReadConfigVectorList(customData, "RouteToUnloadBaseWaypoints", new List<Vector3D>()));
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
        List<Vector3D> ReadConfigVectorList(string customData, string name, List<Vector3D> defaultValue = null)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (defaultValue == null)
                {
                    errors.AppendLine($"{name} not set.");
                }

                return new List<Vector3D>();
            }

            return Utils.StrToVectorList(value.Trim());
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
                "WildcardShipId=[shipId]\n" +
                "WildcardShipInfo=[DELIVERY_INFO]\n" +
                "WildcardLogLCDs=[DELIVERY_LOG]\n" +
                "\n" +
                "TimerPilot=Timer Block Pilot\n" +
                "TimerLock=Timer Block Locking\n" +
                "TimerUnlock=Timer Block Unlocking\n" +
                "TimerLoad=Timer Block Load\n" +
                "TimerUnload=Timer Block Unload\n" +
                "TimerWaiting=Timer Block Waiting\n" +
                "RemoteControlPilot=Remote Control Pilot\n" +
                "CameraPilot=Camera Pilot\n" +
                "RemoteControlAlign=Remote Control Locking\n" +
                "RemoteControlLanding=Remote Control Landing\n" +
                "ConnectorA=Connector A\n" +
                "Antenna=Compact Antenna\n" +
                "\n" +
                "MaxLoad=1\n" +
                "MinLoad=0\n" +
                "\n" +
                "RouteLoadBase=base1\n" +
                "RouteUnloadBase=base2\n" +
                "RouteToLoadBaseWaypoints=x:y:z;x:y:z\n" +
                "RouteToUnloadBaseWaypoints=x:y:z;x:y:z\n";
        }
    }
}
