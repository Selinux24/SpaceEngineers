using Sandbox.ModAPI.Ingame;
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
        const int NumWaypoints = 5;

        public string Name;
        public string DockedShipName; //TODO: Guardar en Storage
        public IMyShipConnector UpperConnector;
        public IMyShipConnector LowerConnector;
        public IMyCargoContainer Cargo;
        public IMyConveyorSorter SorterInput;
        public IMyConveyorSorter SorterOutput;
        public IMyTimerBlock TimerPrepare;
        public IMyTimerBlock TimerUnload;

        public bool IsValid()
        {
            return
                UpperConnector != null &&
                SorterInput != null &&
                SorterOutput != null;
        }
        public bool IsFree()
        {
            return
                string.IsNullOrWhiteSpace(DockedShipName) &&
                UpperConnector.Status == MyShipConnectorStatus.Unconnected &&
                (LowerConnector?.Status ?? MyShipConnectorStatus.Unconnected) == MyShipConnectorStatus.Unconnected;
        }
        public List<Vector3D> CalculateRouteToConnector()
        {
            List<Vector3D> waypoints = new List<Vector3D>();

            Vector3D targetDock = UpperConnector.GetPosition();   // Punto final
            Vector3D forward = UpperConnector.WorldMatrix.Forward;
            Vector3D approachStart = targetDock + forward * 150;  // Punto de aproximación inicial

            for (int i = 0; i <= NumWaypoints; i++)
            {
                double t = i / (double)NumWaypoints;
                Vector3D point = Vector3D.Lerp(approachStart, targetDock, t) + forward * 2.3;
                waypoints.Add(point);
            }

            return waypoints;
        }
        public string GetApproachingWaypoints()
        {
            return string.Join(";", CalculateRouteToConnector().Select(Utils.VectorToStr));
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

            //Cargo del exchange a donde tienen que ir los items del pedido
            var dstInv = Cargo.GetInventory();

            foreach (var orderItem in order.Items)
            {
                var amountRemaining = orderItem.Value;
                sb.AppendLine($"## Item {orderItem.Key}: {orderItem.Value}.");

                // Buscar ítem en los contenedores de la base
                foreach (var srcCargo in sourceCargos)
                {
                    var srcInv = srcCargo.GetInventory();
                    var srcItems = new List<MyInventoryItem>();
                    srcInv.GetItems(srcItems);

                    if (!srcInv.IsConnectedTo(dstInv))
                    {
                        sb.AppendLine($"## Los cargos {srcCargo.CustomName} y {Cargo.CustomName} no están conectados.");
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

                var exchange = exchanges.Find(e => e.Name == name);
                if (exchange != null)
                {
                    exchange.DockedShipName = dockedShipName;
                }
            }
        }
    }
}
