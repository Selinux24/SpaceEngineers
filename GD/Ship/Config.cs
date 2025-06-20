using System;
using System.Linq;
using System.Text;

namespace IngameScript
{
    class Config
    {
        readonly StringBuilder errors = new StringBuilder();

        public readonly string Channel;

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
        public readonly string ShipBeaconName;
        public readonly string ShipAntennaName;

        public readonly double GyrosThr; //Precisión de alineación
        public readonly double GyrosSpeed; //Velocidad de los giroscopios

        public readonly double ExchangeMaxApproachingSpeed; // Velocidad máxima de aproximación al conector
        public readonly double ExchangeDistanceThr; //Precisión de aproximación al primer punto del conector
        public readonly double ExchangeWaypointDistanceThr; //Precisión de aproximación entre waypoints

        public readonly double AlignMaxApproachSpeed = 10.0; //Velocidad máxima de llegada
        public readonly double AlignMaxApproachSpeedAprox = 5.0; //Velocidad máxima de aproximación
        public readonly double AlignMaxApproachSpeedLocking = 1.0; //Velocidad máxima en el último waypoint
        public readonly double AlignSlowdownDistance = 50.0; //Distancia de frenada

        public readonly double CruisingMaxSpeed; // Velocidad máxima de crucero
        public readonly double CruisingMaxSpeedThr;
        public readonly double CruisingMaxAccelerationSpeed; // Velocidad máxima de crucero cerca de la base
        public readonly double CruisingToBasesDistanceThr; // Distancia al punto de salida para activar la velocidad máxima de crucero
        public readonly double CruisingToTargetDistanceThr; // Rango de frenado hasta el objetivo

        public readonly double CruisingThrustAlignSeconds; // Tiempo de encendido de thrusters hasta alineación
        public readonly double CruisingLocateAlignThr; // Precisión de alineación
        public readonly double CruisingCruiseAlignThr; // Precisión de alineación

        public readonly double CrusingCollisionDetectRange; // Rango de detección de colisiones
        public readonly double CrusingEvadingWaypointDistance;
        public readonly double CrusingEvadingMaxSpeed;

        public readonly double AtmNavigationMaxSpeed; // Velocidad máxima de despegue
        public readonly double AtmNavigationToTargetDistanceThr; // Rango de frenado hasta el objetivo
        public readonly double AtmNavigationAlignThr; // Precisión de alineación

        public readonly int ArrivalTicks;
        public readonly int AlignTicks;
        public readonly int NavigationTicks;
        public readonly int AtmNavigationTicks;

