using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    class Listener
    {
        const string ListenerSep = "¬";

        readonly string name;
        readonly Route route;
        readonly List<IMyShipConnector> connectors;
        readonly List<IMyCargoContainer> outputCargos;
        readonly IMyTimerBlock timerOpen;
        readonly IMyTimerBlock timerClose;

        TimeSpan lastQuery = TimeSpan.Zero;
        bool preparing = false;
        string preparingData = null;
        bool closing = false;

        readonly StringBuilder lastQueryState = new StringBuilder();
        readonly StringBuilder state = new StringBuilder();

        public long SenderId { get; private set; } = 0;

        public Listener(string name, Route route, List<IMyShipConnector> connectors, List<IMyCargoContainer> outputCargos, IMyTimerBlock timerOpen, IMyTimerBlock timerClose)
        {
            this.name = name;
            this.route = route;
            this.connectors = connectors;

            this.outputCargos = outputCargos;
            this.timerOpen = timerOpen;
            this.timerClose = timerClose;
        }

        public void Prepare(long senderId, string items)
        {
            if (preparing) return;

            SenderId = senderId;
            lastQuery = TimeSpan.Zero;
            lastQueryState.Clear();

            if (!InventoryEmpty())
            {
                lastQueryState.AppendLine("  Output inventory not empty. Waiting for next query.");
                return;
            }

            if (!HasShipsConnected())
            {
                lastQueryState.AppendLine("  No ships connected. Waiting for next query.");
                return;
            }

            Open();

            preparingData = items;
            preparing = true;
        }
        public bool Preparing(List<IMyCargoContainer> warehouseCargos)
        {
            if (IsOpening()) return false;

            if (closing)
            {
                if (IsClosing()) return false;

                closing = false;
                lastQueryState.AppendLine($"  {timerClose?.CustomName ?? ""} finished.");
                preparing = false;
                preparingData = null;

                return true;
            }

            if (!preparing) return false;

            PrepareItems(warehouseCargos);

            if (Close()) return false;

            preparing = false;
            preparingData = null;

            return true;
        }
        void PrepareItems(List<IMyCargoContainer> warehouseCargos)
        {
            if (string.IsNullOrEmpty(preparingData)) return;

            var requestedItems = ReadItems(preparingData);
            if (requestedItems.Count == 0) return;

            bool anyMoved = false;
            foreach (var reqItem in requestedItems)
            {
                string itemType = reqItem.Key;
                int itemRemaining = reqItem.Value;

                foreach (var outputCargo in outputCargos)
                {
                    var outputInv = outputCargo.GetInventory();
                    var orderItems = GetItemsFromCargo(outputInv);
                    
                    int index = orderItems.FindIndex(i => i.Type.ToString().Contains(itemType));
                    if (index >= 0)
                    {
                        int c = (int)orderItems[index].Amount;
                        itemRemaining -= c;
                    }

                    lastQueryState.AppendLine($"- {itemType}: remaining {itemRemaining}");

                    //Search for that item in the containers
                    foreach (var cargo in warehouseCargos)
                    {
                        if (itemRemaining <= 0) break;

                        var inv = cargo.GetInventory();
                        var items = GetItemsFromCargo(inv);

                        foreach (var item in items)
                        {
                            if (itemRemaining <= 0) break;

                            if (!item.Type.ToString().Contains(itemType)) continue;

                            var toTransfer = VRage.MyFixedPoint.Min(item.Amount, itemRemaining);

                            bool moved = inv.TransferItemTo(outputInv, item, toTransfer);
                            if (moved)
                            {
                                anyMoved = true;
                                itemRemaining -= (int)toTransfer;
                                lastQueryState.AppendLine($"  Transfered {(int)toTransfer}");
                            }
                        }
                    }
                }

                lastQueryState.AppendLine($"  {(itemRemaining > 0 ? $"Missing {itemRemaining}" : "All transfered")}");
            }

            if (!anyMoved)
            {
                lastQueryState.AppendLine("- No items moved");
            }
        }
        static Dictionary<string, int> ReadItems(string data)
        {
            Dictionary<string, int> requestedItems = new Dictionary<string, int>();

            var parts = data.Split(';');
            foreach (var part in parts)
            {
                string[] items = part.Split('=');
                if (items.Length != 2) continue;

                string item = items[0].Trim();
                if (item.ToUpper() == "ANY") continue;

                int amount = (int)decimal.Parse(items[1].Trim());
                requestedItems.Add(item, amount);
            }

            return requestedItems;
        }
        static List<MyInventoryItem> GetItemsFromCargo(IMyInventory cargoInv)
        {
            var items = new List<MyInventoryItem>();
            cargoInv.GetItems(items);
            return items;
        }

        void Open()
        {
            if (timerOpen == null) return;

            timerOpen.StartCountdown();
            lastQueryState.AppendLine($"  {timerOpen.CustomName} started.");
        }
        bool IsOpening()
        {
            return timerOpen?.IsCountingDown ?? false;
        }
        bool Close()
        {
            if (timerClose == null) return false;

            timerClose.StartCountdown();
            lastQueryState.AppendLine($"  {timerClose.CustomName} started.");
            closing = true;

            return true;
        }
        bool IsClosing()
        {
            return timerClose?.IsCountingDown ?? false;
        }
        public List<string> GetConnectedShips()
        {
            var connected = connectors.FindAll(c => c?.OtherConnector?.IsConnected == true);

            return connected
                .Select(c => c.OtherConnector.CubeGrid.CustomName)
                .Distinct()
                .ToList();
        }
        public bool HasShipsConnected()
        {
            return connectors.Any(c => c.OtherConnector != null && c.OtherConnector.IsConnected);
        }
        public List<List<string>> FreeConnectors()
        {
            var res = new List<List<string>>();

            var ships = GetConnectedShips();
            foreach (var ship in ships)
            {
                lastQueryState.AppendLine($"  {name}: {ship} route sent.");

                //Set the route on the ship
                var parts = new List<string>()
                {
                    $"Command=SET_ROUTE",
                    $"From={name}",
                    $"To={ship}",
                    $"LoadBase={route.LoadBase}",
                    $"LoadBaseOnPlanet={(route.LoadBaseOnPlanet?1:0)}",
                    $"ToLoadBaseWaypoints={Utils.VectorListToStr(route.ToLoadBaseWaypoints)}",
                    $"UnloadBase={route.UnloadBase}",
                    $"UnloadBaseOnPlanet={(route.UnloadBaseOnPlanet?1:0)}",
                    $"ToUnloadBaseWaypoints={Utils.VectorListToStr(route.ToUnloadBaseWaypoints)}",
                };
                res.Add(parts);
            }

            return res;
        }

        public bool InventoryEmpty()
        {
            foreach (var cargo in outputCargos)
            {
                var inv = cargo.GetInventory();
                if (inv.CurrentVolume > 0)
                {
                    return false;
                }
            }
            return true;
        }

        public string GetState(TimeSpan time)
        {
            lastQuery += time;

            state.Clear();
            state.AppendLine($"+ {name} {route?.GetState() ?? "No route defined. Using ship's default."}");

            string error;
            if (!IsValid(out error))
            {
                state.AppendLine(error);
                return state.ToString();
            }

            if (lastQuery.Ticks > 0)
            {
                state.AppendLine($"  Last message received {lastQuery:d\\.hh\\:mm\\:ss} days ago.");
            }
            else if (!preparing)
            {
                state.AppendLine("  Idle.");
            }

            if (lastQueryState.Length > 0)
            {
                state.Append(lastQueryState.ToString());
            }

            return state.ToString();
        }
        bool IsValid(out string errorMsg)
        {
            errorMsg = null;
            if (outputCargos.Count == 0)
            {
                errorMsg = $"No output cargos found with name {name}";
            }
            if (timerOpen == null)
            {
                errorMsg = $"No open timer found with name {name}";
            }
            if (timerClose == null)
            {
                errorMsg = $"No close timer found with name {name}";
            }
            if (connectors.Count == 0)
            {
                errorMsg = $"No connectors found with name {name}";
                return false;
            }
            return true;
        }

        public void LoadFromStorage(string storageLine)
        {
            if (string.IsNullOrWhiteSpace(storageLine)) return;
            string[] storageLines = storageLine.Split(ListenerSep.ToCharArray());

            SenderId = Utils.ReadLong(storageLines, "senderId", 0);
            lastQuery = new TimeSpan(Utils.ReadLong(storageLines, "lastQuery", 0));
            preparing = Utils.ReadInt(storageLines, "preparing", 0) == 1;
            preparingData = Utils.ReadString(storageLines, "preparingData", null);
            closing = Utils.ReadInt(storageLines, "closing", 0) == 1;
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"senderId={SenderId}",
                $"lastQuery={lastQuery.Ticks}",
                $"preparing={(preparing ? 1 : 0)}",
                $"preparingData={preparingData}",
                $"closing={(closing ? 1 : 0)}",
            };

            return string.Join(ListenerSep, parts);
        }
    }
}
