﻿using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    class ExchangeGroup
    {
        private readonly Config config;
        private double dockRequestTime = 0;

        public string Name;
        public IMyShipConnector UpperConnector;
        public IMyShipConnector LowerConnector;
        public IMyCargoContainer Cargo;
        public IMyConveyorSorter SorterInput;
        public IMyConveyorSorter SorterOutput;
        public IMyTimerBlock TimerPrepare;
        public IMyTimerBlock TimerUnload;

        public string DockedShipName { get; private set; }
        public bool UpperConnected => UpperConnector.Status == MyShipConnectorStatus.Connected;
        public bool LowerConnected => LowerConnector == null || LowerConnector.Status == MyShipConnectorStatus.Connected;
        public string UpperShipName => UpperConnected ? UpperConnector.OtherConnector.CubeGrid.CustomName : null;
        public string LowerShipName => LowerConnected ? LowerConnector?.OtherConnector.CubeGrid.CustomName : UpperShipName;

        public ExchangeGroup(Config config)
        {
            this.config = config;
        }

        public bool IsValid()
        {
            return UpperConnector != null;
        }
        public bool IsFree()
        {
            return
                string.IsNullOrWhiteSpace(DockedShipName) &&
                UpperConnector.Status == MyShipConnectorStatus.Unconnected &&
                (LowerConnector?.Status ?? MyShipConnectorStatus.Unconnected) == MyShipConnectorStatus.Unconnected;
        }

        public bool Update(double time)
        {
            dockRequestTime += time;

            bool upperConnected = UpperConnected;
            bool lowerConnected = LowerConnected;

            string newShipU = null;
            if (upperConnected)
            {
                newShipU = UpperConnector.OtherConnector.CubeGrid.CustomName;
            }

            string newShipL = null;
            if (lowerConnected)
            {
                newShipL = LowerConnector?.OtherConnector.CubeGrid.CustomName ?? DockedShipName;
            }

            bool hasDockRequested = !string.IsNullOrWhiteSpace(DockedShipName) && dockRequestTime <= config.ExchangeDockRequestTimeThr;
            if (hasDockRequested)
            {
                if ((upperConnected && DockedShipName != newShipU) || (lowerConnected && DockedShipName != newShipL))
                {
                    return false;
                }
            }
            else
            {
                //Update ship name
                DockedShipName = newShipU ?? newShipL;
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
            List<Vector3D> waypoints = new List<Vector3D>();

            Vector3D targetDock = UpperConnector.GetPosition();   //Last point
            Vector3D forward = UpperConnector.WorldMatrix.Forward;
            Vector3D approachStart = targetDock + forward * config.ExchangePathDistance;  //Initial approach point

            for (int i = 0; i <= config.ExchangeNumWaypoints; i++)
            {
                double t = i / (double)config.ExchangeNumWaypoints;
                Vector3D point = Vector3D.Lerp(approachStart, targetDock, t) + forward * 2.3;
                waypoints.Add(point);
            }

            return waypoints;
        }
        public string MoveCargo(Order order, List<IMyCargoContainer> sourceCargos)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"## Exchange: {Name}. MoveCargo {order.Id}");

            if (Cargo == null)
            {
                sb.AppendLine($"## ERROR Cargo not found in Exchange.");
                return sb.ToString();
            }

            sb.AppendLine($"## Destination Cargo: {Cargo.CustomName}. Warehouse cargos: {sourceCargos.Count}");

            //Exchange charge where the order items have to go
            var dstInv = Cargo.GetInventory();

            foreach (var orderItem in order.Items)
            {
                var amountRemaining = orderItem.Value;
                sb.AppendLine($"## Item {orderItem.Key}: {orderItem.Value}.");

                //Search for items in the base containers
                foreach (var srcCargo in sourceCargos)
                {
                    var srcInv = srcCargo.GetInventory();
                    var srcItems = new List<MyInventoryItem>();
                    srcInv.GetItems(srcItems);

                    if (!srcInv.IsConnectedTo(dstInv))
                    {
                        sb.AppendLine($"## {srcCargo.CustomName} & {Cargo.CustomName} not connected.");
                        break;
                    }

                    for (int o = srcItems.Count - 1; o >= 0 && amountRemaining > 0; o--)
                    {
                        if (srcItems[o].Type.SubtypeId != orderItem.Key)
                        {
                            sb.AppendLine($"## WARNING Item {srcItems[o].Type.SubtypeId} discarded.");
                            continue;
                        }

                        var transferAmount = MyFixedPoint.Min(amountRemaining, srcItems[o].Amount);
                        sb.AppendLine($"## Moving {transferAmount}/{srcItems[o].Amount:F0} of {orderItem.Key} from {srcCargo.CustomName} to {Cargo.CustomName}.");

                        if (srcInv.TransferItemTo(dstInv, srcItems[o], transferAmount))
                        {
                            sb.AppendLine($"## Moved {transferAmount} of {orderItem.Key} from {srcCargo.CustomName}.");
                            amountRemaining -= transferAmount.ToIntSafe();
                            break;
                        }
                        else
                        {
                            sb.AppendLine($"## ERROR Cannot move {transferAmount} of {orderItem.Key} from {srcCargo.CustomName} to {Cargo.CustomName}.");
                        }
                    }
                }

                sb.AppendLine($"## Remaining {amountRemaining} of {orderItem.Key}.");
            }

            return sb.ToString();
        }

        public string SaveToStorage()
        {
            List<string> parts = new List<string>
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
