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
        readonly List<IMyCargoContainer> outContainers;
        readonly IMyTimerBlock timerOpen;
        readonly IMyTimerBlock timerClose;

        TimeSpan lastQuery = TimeSpan.Zero;
        bool preparing = false;
        string preparingData = null;
        bool closing = false;
        bool prepared = true;

        readonly StringBuilder lastQueryState = new StringBuilder();
        readonly StringBuilder state = new StringBuilder();

        public long SenderId { get; private set; } = 0;

        public Listener(string name, Route route, List<IMyShipConnector> connectors, List<IMyCargoContainer> outContainers, IMyTimerBlock timerOpen, IMyTimerBlock timerClose)
        {
            this.name = name;
            this.route = route;
            this.connectors = connectors;
            this.outContainers = outContainers;
            this.timerOpen = timerOpen;
            this.timerClose = timerClose;
        }

        public bool Start(long senderId, string items, List<IMyCargoContainer> containers, out string reason)
        {
            reason = null;

            if (preparing) return false;

            SenderId = senderId;
            lastQuery = TimeSpan.Zero;
            lastQueryState.Clear();

            if (!InventoryEmpty())
            {
                lastQueryState.AppendLine("  Output inventory not empty. Waiting for next query.");
                reason = "Output inventory not empty";
                return false;
            }

            if (!HasShipsConnected())
            {
                lastQueryState.AppendLine("  No ships connected. Waiting for next query.");
                reason = "No ships connected";
                return false;
            }

            if (!HasItems(items, containers))
            {
                lastQueryState.AppendLine("  No items available. Waiting for next query.");
                reason = "No items available";
                return false;
            }

            Open();

            prepared = false;
            preparingData = items;
            preparing = true;

            return true;
        }
        bool InventoryEmpty()
        {
            foreach (var c in outContainers)
            {
                var inv = c.GetInventory();
                if (inv.CurrentVolume > 0)
                {
                    return false;
                }
            }
            return true;
        }
        bool HasShipsConnected()
        {
            return connectors.Any(c => c.OtherConnector != null && c.OtherConnector.IsConnected);
        }
        static bool HasItems(string data, List<IMyCargoContainer> containers)
        {
            if (string.IsNullOrEmpty(data)) return false;

            var reqItems = ReadItems(data);
            if (reqItems.Count == 0) return false;

            bool hasItems = false;

            foreach (var reqItem in reqItems)
            {
                string itemType = reqItem.Key;

                foreach (var c in containers)
                {
                    var inv = c.GetInventory();
                    var items = GetItemsFromCargo(inv);

                    foreach (var item in items)
                    {
                        if (!item.Type.ToString().Contains(itemType)) continue;

                        hasItems = true;
                    }
                }
            }

            return hasItems;
        }
        void Open()
        {
            if (timerOpen == null) return;

            timerOpen.StartCountdown();
            lastQueryState.AppendLine($"  {timerOpen.CustomName} started.");
        }

        public bool Preparing(List<IMyCargoContainer> containers)
        {
            if (IsOpening()) return false;

            if (closing)
            {
                if (IsClosing()) return false;

                closing = false;
                lastQueryState.AppendLine($"  {timerClose?.CustomName ?? ""} finished.");
                preparing = false;
                preparingData = null;
                prepared = true;
                return true;
            }

            if (!preparing) return false;

            PrepareItems(containers);

            if (Close()) return false;

            preparing = false;
            preparingData = null;
            prepared = true;
            return true;
        }
        bool IsOpening()
        {
            return timerOpen?.IsCountingDown ?? false;
        }
        bool IsClosing()
        {
            return timerClose?.IsCountingDown ?? false;
        }
        void PrepareItems(List<IMyCargoContainer> containers)
        {
            if (string.IsNullOrEmpty(preparingData)) return;

            var requestedItems = ReadItems(preparingData);
            if (requestedItems.Count == 0) return;

            bool anyMoved = false;
            foreach (var reqItem in requestedItems)
            {
                string itemType = reqItem.Key;
                int itemRemaining = reqItem.Value;

                foreach (var outC in outContainers)
                {
                    var outInv = outC.GetInventory();
                    var orderItems = GetItemsFromCargo(outInv);

                    int index = orderItems.FindIndex(i => i.Type.ToString().Contains(itemType));
                    if (index >= 0)
                    {
                        int c = (int)orderItems[index].Amount;
                        itemRemaining -= c;
                    }

                    lastQueryState.AppendLine($"- {itemType}: remaining {itemRemaining}");

                    //Search for that item in the containers
                    foreach (var c in containers)
                    {
                        if (itemRemaining <= 0) break;

                        var inv = c.GetInventory();
                        var items = GetItemsFromCargo(inv);

                        foreach (var item in items)
                        {
                            if (itemRemaining <= 0) break;

                            if (!item.Type.ToString().Contains(itemType)) continue;

                            var toTransfer = VRage.MyFixedPoint.Min(item.Amount, itemRemaining);

                            bool moved = inv.TransferItemTo(outInv, item, toTransfer);
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
        bool Close()
        {
            if (timerClose == null) return false;

            timerClose.StartCountdown();
            lastQueryState.AppendLine($"  {timerClose.CustomName} started.");
            closing = true;

            return true;
        }

        static Dictionary<string, int> ReadItems(string data)
        {
            Dictionary<string, int> requestedItems = new Dictionary<string, int>();

            var parts = data.Split(';');
            foreach (var part in parts)
            {
                string[] items = part.Split('=');

                if (items.Length < 1) continue;
                string item = items[0].Trim();
                if (item.ToUpper() == "ANY") continue;

                if (items.Length != 2) continue;
                int amount = (int)decimal.Parse(items[1].Trim());
                requestedItems.Add(item, amount);
            }

            return requestedItems;
        }
        static List<MyInventoryItem> GetItemsFromCargo(IMyInventory inv)
        {
            var items = new List<MyInventoryItem>();
            inv.GetItems(items);
            return items;
        }

        public void Reset()
        {
            lastQuery = TimeSpan.Zero;
            lastQueryState.Clear();
            preparing = false;
            preparingData = null;
            closing = false;
            prepared = true;
        }

        public bool Prepared()
        {
            return !preparing || prepared;
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
        List<string> GetConnectedShips()
        {
            var connected = connectors.FindAll(c => c?.OtherConnector?.IsConnected == true);

            return connected
                .Select(c => c.OtherConnector.CubeGrid.CustomName)
                .Distinct()
                .ToList();
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
            if (outContainers.Count == 0)
            {
                errorMsg = $"No output cargos containers found with name [{name}]";
            }
            if (timerOpen == null)
            {
                errorMsg = $"No open timer found with name [{name}]";
            }
            if (timerClose == null)
            {
                errorMsg = $"No close timer found with name [{name}]";
            }
            if (connectors.Count == 0)
            {
                errorMsg = $"No connectors found with name [{name}]";
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
            prepared = Utils.ReadInt(storageLines, "prepared", 0) == 1;
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
                $"prepared={(prepared ? 1 : 0)}",
            };

            return string.Join(ListenerSep, parts);
        }
    }
}
