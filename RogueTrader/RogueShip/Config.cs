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
        public bool EnableRefreshLCDs = false;

        public readonly System.Text.RegularExpressions.Regex WildcardShipInfo;
        public readonly System.Text.RegularExpressions.Regex WildcardLogLCDs;

        public readonly string TimerPilot;
        public readonly string TimerWaiting;
        public readonly string TimerDock;
        public readonly string TimerUndock;
        public readonly string TimerLoad;
        public readonly string TimerUnload;
        public readonly string TimerFinalizeCargo;

        public readonly string RemoteControlPilot;
        public readonly string RemoteControlDocking;
        public readonly string RemoteControlLanding;

        public readonly string Camera;
        public readonly string Connector;
        public readonly string Antenna;

        public readonly double MaxLoad;
        public readonly double MinLoad;
        public readonly double MinPowerOnLoad;
        public readonly double MinPowerOnUnload;
        public readonly double MinHydrogenOnLoad;
        public readonly double MinHydrogenOnUnload;

        public readonly Route Route;

        public readonly int NavigationTicks;
        public readonly double DockingSpeedWaypointFirst;
        public readonly double DockingSpeedWaypointLast;
        public readonly double DockingSpeedWaypoints;
        public readonly double DockingSlowdownDistance;
        public readonly double DockingDistanceThrWaypoints;

        public readonly double TaxiSpeed;

        public readonly double AtmNavigationAlignThr;
        public readonly double AtmNavigationMaxSpeed;
        public readonly double AtmNavigationWaypointThr;
        public readonly double AtmNavigationDestinationThr;
        public readonly double AtmGravityThr;

        public readonly double CrsNavigationAlignThr;
        public readonly double CrsNavigationAlignSeconds;

        public readonly double CrsNavigationMaxSpeedThr;
        public readonly double CrsNavigationMaxAccelerationSpeed;
        public readonly double CrsNavigationMaxCruiseSpeed;
        public readonly double CrsNavigationMaxEvadingSpeed;

        public readonly double CrsNavigationWaypointThr;
        public readonly double CrsNavigationDestinationThr;

        public readonly double CrsNavigationCollisionDetectRange;
        public readonly double CrsNavigationEvadingWaypointThr;

        public readonly double GyrosThr;
        public readonly double GyrosSpeed;

        public readonly TimeSpan RefreshLCDsInterval; // seconds, how often to refresh LCDs

        public Config(string customData)
        {
            Channel = Utils.ReadConfig(customData, "Channel");

            EnableLogs = ReadConfigBool(customData, "EnableLogs");
            EnableRefreshLCDs = ReadConfigBool(customData, "EnableRefreshLCDs");

            WildcardShipInfo = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardShipInfo")}(?:\.(\d+))?\]");
            WildcardLogLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardLogLCDs")}(?:\.(\d+))?\]");

            TimerPilot = ReadConfig(customData, "TimerPilot", "");
            TimerWaiting = ReadConfig(customData, "TimerWaiting");

            TimerDock = ReadConfig(customData, "TimerDock");
            TimerUndock = ReadConfig(customData, "TimerUndock");

            TimerLoad = ReadConfig(customData, "TimerLoad", "");
            TimerUnload = ReadConfig(customData, "TimerUnload", "");
            TimerFinalizeCargo = ReadConfig(customData, "TimerFinalizeCargo", "");

            RemoteControlPilot = ReadConfig(customData, "RemoteControlPilot");
            RemoteControlDocking = ReadConfig(customData, "RemoteControlDocking");
            RemoteControlLanding = ReadConfig(customData, "RemoteControlLanding");

            Camera = ReadConfig(customData, "Camera");
            Connector = ReadConfig(customData, "Connector");
            Antenna = ReadConfig(customData, "Antenna");

            MaxLoad = ReadConfigDouble(customData, "MaxLoad", 1);
            MinLoad = ReadConfigDouble(customData, "MinLoad", 0);
            MinPowerOnLoad = ReadConfigDouble(customData, "MinPowerOnLoad", 0);
            MinPowerOnUnload = ReadConfigDouble(customData, "MinPowerOnUnload", 0);
            MinHydrogenOnLoad = ReadConfigDouble(customData, "MinHydrogenOnLoad", 0);
            MinHydrogenOnUnload = ReadConfigDouble(customData, "MinHydrogenOnUnload", 0);

            Route = new Route(
                ReadConfig(customData, "RouteLoadBase"),
                ReadConfigBool(customData, "RouteLoadBaseOnPlanet"),
                ReadConfigVectorList(customData, "RouteToLoadBaseWaypoints"),
                ReadConfig(customData, "RouteUnloadBase"),
                ReadConfigBool(customData, "RouteUnloadBaseOnPlanet"),
                ReadConfigVectorList(customData, "RouteToUnloadBaseWaypoints"));

            NavigationTicks = ReadConfigInt(customData, "NavigationTicks", 1);
            DockingSpeedWaypointFirst = ReadConfigDouble(customData, "DockingSpeedWaypointFirst");
            DockingSpeedWaypointLast = ReadConfigDouble(customData, "DockingSpeedWaypointLast");
            DockingSpeedWaypoints = ReadConfigDouble(customData, "DockingSpeedWaypoints");
            DockingSlowdownDistance = ReadConfigDouble(customData, "DockingSlowdownDistance");
            DockingDistanceThrWaypoints = ReadConfigDouble(customData, "DockingDistanceThrWaypoints");

            TaxiSpeed = ReadConfigDouble(customData, "TaxiSpeed");

            AtmNavigationAlignThr = ReadConfigDouble(customData, "AtmNavigationAlignThr");
            AtmNavigationMaxSpeed = ReadConfigDouble(customData, "AtmNavigationMaxSpeed");
            AtmNavigationWaypointThr = ReadConfigDouble(customData, "AtmNavigationWaypointThr");
            AtmNavigationDestinationThr = ReadConfigDouble(customData, "AtmNavigationDestinationThr");
            AtmGravityThr = ReadConfigDouble(customData, "AtmGravityThr", 0.001);

            CrsNavigationAlignThr = ReadConfigDouble(customData, "CrsNavigationAlignThr");
            CrsNavigationAlignSeconds = ReadConfigDouble(customData, "CrsNavigationAlignSeconds");
            CrsNavigationMaxSpeedThr = ReadConfigDouble(customData, "CrsNavigationMaxSpeedThr");
            CrsNavigationMaxAccelerationSpeed = ReadConfigDouble(customData, "CrsNavigationMaxAccelerationSpeed");
            CrsNavigationMaxCruiseSpeed = ReadConfigDouble(customData, "CrsNavigationMaxCruiseSpeed");
            CrsNavigationMaxEvadingSpeed = ReadConfigDouble(customData, "CrsNavigationMaxEvadingSpeed");
            CrsNavigationWaypointThr = ReadConfigDouble(customData, "CrsNavigationWaypointThr");
            CrsNavigationDestinationThr = ReadConfigDouble(customData, "CrsNavigationDestinationThr");
            CrsNavigationCollisionDetectRange = ReadConfigDouble(customData, "CrsNavigationCollisionDetectRange");
            CrsNavigationEvadingWaypointThr = ReadConfigDouble(customData, "CrsNavigationEvadingWaypointThr");

            GyrosThr = ReadConfigDouble(customData, "GyrosThr");
            GyrosSpeed = ReadConfigDouble(customData, "GyrosSpeed");

            RefreshLCDsInterval = TimeSpan.FromSeconds(ReadConfigInt(customData, "RefreshLCDsInterval", 10));
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
                "EnableRefreshLCDs=false\n" +
                "\n" +
                "WildcardShipInfo=DELIVERY_INFO\n" +
                "WildcardLogLCDs=DELIVERY_LOG\n" +
                "\n" +
                "TimerPilot=Timer Block Pilot\n" +
                "TimerWaiting=Timer Block Waiting\n" +
                "\n" +
                "TimerDock=Timer Block Dock\n" +
                "TimerUndock=Timer Block Undock\n" +
                "\n" +
                "TimerLoad=Timer Block Load\n" +
                "TimerUnload=Timer Block Unload\n" +
                "TimerFinalizeCargo=Timer Block Finalize\n" +
                "\n" +
                "RemoteControlPilot=Remote Control Pilot\n" +
                "RemoteControlDocking=Remote Control Docking\n" +
                "RemoteControlLanding=Remote Control Landing\n" +
                "\n" +
                "Camera=Main Camera\n" +
                "Connector=Main Connector\n" +
                "Antenna=Main Antenna\n" +
                "\n" +
                "MaxLoad=1\n" +
                "MinLoad=0\n" +
                "MinPowerOnLoad=0.9\n" +
                "MinPowerOnUnload=0.9\n" +
                "MinHydrogenOnLoad=0.9\n" +
                "MinHydrogenOnUnload=0.9\n" +
                "\n" +
                "RouteLoadBase=base1\n" +
                "RouteLoadBaseOnPlanet=false\n" +
                "RouteToLoadBaseWaypoints=x:y:z;x:y:z\n" +
                "RouteUnloadBase=base2\n" +
                "RouteUnloadBaseOnPlanet=false\n" +
                "RouteToUnloadBaseWaypoints=x:y:z;x:y:z\n" +
                "\n" +
                "NavigationTicks=1\n" +
                "DockingSpeedWaypointFirst=10.0\n" +
                "DockingSpeedWaypointLast=1.0\n" +
                "DockingSpeedWaypoints=5.0\n" +
                "DockingSlowdownDistance=50.0\n" +
                "DockingDistanceThrWaypoints=0.5\n" +
                "\n" +
                "TaxiSpeed=25\n" +
                "\n" +
                "AtmNavigationAlignThr=0.01\n" +
                "AtmNavigationMaxSpeed=100.0\n" +
                "AtmNavigationWaypointThr=500.0\n" +
                "AtmNavigationDestinationThr=1000.0\n" +
                "AtmGravityThr=0.1\n" +
                "\n" +
                "CrsNavigationAlignThr=0.01\n" +
                "CrsNavigationAlignSeconds=5.0\n" +
                "CrsNavigationMaxSpeedThr=0.95\n" +
                "CrsNavigationMaxAccelerationSpeed=19.5\n" +
                "CrsNavigationMaxCruiseSpeed=100.0\n" +
                "CrsNavigationMaxEvadingSpeed=19.5\n" +
                "CrsNavigationWaypointThr=1000.0\n" +
                "CrsNavigationDestinationThr=2500.0\n" +
                "CrsNavigationCollisionDetectRange=10000.0\n" +
                "CrsNavigationEvadingWaypointThr=100.0\n" +
                "\n" +
                "GyrosThr=0.001\n" +
                "GyrosSpeed=2.0\n" +
                "\n" +
                "RefreshLCDsInterval=10";
        }
    }
}