        public Config(string customData)
        {
            Channel = Utils.ReadConfig(customData, "Channel");

            WildcardShipId = ReadConfig(customData, "WildcardShipId");
            WildcardShipInfo = ReadConfig(customData, "WildcardShipInfo");
            WildcardLogLCDs = ReadConfig(customData, "WildcardLogLCDs");

            ShipTimerPilot = ReadConfig(customData, "ShipTimerPilot");
            ShipTimerLock = ReadConfig(customData, "ShipTimerLock");
            ShipTimerUnlock = ReadConfig(customData, "ShipTimerUnlock");
            ShipTimerLoad = ReadConfig(customData, "ShipTimerLoad");
            ShipTimerUnload = ReadConfig(customData, "ShipTimerUnload");
            ShipTimerWaiting = ReadConfig(customData, "ShipTimerWaiting");
            ShipRemoteControlPilot = ReadConfig(customData, "ShipRemoteControlPilot");
            ShipCameraPilot = ReadConfig(customData, "ShipCameraPilot");
            ShipRemoteControlAlign = ReadConfig(customData, "ShipRemoteControlAlign");
            ShipRemoteControlLanding = ReadConfig(customData, "ShipRemoteControlLanding");
            ShipConnectorA = ReadConfig(customData, "ShipConnectorA");
            ShipBeaconName = ReadConfig(customData, "ShipBeaconName");
            ShipAntennaName = ReadConfig(customData, "ShipAntennaName");

            GyrosThr = ReadConfigDouble(customData, "GyrosThr");
            GyrosSpeed = ReadConfigDouble(customData, "GyrosSpeed");

            ExchangeMaxApproachingSpeed = ReadConfigDouble(customData, "ExchangeMaxApproachingSpeed");
            ExchangeDistanceThr = ReadConfigDouble(customData, "ExchangeDistanceThr");
            ExchangeWaypointDistanceThr = ReadConfigDouble(customData, "ExchangeWaypointDistanceThr");

            AlignMaxApproachSpeed = ReadConfigDouble(customData, "AlignMaxApproachSpeed");
            AlignMaxApproachSpeedAprox = ReadConfigDouble(customData, "AlignMaxApproachSpeedAprox");
            AlignMaxApproachSpeedLocking = ReadConfigDouble(customData, "AlignMaxApproachSpeedLocking");
            AlignSlowdownDistance = ReadConfigDouble(customData, "AlignSlowdownDistance");

            CruisingMaxSpeed = ReadConfigDouble(customData, "CruisingMaxSpeed");
            CruisingMaxSpeedThr = ReadConfigDouble(customData, "CruisingMaxSpeedThr");
            CruisingMaxAccelerationSpeed = ReadConfigDouble(customData, "CruisingMaxAccelerationSpeed");
            CruisingToBasesDistanceThr = ReadConfigDouble(customData, "CruisingToBasesDistanceThr");
            CruisingToTargetDistanceThr = ReadConfigDouble(customData, "CruisingToTargetDistanceThr");

            CruisingThrustAlignSeconds = ReadConfigDouble(customData, "CruisingThrustAlignSeconds");
            CruisingLocateAlignThr = ReadConfigDouble(customData, "CruisingLocateAlignThr");
            CruisingCruiseAlignThr = ReadConfigDouble(customData, "CruisingCruiseAlignThr");

            CrusingCollisionDetectRange = ReadConfigDouble(customData, "CrusingCollisionDetectRange");
            CrusingEvadingWaypointDistance = ReadConfigDouble(customData, "CrusingEvadingWaypointDistance");
            CrusingEvadingMaxSpeed = ReadConfigDouble(customData, "CrusingEvadingMaxSpeed");

            AtmNavigationMaxSpeed = ReadConfigDouble(customData, "AtmNavigationMaxSpeed");
            AtmNavigationToTargetDistanceThr = ReadConfigDouble(customData, "AtmNavigationToTargetDistanceThr");
            AtmNavigationAlignThr = ReadConfigDouble(customData, "AtmNavigationAlignThr");

            ArrivalTicks = ReadConfigInt(customData, "ArrivalTicks");
            AlignTicks = ReadConfigInt(customData, "AlignTicks");
            NavigationTicks = ReadConfigInt(customData, "NavigationTicks");
            AtmNavigationTicks = ReadConfigInt(customData, "AtmNavigationTicks");
        }
        string ReadConfig(string customData, string name)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.AppendLine($"{name} not set.");
            }

            return value;
        }
        int ReadConfigInt(string customData, string name, int defaultValue = 0)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.AppendLine($"{name} not set.");

                return defaultValue;
            }

            return int.Parse(value.Trim());
        }
        double ReadConfigDouble(string customData, string name, double defaultValue = 0)
        {
            var value = ReadConfigLine(customData, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.AppendLine($"{name} not set.");

                return defaultValue;
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
                "ExchangeMaxApproachingSpeed=5\n" +
                "ExchangeDistanceThr=200.0\n" +
                "ExchangeWaypointDistanceThr=0.5\n" +
                "\n" +
                "AlignMaxApproachSpeed=10.0\n" +
                "AlignMaxApproachSpeedAprox=5.0\n" +
                "AlignMaxApproachSpeedLocking=1.0\n" +
                "AlignSlowdownDistance=50.0\n" +
                "\n" +
                "CruisingMaxSpeed=100.0\n" +
                "CruisingMaxSpeedThr=0.95\n" +
                "CruisingMaxAccelerationSpeed=19.5\n" +
                "CruisingToBasesDistanceThr=2000.0\n" +
                "CruisingToTargetDistanceThr=3000.0\n" +
                "\n" +
                "CruisingThrustAlignSeconds=5.0\n" +
                "CruisingLocateAlignThr=0.001\n" +
                "CruisingCruiseAlignThr=0.01\n" +
                "\n" +
                "CrusingCollisionDetectRange=10000.0\n" +
                "CrusingEvadingWaypointDistance=100.0\n" +
                "CrusingEvadingMaxSpeed=19.5\n" +
                "\n" +
                "AtmNavigationMaxSpeed=100.0\n" +
                "AtmNavigationToTargetDistanceThr=1000.0\n" +
                "AtmNavigationAlignThr=0.01\n" +
                "\n" +
                "ArrivalTicks=100\n" +
                "AlignTicks=1\n" +
                "NavigationTicks=1\n" +
                "AtmNavigationTicks=1";
        }
    }
}
