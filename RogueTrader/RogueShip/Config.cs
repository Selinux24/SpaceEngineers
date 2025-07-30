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

        public readonly string ShipTimerPilot;
        public readonly string ShipTimerLock;
        public readonly string ShipTimerUnlock;
        public readonly string ShipTimerLoad;
        public readonly string ShipTimerUnload;
        public readonly string ShipTimerWaiting;
        public readonly string ShipRemoteControlPilot;
        public readonly string ShipCameraPilot;
        public readonly string ShipRemoteControlAlign;
        public readonly string ShipRemoteControlLanding;
        public readonly string ShipConnectorA;
        public readonly string ShipAntennaName;

        public readonly double GyrosThr; //Alignment accuracy
        public readonly double GyrosSpeed; //Gyroscope speed


        public readonly int ArrivalTicks;

        public readonly int AlignTicks;
        public readonly double AlignExchangeApproachingSpeed; //Maximum approach speed to the connector
        public readonly double AlignExchangeSlowdownDistance = 50.0; //Braking distance to the first point of the connector
        public readonly double AlignExchangeDistanceThr; //Accuracy of approximation to the first point of the connector
        public readonly double AlignSpeedWaypointFirst = 10.0; //Maximum speed at first waypoint
        public readonly double AlignSpeedWaypoints = 5.0; //Maximum speed between waypoints
        public readonly double AlignSpeedWaypointLast = 1.0; //Maximum speed at the last waypoint
        public readonly double AlignDistanceThrWaypoints; //Approximation accuracy between waypoints

        public readonly int CruisingTicks;
        public readonly double CruisingMaxSpeed; //Maximum cruising speed
        public readonly double CruisingMaxSpeedThr;
        public readonly double CruisingMaxAccelerationSpeed; //Maximum cruising speed near base
        public readonly double CruisingToBasesDistanceThr; //Distance to the departure point to activate maximum cruising speed
        public readonly double CruisingToTargetDistanceThr; //Braking range to target
        public readonly double CruisingThrustAlignSeconds; //Thruster ignition time until alignment
        public readonly double CruisingLocateAlignThr; //Alignment accuracy
        public readonly double CruisingCruiseAlignThr; //Alignment accuracy
        public readonly double CruisingCollisionDetectRange; //Collision detection range
        public readonly double CruisingEvadingWaypointDistance;
        public readonly double CruisingEvadingMaxSpeed;

        public readonly int AtmNavigationTicks;
        public readonly double AtmNavigationMaxSpeed; //Maximum takeoff speed
        public readonly double AtmNavigationToTargetDistanceThr; //Braking range to target
        public readonly double AtmNavigationAlignThr; //Alignment accuracy
        public readonly double AtmNavigationMinLoad;
        public readonly double AtmNavigationMaxLoad;
        public readonly Route AtmNavigationRoute;
        public readonly double AtmNavigationSeparationSecs; //Separation thrust time before acceleration

        public Config(string customData)
        {
            Channel = Utils.ReadConfig(customData, "Channel");

            EnableLogs = ReadConfig(customData, "EnableLogs", "false") == "true";

            WildcardShipId = ReadConfig(customData, "WildcardShipId");
            WildcardShipInfo = ReadConfig(customData, "WildcardShipInfo");
            WildcardLogLCDs = ReadConfig(customData, "WildcardLogLCDs");

            ShipTimerPilot = ReadConfig(customData, "ShipTimerPilot", "");
            ShipTimerLock = ReadConfig(customData, "ShipTimerLock");
            ShipTimerUnlock = ReadConfig(customData, "ShipTimerUnlock");
            ShipTimerLoad = ReadConfig(customData, "ShipTimerLoad", "");
            ShipTimerUnload = ReadConfig(customData, "ShipTimerUnload", "");
            ShipTimerWaiting = ReadConfig(customData, "ShipTimerWaiting");
            ShipRemoteControlPilot = ReadConfig(customData, "ShipRemoteControlPilot");
            ShipCameraPilot = ReadConfig(customData, "ShipCameraPilot");
            ShipRemoteControlAlign = ReadConfig(customData, "ShipRemoteControlAlign");
            ShipRemoteControlLanding = ReadConfig(customData, "ShipRemoteControlLanding");
            ShipConnectorA = ReadConfig(customData, "ShipConnectorA");
            ShipAntennaName = ReadConfig(customData, "ShipAntennaName");

            GyrosThr = ReadConfigDouble(customData, "GyrosThr");
            GyrosSpeed = ReadConfigDouble(customData, "GyrosSpeed");

            ArrivalTicks = ReadConfigInt(customData, "ArrivalTicks");

            AlignTicks = ReadConfigInt(customData, "AlignTicks");
            AlignExchangeApproachingSpeed = ReadConfigDouble(customData, "AlignExchangeApproachingSpeed");
            AlignExchangeSlowdownDistance = ReadConfigDouble(customData, "AlignExchangeSlowdownDistance");
            AlignExchangeDistanceThr = ReadConfigDouble(customData, "AlignExchangeDistanceThr");
            AlignSpeedWaypointFirst = ReadConfigDouble(customData, "AlignSpeedWaypointFirst");
            AlignSpeedWaypoints = ReadConfigDouble(customData, "AlignSpeedWaypoints");
            AlignSpeedWaypointLast = ReadConfigDouble(customData, "AlignSpeedWaypointLast");
            AlignDistanceThrWaypoints = ReadConfigDouble(customData, "AlignDistanceThrWaypoints");

            CruisingTicks = ReadConfigInt(customData, "CruisingTicks");
            CruisingMaxSpeed = ReadConfigDouble(customData, "CruisingMaxSpeed");
            CruisingMaxSpeedThr = ReadConfigDouble(customData, "CruisingMaxSpeedThr");
            CruisingMaxAccelerationSpeed = ReadConfigDouble(customData, "CruisingMaxAccelerationSpeed");
            CruisingToBasesDistanceThr = ReadConfigDouble(customData, "CruisingToBasesDistanceThr");
            CruisingToTargetDistanceThr = ReadConfigDouble(customData, "CruisingToTargetDistanceThr");
            CruisingThrustAlignSeconds = ReadConfigDouble(customData, "CruisingThrustAlignSeconds");
            CruisingLocateAlignThr = ReadConfigDouble(customData, "CruisingLocateAlignThr");
            CruisingCruiseAlignThr = ReadConfigDouble(customData, "CruisingCruiseAlignThr");
            CruisingCollisionDetectRange = ReadConfigDouble(customData, "CruisingCollisionDetectRange");
            CruisingEvadingWaypointDistance = ReadConfigDouble(customData, "CruisingEvadingWaypointDistance");
            CruisingEvadingMaxSpeed = ReadConfigDouble(customData, "CruisingEvadingMaxSpeed");

            AtmNavigationTicks = ReadConfigInt(customData, "AtmNavigationTicks");
            AtmNavigationMaxSpeed = ReadConfigDouble(customData, "AtmNavigationMaxSpeed");
            AtmNavigationToTargetDistanceThr = ReadConfigDouble(customData, "AtmNavigationToTargetDistanceThr");
            AtmNavigationAlignThr = ReadConfigDouble(customData, "AtmNavigationAlignThr");
            AtmNavigationMinLoad = ReadConfigDouble(customData, "AtmNavigationMinLoad", 0.1);
            AtmNavigationMaxLoad = ReadConfigDouble(customData, "AtmNavigationMaxLoad", 0.9);
            AtmNavigationRoute = new Route(
                ReadConfig(customData, "AtmNavigationLoadBase"),
                ReadConfig(customData, "AtmNavigationUnloadBase"),
                ReadConfigVectorList(customData, "AtmNavigationToLoadBaseWaypoints", new List<Vector3D>()),
                ReadConfigVectorList(customData, "AtmNavigationToUnloadBaseWaypoints", new List<Vector3D>()));
            AtmNavigationSeparationSecs = ReadConfigDouble(customData, "AtmNavigationSeparationSecs");
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
                "ShipTimerPilot=Timer Block Pilot\n" +
                "ShipTimerLock=Timer Block Locking\n" +
                "ShipTimerUnlock=Timer Block Unlocking\n" +
                "ShipTimerLoad=Timer Block Load\n" +
                "ShipTimerUnload=Timer Block Unload\n" +
                "ShipTimerWaiting=Timer Block Waiting\n" +
                "ShipRemoteControlPilot=Remote Control Pilot\n" +
                "ShipCameraPilot=Camera Pilot\n" +
                "ShipRemoteControlAlign=Remote Control Locking\n" +
                "ShipRemoteControlLanding=Remote Control Landing\n" +
                "ShipConnectorA=Connector A\n" +
                "ShipBeaconName=Distress Beacon\n" +
                "ShipAntennaName=Compact Antenna\n" +
                "\n" +
                "GyrosThr=0.001\n" +
                "GyrosSpeed=2.0\n" +
                "\n" +
                "ArrivalTicks=100\n" +
                "\n" +
                "AlignTicks=1\n" +
                "AlignExchangeApproachingSpeed=5\n" +
                "AlignExchangeSlowdownDistance=50.0\n" +
                "AlignExchangeDistanceThr=200.0\n" +
                "AlignSpeedWaypointFirst=10.0\n" +
                "AlignSpeedWaypoints=5.0\n" +
                "AlignSpeedWaypointLast=1.0\n" +
                "AlignDistanceThrWaypoints=0.5\n" +
                "\n" +
                "CruisingTicks=1\n" +
                "CruisingMaxSpeed=100.0\n" +
                "CruisingMaxSpeedThr=0.95\n" +
                "CruisingMaxAccelerationSpeed=19.5\n" +
                "CruisingToBasesDistanceThr=2000.0\n" +
                "CruisingToTargetDistanceThr=3000.0\n" +
                "CruisingThrustAlignSeconds=5.0\n" +
                "CruisingLocateAlignThr=0.001\n" +
                "CruisingCruiseAlignThr=0.01\n" +
                "CruisingCollisionDetectRange=10000.0\n" +
                "CruisingEvadingWaypointDistance=100.0\n" +
                "CruisingEvadingMaxSpeed=19.5\n" +
                "\n" +
                "AtmNavigationTicks=1\n" +
                "AtmNavigationMaxSpeed=100.0\n" +
                "AtmNavigationToTargetDistanceThr=1000.0\n" +
                "AtmNavigationAlignThr=0.01\n" +
                "AtmNavigationMinLoad=0.1\n" +
                "AtmNavigationMaxLoad=0.9\n" +
                "AtmNavigationLoadBase=name\n" +
                "AtmNavigationUnloadBase=name\n" +
                "AtmNavigationSeparationSecs=3";
        }
    }
}
