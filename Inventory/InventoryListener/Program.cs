using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "1.0";
        const char AttributeSep = '=';
        const string WildcardLCDs = "[INV]";
        const string MessageCallback = "InventorySystem";

        readonly IMyCargoContainer outputCargo;
        readonly List<IMyCargoContainer> warehouseCargos;
        readonly IMyTimerBlock timerOpen;
        readonly IMyTimerBlock timerClose;
        readonly List<IMyTextPanel> infoLCDs;

        readonly IMyBroadcastListener bl;
        readonly string channel;

        DateTime lastQuery = DateTime.MinValue;
        readonly StringBuilder lastQueryState = new StringBuilder();
        bool preparing = false;
        string preparingData = null;

        readonly StringBuilder infoText = new StringBuilder();

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "Channel=name\n" +
                    "OutputCargo=name\n" +
                    "InventoryCargo=name\n" +
                    "TimerOpen=name\n" +
                    "TimerClose=name\n" +
                    "WildcardLCDs=name(optional)";

                Echo("CustomData not set.");
                return;
            }

            channel = ReadConfig(Me.CustomData, "Channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                Echo("Channel not set.");
                return;
            }

            string outputCargoName = ReadConfig(Me.CustomData, "OutputCargo");
            if (string.IsNullOrWhiteSpace(outputCargoName))
            {
                Echo("OutputCargo name not set.");
                return;
            }

            string inventoryCargoName = ReadConfig(Me.CustomData, "InventoryCargo");
            if (string.IsNullOrWhiteSpace(inventoryCargoName))
            {
                Echo("InventoryCargo name not set.");
                return;
            }

            string timerOpenName = ReadConfig(Me.CustomData, "TimerOpen");
            if (string.IsNullOrWhiteSpace(timerOpenName))
            {
                Echo("TimerOpen name not set.");
                return;
            }

            string timerCloseName = ReadConfig(Me.CustomData, "TimerClose");
            if (string.IsNullOrWhiteSpace(timerCloseName))
            {
                Echo("TimerClose name not set.");
                return;
            }

            //Get the output container
            outputCargo = GetBlockWithName<IMyCargoContainer>(outputCargoName);
            if (outputCargo == null)
            {
                Echo($"No output cargo found with name {outputCargoName}");
                return;
            }

            //Get all input containers
            warehouseCargos = GetBlocksOfType<IMyCargoContainer>(inventoryCargoName);
            if (warehouseCargos.Count == 0)
            {
                Echo($"No warehouse cargo containers found with name {inventoryCargoName}");
                return;
            }

            timerOpen = GetBlockWithName<IMyTimerBlock>(timerOpenName);
            if (timerOpen == null)
            {
                Echo($"No timer found with name {timerOpenName}");
            }

            timerClose = GetBlockWithName<IMyTimerBlock>(timerCloseName);
            if (timerClose == null)
            {
                Echo($"No timer found with name {timerCloseName}");
            }

            string wildcard = ReadConfig(Me.CustomData, "WildcardLCDs") ?? WildcardLCDs;
            infoLCDs = GetBlocksOfType<IMyTextPanel>(wildcard);

            bl = IGC.RegisterBroadcastListener(channel);
            bl.SetMessageCallback(MessageCallback);
            Echo($"Listener registered on {channel}");

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) != 0 && argument == MessageCallback)
            {
                while (bl.HasPendingMessage)
                {
                    var msg = bl.AcceptMessage();
                    Prepare(msg.Data.ToString());
                }
            }

            if (preparing)
            {
                if (timerOpen.IsCountingDown) return;
                Preparing();
                return;
            }

            infoText.Clear();
            infoText.AppendLine($"Inventory Listener v{Version} - {channel}. {DateTime.Now:HH:mm:ss}");
            if (lastQuery.Ticks > 0) infoText.AppendLine($"Last message received {(int)(DateTime.Now - lastQuery).TotalMinutes} minutes ago.");
            infoText.AppendLine(lastQueryState.ToString());

            WriteInfo();
        }

        void Prepare(string data)
        {
            if (preparing) return;

            lastQuery = DateTime.Now;
            lastQueryState.Clear();

            if (string.IsNullOrWhiteSpace(data))
            {
                lastQueryState.AppendLine("No components Needed.");
                return;
            }

            timerOpen.StartCountdown();
            preparing = true;
            preparingData = data;
        }
        void Preparing()
        {
            if (!preparing) return;

            if (timerOpen.IsCountingDown) return;

            var requestedItems = ReadItems(preparingData);
            var outputInv = outputCargo.GetInventory();
            var orderItems = GetItemsFromCargo(outputInv);

            lastQueryState.AppendLine("Preparing Cargo...");

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
                            lastQueryState.AppendLine($"Transfered {(int)toTransfer} of {item.Type}");
                        }
                    }
                }

                lastQueryState.AppendLine($"{itemType}: {(itemRemaining > 0 ? $"Missing {itemRemaining}" : "Transfered")}");
            }

            if (!anyMoved)
            {
                lastQueryState.AppendLine("No items moved");
            }

            timerClose.StartCountdown();

            preparing = false;
            preparingData = null;
        }
        Dictionary<string, int> ReadItems(string data)
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
        List<MyInventoryItem> GetItemsFromCargo(IMyInventory cargoInv)
        {
            var items = new List<MyInventoryItem>();
            cargoInv.GetItems(items);
            return items;
        }

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName == name);
            return blocks.FirstOrDefault();
        }
        List<T> GetBlocksOfType<T>(string filter) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(filter));
            return blocks;
        }
        void WriteInfo()
        {
            Echo(infoText.ToString());

            foreach (var lcd in infoLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(infoText);
            }
        }

        static string ReadConfig(string customData, string name)
        {
            string[] config = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return config.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            lastQuery = new DateTime(ReadLong(storageLines, "lastQuery", 0));
            preparing = ReadInt(storageLines, "preparing", 0) == 1;
            preparingData = ReadString(storageLines, "preparingData", null);
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"lastQuery={lastQuery.Ticks}",
                $"preparing={(preparing ? 1 : 0)}",
                $"preparingData={preparingData}"
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
        static string ReadString(string[] lines, string name, string defaultValue = "")
        {
            string cmdToken = $"{name}{AttributeSep}";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return value;
        }
        static int ReadInt(string[] lines, string name, int defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return int.Parse(value);
        }
        static long ReadLong(string[] lines, string name, long defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return long.Parse(value);
        }
    }
}
