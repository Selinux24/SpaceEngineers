using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    class ExchangeGroup
    {
        private double dockRequestTime = 0;
        private bool waitingDock = false;
        private bool waitingUndock = false;

        public readonly string Name;
        public readonly int NumWaypoints;
        public readonly double PathDistance; //Meters, distance from the dock to the first waypoint
        public readonly int PathType; //0 = Straight, 1 = Curve
        public IMyShipConnector MainConnector;
        public readonly List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
        public IMyCameraBlock Camera;
        public IMyTimerBlock TimerLoad;
        public IMyTimerBlock TimerUnload;
        public IMyTimerBlock TimerDockPrepare;
        public IMyTimerBlock TimerDockStart;
        public IMyTimerBlock TimerUndockPrepare;
        public IMyTimerBlock TimerUndockStart;
        public IMyTimerBlock TimerFree;
        public Vector3D Forward => Camera.WorldMatrix.Forward;
        public Vector3D Up => Camera.WorldMatrix.Up;

        public string DockedShipName { get; private set; }
        public string ReservedShipName { get; private set; }

        public ExchangeGroup(string name, int numWaypoints, double pathDistance, int pathType)
        {
            Name = name;
            NumWaypoints = numWaypoints;
            PathDistance = pathDistance;
            PathType = pathType;
        }

        public bool IsValid(out string errorMessage)
        {
            if (MainConnector == null)
            {
                errorMessage = "No main connector";
                return false;
            }
            if (Camera == null)
            {
                errorMessage = "No camera";
                return false;
            }

            errorMessage = "";
            return true;
        }
        public bool IsFree()
        {
            if (MainConnector.Status != MyShipConnectorStatus.Unconnected) return false;

            foreach (var connector in Connectors)
            {
                if (connector.Status != MyShipConnectorStatus.Unconnected) return false;
            }

            return true;
        }
        public List<string> DockedShips()
        {
            var names = new List<string>();

            if (MainConnector.Status == MyShipConnectorStatus.Connected)
            {
                names.Add(MainConnector.OtherConnector.CubeGrid.CustomName);
            }

            foreach (var connector in Connectors)
            {
                if (connector.Status != MyShipConnectorStatus.Connected)
                {
                    continue;
                }

                if (names.Contains(connector.OtherConnector.CubeGrid.CustomName))
                {
                    continue;
                }

                names.Add(connector.OtherConnector.CubeGrid.CustomName);
            }

            return names;
        }

        public void Update(double time)
        {
            dockRequestTime += time;

            bool mainConnected = MainConnector.Status == MyShipConnectorStatus.Connected;

            string newShip = null;
            if (mainConnected)
            {
                newShip = MainConnector.OtherConnector.CubeGrid.CustomName;
            }

            bool moreThanOneShip = false;
            foreach (var con in Connectors)
            {
                if (con.Status != MyShipConnectorStatus.Connected) continue;

                string ship = con.OtherConnector.CubeGrid.CustomName;
                if (newShip != ship)
                {
                    moreThanOneShip = true;
                    break;
                }
            }

            //Update ship name
            DockedShipName = moreThanOneShip ? "Several ships" : newShip;

            if (!string.IsNullOrWhiteSpace(ReservedShipName) && DockedShipName == ReservedShipName)
            {
                //Clears reservation if the reserved ship has docked
                ReservedShipName = null;
            }
        }

        public bool DockRequest(string shipName)
        {
            if (waitingDock)
            {
                if (TimerDockPrepare?.IsCountingDown ?? false) return false;
                waitingDock = false;
                return true;
            }
            else
            {
                dockRequestTime = 0;
                ReservedShipName = shipName;
                TimerDockPrepare?.StartCountdown();
                TimerDockStart?.StartCountdown();
                waitingDock = true;
                return false;
            }
        }
        public bool UndockRequest()
        {
            if (TimerUndockPrepare == null) return true;

            if (waitingUndock)
            {
                if (TimerUndockPrepare.IsCountingDown) return false;
                waitingUndock = false;
                return true;
            }
            else
            {
                TimerUndockPrepare.StartCountdown();
                TimerUndockStart?.StartCountdown();
                waitingUndock = true;
                return false;
            }
        }

        public List<Vector3D> CalculateRouteToConnector()
        {
            var targetDock = MainConnector.GetPosition();

            var forward = Camera.WorldMatrix.Forward;
            var up = Camera.WorldMatrix.Up;

            return PathType == 0 ?
                CalculateStraightRoute(targetDock, forward, PathDistance, NumWaypoints) :
                CalculateCurveRoute(targetDock, forward, up, PathDistance, NumWaypoints);
        }
        public List<Vector3D> CalculateRouteFromConnector()
        {
            var waypoints = CalculateRouteToConnector();
            waypoints.Reverse();
            return waypoints;
        }
        static List<Vector3D> CalculateStraightRoute(Vector3D targetDock, Vector3D forward, double distance, int numWaypoints)
        {
            var waypoints = new List<Vector3D>();

            var offset = forward * 2.3;
            var approachStart = targetDock + forward * distance; //Initial approach point

            for (int i = 0; i <= numWaypoints; i++)
            {
                double t = i / (double)numWaypoints;

                var point = Vector3D.Lerp(approachStart, targetDock, t);
                waypoints.Add(point + offset);
            }

            return waypoints;
        }
        static List<Vector3D> CalculateCurveRoute(Vector3D targetDock, Vector3D forward, Vector3D up, double distance, int numWaypoints)
        {
            var waypoints = new List<Vector3D>();

            var offset = forward * 2.3;

            // Calculate the center of the curve, wich is located along the down direction of the target dock, taking account the forward direction to determine the correct side
            var center = targetDock + (-up * distance);

            // Calculate the entry point of the route. It is located over the center at a distance of radios, in the same direction as the forward vector
            var entryPoint = center + forward * distance;

            // Calculate the waypoints along the curve, from the entry point to the target dock
            for (int i = 0; i <= numWaypoints; i++)
            {
                double t = i / (double)numWaypoints;

                // Interpolate the curve between the entry point and the target dock, using a quarter of a circle arc (90 degrees) around the center
                var point = Vector3D.Transform(entryPoint - center, MatrixD.CreateFromAxisAngle(Vector3D.Cross(forward, up), MathHelper.PiOver2 * t)) + center;
                waypoints.Add(point + offset);
            }

            return waypoints;
        }

        public string GetState()
        {
            if (!string.IsNullOrWhiteSpace(ReservedShipName)) return $"Reserved - {ReservedShipName}";

            if (IsFree()) return "Free";

            return string.Join(", ", DockedShips());
        }

        public static string SaveListToStorage(List<ExchangeGroup> exchanges)
        {
            var exchangeList = string.Join("¬", exchanges.Select(e => e.SaveToStorage()).ToList());

            var parts = new List<string>
            {
                $"ExchangeCount={exchanges.Count}",
                $"Exchanges={exchangeList}",
            };

            return string.Join(";", parts);
        }
        string SaveToStorage()
        {
            var parts = new List<string>
            {
                $"Name={Name}",
                $"ReservedShipName={ReservedShipName}",
                $"DockedShipName={DockedShipName}",
                $"DockRequestTime={dockRequestTime}",
            };

            return string.Join("|", parts);
        }
        public static void LoadListFromStorage(string line, List<ExchangeGroup> exchanges)
        {
            string[] storageLines = line.Split(';');

            int exchangeCount = Utils.ReadInt(storageLines, "ExchangeCount");
            if (exchangeCount == 0) return;

            string exchangeList = Utils.ReadString(storageLines, "Exchanges");
            string[] exchangeLines = exchangeList.Split('¬');
            for (int i = 0; i < exchangeLines.Length; i++)
            {
                var parts = exchangeLines[i].Split('|');
                string name = Utils.ReadString(parts, "Name");
                string reservedShipName = Utils.ReadString(parts, "ReservedShipName");
                string dockedShipName = Utils.ReadString(parts, "DockedShipName");
                double dockRequestTime = Utils.ReadDouble(parts, "DockRequestTime");

                var exchange = exchanges.Find(e => e.Name == name);
                if (exchange != null)
                {
                    exchange.ReservedShipName = reservedShipName;
                    exchange.DockedShipName = dockedShipName;
                    exchange.dockRequestTime = dockRequestTime;
                }
            }
        }
    }
}
