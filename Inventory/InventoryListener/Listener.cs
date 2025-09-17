using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    class Listener
    {
        const string ListenerSep = "¬";

        readonly string name;
        readonly IMyCargoContainer outputCargo;
        readonly IMyTimerBlock timerOpen;
        readonly IMyTimerBlock timerClose;
        DateTime lastQuery = DateTime.MinValue;
        bool preparing = false;
        string preparingData = null;

        readonly StringBuilder lastQueryState = new StringBuilder();
        readonly StringBuilder state = new StringBuilder();

        public long SenderId { get; private set; } = 0;

        public Listener(string name, IMyCargoContainer outputCargo, IMyTimerBlock timerOpen, IMyTimerBlock timerClose)
        {
            this.name = name;
            this.outputCargo = outputCargo;
            this.timerOpen = timerOpen;
            this.timerClose = timerClose;
        }

        public void Prepare(long senderId, string items)
        {
            if (preparing) return;

            SenderId = senderId;
            lastQuery = DateTime.Now;
            lastQueryState.Clear();

            timerOpen.StartCountdown();
            preparingData = items;

            preparing = true;
        }
        public bool Preparing(List<IMyCargoContainer> warehouseCargos)
        {
            if (!preparing) return false;

            if (timerOpen.IsCountingDown) return false;

            var requestedItems = ReadItems(preparingData);
            var outputInv = outputCargo.GetInventory();
            var orderItems = GetItemsFromCargo(outputInv);

            //Go through each requested item
            bool anyMoved = false;
            foreach (var reqItem in requestedItems)
            {
                string itemType = reqItem.Key;
                int itemRemaining = reqItem.Value;

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

                lastQueryState.AppendLine($"  {(itemRemaining > 0 ? $"Missing {itemRemaining}" : "All transfered")}");
            }

            if (!anyMoved)
            {
                lastQueryState.AppendLine("- No items moved");
            }

            timerClose.StartCountdown();
            lastQueryState.AppendLine($"  {timerClose.CustomName} started.");

            preparing = false;
            preparingData = null;

            return true;
        }
        static Dictionary<string, int> ReadItems(string data)
        {
            Dictionary<string, int> requestedItems = new Dictionary<string, int>();

            var parts = data.Split(';');
            foreach (var part in parts)
            {
                string[] items = part.Split('=');
                if (items.Length != 2)
                {
                    continue;
                }

                string item = items[0].Trim();
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

        public string GetState()
        {
            state.Clear();
            state.Append($"+ {name} ");

            string error;
            if (!IsValid(out error))
            {
                state.AppendLine(error);
                return state.ToString();
            }

            if (lastQuery.Ticks > 0)
            {
                state.AppendLine($"Last message received {(int)(DateTime.Now - lastQuery).TotalMinutes} minutes ago.");
            }
            else if (!preparing)
            {
                state.AppendLine("Idle.");
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
            if (outputCargo == null)
            {
                errorMsg = $"No output cargo found with name {name}";
                return false;
            }
            if (timerOpen == null)
            {
                errorMsg = $"No open timer found with name {name}";
                return false;
            }
            if (timerClose == null)
            {
                errorMsg = $"No close timer found with name {name}";
                return false;
            }
            return true;
        }

        public void LoadFromStorage(string storageLine)
        {
            if (string.IsNullOrWhiteSpace(storageLine)) return;
            string[] storageLines = storageLine.Split(ListenerSep.ToCharArray());

            SenderId = Utils.ReadLong(storageLines, "senderId", 0);
            lastQuery = new DateTime(Utils.ReadLong(storageLines, "lastQuery", 0));
            preparing = Utils.ReadInt(storageLines, "preparing", 0) == 1;
            preparingData = Utils.ReadString(storageLines, "preparingData", null);
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"senderId={SenderId}",
                $"lastQuery={lastQuery.Ticks}",
                $"preparing={(preparing ? 1 : 0)}",
                $"preparingData={preparingData}"
            };

            return string.Join(ListenerSep, parts);
        }
    }
}
