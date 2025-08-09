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

        public ExchangeGroup(Config config)
        {
            this.config = config;
        }

        public bool IsValid()
        {
            return MainConnector != null && Camera != null;
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
                names.Add(MainConnector.OtherConnector.CubeGrid.Name);
            }

            foreach (var connector in Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                {
                    names.Add(connector.OtherConnector.CubeGrid.Name);
                }
            }

            return names;
        }

        public bool Update(double time)
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

            bool hasDockRequested = !string.IsNullOrWhiteSpace(DockedShipName) && dockRequestTime <= config.ExchangeDockRequestTimeThr;
            if (hasDockRequested)
            {
                if ((mainConnected && DockedShipName != newShip) || moreThanOneShip)
                {
                    //Another ship is docked at least. Not valid for dock
                    return false;
                }
            }
            else
            {
                //Update ship name
                DockedShipName = moreThanOneShip ? "Several ships" : newShip;
            }

            return true;
        }

        public void DockRequest(string shipName)
        {
            dockRequestTime = 0;
            DockedShipName = shipName;
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

        public string SaveToStorage()
        {
            var parts = new List<string>
            {
                $"Name={Name}",
                $"DockedShipName={DockedShipName}",
                $"DockRequestTime={dockRequestTime}",
            };

            return string.Join("|", parts);
        }

        public static List<string> SaveListToStorage(List<ExchangeGroup> exchanges)
        {
            var exchangeList = string.Join("¬", exchanges.Select(e => e.SaveToStorage()).ToList());

            return new List<string>
            {
                $"ExchangeCount={exchanges.Count}",
                $"Exchanges={exchangeList}",
            };
        }
        public static void LoadListFromStorage(string[] storageLines, List<ExchangeGroup> exchanges)
        {
            int exchangeCount = Utils.ReadInt(storageLines, "ExchangeCount");
            if (exchangeCount == 0) return;

            string exchangeList = Utils.ReadString(storageLines, "Exchanges");
            string[] exchangeLines = exchangeList.Split('¬');
            for (int i = 0; i < exchangeLines.Length; i++)
            {
                var parts = exchangeLines[i].Split('|');
                string name = Utils.ReadString(parts, "Name");
                string dockedShipName = Utils.ReadString(parts, "DockedShipName");
                double dockRequestTime = Utils.ReadDouble(parts, "DockRequestTime");

                var exchange = exchanges.Find(e => e.Name == name);
                if (exchange != null)
                {
                    exchange.DockedShipName = dockedShipName;
                    exchange.dockRequestTime = dockRequestTime;
                }
            }
        }
    }
}
