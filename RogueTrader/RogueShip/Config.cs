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
        public readonly string ExchangeType;

        public bool EnableLogs = true;
        public bool EnableRefreshLCDs = false;

        public readonly System.Text.RegularExpressions.Regex WildcardShipInfo;
        public readonly System.Text.RegularExpressions.Regex WildcardPlanLCDs;
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
        public readonly TimeSpan MaxLoadTime;
        public readonly double MinPowerOnLoad;
        public readonly double MinPowerOnUnload;
        public readonly double MinHydrogenOnLoad;
        public readonly double MinHydrogenOnUnload;

        public readonly Route DefaultRoute;

        public readonly int NavigationTicks;
        public readonly double DockingSpeedWaypointFirst;
        public readonly double DockingSpeedWaypointLast;
        public readonly double DockingSpeedWaypoints;
        public readonly double DockingSlowdownDistance;
        public readonly double DockingDistanceThrWaypoints;
        public readonly TimeSpan DockUpdateInterval;

        public readonly double TaxiSpeed;

        public readonly double AtmNavigationAlignThr;
        public readonly double AtmNavigationMaxSpeed;
        public readonly double AtmNavigationWaypointThr;
        public readonly double AtmNavigationDestinationThr;
        public readonly double AtmNavigationGravityThr;

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

        public readonly TimeSpan DockRequestTimeout = TimeSpan.FromSeconds(300); //Seconds to wait for a docking request to be accepted

        public readonly TimeSpan RefreshLCDsInterval; // seconds, how often to refresh LCDs

        public Config(string customData)
        {
            Channel = Utils.ReadConfig(customData, "Channel");
            ExchangeType = Utils.ReadConfig(customData, "ExchangeType");

            EnableLogs = ReadConfigBool(customData, "EnableLogs", false);
            EnableRefreshLCDs = ReadConfigBool(customData, "EnableRefreshLCDs", false);

            WildcardShipInfo = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardShipInfo", "SHIP_INFO")}(?:\.(\d+))?\]");
            WildcardPlanLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardPlanLCDs", "SHIP_PLAN")}(?:\.(\d+))?\]");
            WildcardLogLCDs = new System.Text.RegularExpressions.Regex($@"\[{ReadConfig(customData, "WildcardLogLCDs", "SHIP_LOG")}(?:\.(\d+))?\]");

            TimerPilot = ReadConfig(customData, "TimerPilot", "");
            TimerWaiting = ReadConfig(customData, "TimerWaiting", "");

            TimerDock = ReadConfig(customData, "TimerDock");
            TimerUndock = ReadConfig(customData, "TimerUndock", "");

            TimerLoad = ReadConfig(customData, "TimerLoad", "");
            TimerUnload = ReadConfig(customData, "TimerUnload", "");
            TimerFinalizeCargo = ReadConfig(customData, "TimerFinalizeCargo", "");

            RemoteControlPilot = ReadConfig(customData, "RemoteControlPilot", "");
            RemoteControlDocking = ReadConfig(customData, "RemoteControlDocking");
            RemoteControlLanding = ReadConfig(customData, "RemoteControlLanding", "");

            Camera = ReadConfig(customData, "Camera", "");
            Connector = ReadConfig(customData, "Connector");
            Antenna = ReadConfig(customData, "Antenna", "");

            MaxLoad = ReadConfigDouble(customData, "MaxLoad", 1);
            MinLoad = ReadConfigDouble(customData, "MinLoad", 0);
            MinPowerOnLoad = ReadConfigDouble(customData, "MinPowerOnLoad", 0);
            MinPowerOnUnload = ReadConfigDouble(customData, "MinPowerOnUnload", 0);
            MinHydrogenOnLoad = ReadConfigDouble(customData, "MinHydrogenOnLoad", 0);
            MinHydrogenOnUnload = ReadConfigDouble(customData, "MinHydrogenOnUnload", 0);
            MaxLoadTime = TimeSpan.FromSeconds(ReadConfigInt(customData, "MaxLoadTime", 0));

            DefaultRoute = new Route(
                ReadConfig(customData, "RouteLoadBase", ""),
                ReadConfigBool(customData, "RouteLoadBaseOnPlanet", false),
                ReadConfigVectorList(customData, "RouteToLoadBaseWaypoints", new List<Vector3D>()),
                ReadConfig(customData, "RouteUnloadBase", ""),
                ReadConfigBool(customData, "RouteUnloadBaseOnPlanet", false),
                ReadConfigVectorList(customData, "RouteToUnloadBaseWaypoints", new List<Vector3D>()));

            NavigationTicks = ReadConfigInt(customData, "NavigationTicks", 1);

            DockingSpeedWaypointFirst = ReadConfigDouble(customData, "DockingSpeedWaypointFirst", 10.0);
            DockingSpeedWaypointLast = ReadConfigDouble(customData, "DockingSpeedWaypointLast", 1.0);
            DockingSpeedWaypoints = ReadConfigDouble(customData, "DockingSpeedWaypoints", 5.0);
            DockingSlowdownDistance = ReadConfigDouble(customData, "DockingSlowdownDistance", 50.0);
            DockingDistanceThrWaypoints = ReadConfigDouble(customData, "DockingDistanceThrWaypoints", 0.5);
            DockUpdateInterval = TimeSpan.FromSeconds(ReadConfigDouble(customData, "DockUpdateInterval", 0.5));

            TaxiSpeed = ReadConfigDouble(customData, "TaxiSpeed", 25);

            AtmNavigationAlignThr = ReadConfigDouble(customData, "AtmNavigationAlignThr", 0.01);
            AtmNavigationMaxSpeed = ReadConfigDouble(customData, "AtmNavigationMaxSpeed", 100.0);
            AtmNavigationWaypointThr = ReadConfigDouble(customData, "AtmNavigationWaypointThr", 500.0);
            AtmNavigationDestinationThr = ReadConfigDouble(customData, "AtmNavigationDestinationThr", 1000.0);
            AtmNavigationGravityThr = ReadConfigDouble(customData, "AtmNavigationGravityThr", 0.001);

            CrsNavigationAlignThr = ReadConfigDouble(customData, "CrsNavigationAlignThr", 0.01);
            CrsNavigationAlignSeconds = ReadConfigDouble(customData, "CrsNavigationAlignSeconds", 5.0);
            CrsNavigationMaxSpeedThr = ReadConfigDouble(customData, "CrsNavigationMaxSpeedThr", 0.95);
            CrsNavigationMaxAccelerationSpeed = ReadConfigDouble(customData, "CrsNavigationMaxAccelerationSpeed", 19.5);
            CrsNavigationMaxCruiseSpeed = ReadConfigDouble(customData, "CrsNavigationMaxCruiseSpeed", 100.0);
            CrsNavigationMaxEvadingSpeed = ReadConfigDouble(customData, "CrsNavigationMaxEvadingSpeed", 19.5);
            CrsNavigationWaypointThr = ReadConfigDouble(customData, "CrsNavigationWaypointThr", 1000.0);
            CrsNavigationDestinationThr = ReadConfigDouble(customData, "CrsNavigationDestinationThr", 2500.0);
            CrsNavigationCollisionDetectRange = ReadConfigDouble(customData, "CrsNavigationCollisionDetectRange", 10000.0);
            CrsNavigationEvadingWaypointThr = ReadConfigDouble(customData, "CrsNavigationEvadingWaypointThr", 100.0);

            GyrosThr = ReadConfigDouble(customData, "GyrosThr", 0.001);
            GyrosSpeed = ReadConfigDouble(customData, "GyrosSpeed", 2.0);

            DockRequestTimeout = TimeSpan.FromSeconds(ReadConfigInt(customData, "DockRequestTimeout", 300));

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
                "Channel=STD\n" +
                "ExchangeType=type\n" +
                "\n" +
                "EnableLogs=false\n" +
                "EnableRefreshLCDs=false\n" +
                "\n" +
                "WildcardShipInfo=SHIP_INFO\n" +
                "WildcardPlanLCDs=SHIP_PLAN\n" +
                "WildcardLogLCDs=SHIP_LOG\n" +
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
                "MaxLoadTime=0\n" +
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
                "DockUpdateInterval=0.5\n" +
                "\n" +
                "TaxiSpeed=25\n" +
                "\n" +
                "AtmNavigationAlignThr=0.01\n" +
                "AtmNavigationMaxSpeed=100.0\n" +
                "AtmNavigationWaypointThr=500.0\n" +
                "AtmNavigationDestinationThr=1000.0\n" +
                "AtmNavigationGravityThr=0.001\n" +
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
                "DockRequestTimeout=300\n" +
                "\n" +
                "RefreshLCDsInterval=10";
        }
    }
}
