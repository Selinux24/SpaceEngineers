using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    class ExchangeGroup
    {
        private readonly Config config;
        private double dockRequestTime = 0;

        public string Name;
        public IMyShipConnector MainConnector;
        public readonly List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
        public IMyCameraBlock Camera;
        public Vector3D Forward => Camera.WorldMatrix.Forward;
        public Vector3D Up => Camera.WorldMatrix.Up;

        public string DockedShipName { get; private set; }
        public string ReservedShipName { get; private set; }

        public ExchangeGroup(Config config)
        {
            this.config = config;
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
            if (!string.IsNullOrWhiteSpace(DockedShipName) &&
                MainConnector.Status != MyShipConnectorStatus.Unconnected)
            {
                return false;
            }

            foreach (var connector in Connectors)
            {
                if (connector.Status != MyShipConnectorStatus.Unconnected)
                {
                    return false;
                }
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

        public void DockRequest(string shipName)
        {
            dockRequestTime = 0;
            ReservedShipName = shipName;
        }

        public List<Vector3D> CalculateRouteToConnector()
        {
            var waypoints = new List<Vector3D>();

            var targetDock = MainConnector.GetPosition();   //Last point
            var forward = MainConnector.WorldMatrix.Forward;
            var approachStart = targetDock + forward * config.ExchangePathDistance;  //Initial approach point

            for (int i = 0; i <= config.ExchangeNumWaypoints; i++)
            {
                double t = i / (double)config.ExchangeNumWaypoints;
                var point = Vector3D.Lerp(approachStart, targetDock, t) + forward * 2.3;
                waypoints.Add(point);
            }

            return waypoints;
        }
        public List<Vector3D> CalculateRouteFromConnector()
        {
            var waypoints = CalculateRouteToConnector();
            waypoints.Reverse();
            return waypoints;
        }

        public string GetState()
        {
            bool isFree = IsFree();
            return isFree ? "Free" : string.IsNullOrWhiteSpace(ReservedShipName) ? string.Join(", ", DockedShips()) : ReservedShipName;
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
