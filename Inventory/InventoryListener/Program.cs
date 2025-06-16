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
        const string MessageCallback = "InventorySystem";
        const string WildcardLCDs = "[INV]";

        readonly IMyBroadcastListener bl;
        readonly string channel;
        readonly StringBuilder sb = new StringBuilder();

        DateTime lastQuery = DateTime.MinValue;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "Channel=name\n" +
                    "OutputCargo=name\n" +
                    "InventoryCargo=name\n" +
                    "InventoryTimer=name\n" +
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

            bl = IGC.RegisterBroadcastListener(channel);
            bl.SetMessageCallback(MessageCallback);
            Echo($"Listener registered on {channel}");

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Echo("Working...");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) != 0 && argument == MessageCallback)
            {
                while (bl.HasPendingMessage)
                {
                    var msg = bl.AcceptMessage();
                    Prepare(msg.Data.ToString());
                    lastQuery = DateTime.Now;
                }
            }

            string wildcard = ReadConfig(Me.CustomData, "WildcardLCDs") ?? WildcardLCDs;
            var infoLCDs = GetBlocksOfType<IMyTextPanel>(wildcard);
            WriteInfoLCDs(infoLCDs);
        }
        void Prepare(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                WriteText("No components Needed.", false);
                return;
            }

            string outputCargoName = ReadConfig(Me.CustomData, "OutputCargo");
            if (string.IsNullOrWhiteSpace(outputCargoName))
            {
                WriteText("OutputCargo name not set.", false);
                return;
            }

            string inventoryCargoName = ReadConfig(Me.CustomData, "InventoryCargo");
            if (string.IsNullOrWhiteSpace(inventoryCargoName))
            {
                WriteText("InventoryCargo name not set.", false);
                return;
            }

            string timerName = ReadConfig(Me.CustomData, "InventoryTimer");
            if (string.IsNullOrWhiteSpace(timerName))
            {
                WriteText("InventoryTimer name not set.", false);
                return;
            }

            // Obtener el contenedor de salida
            var outputCargo = GetBlockWithName<IMyCargoContainer>(outputCargoName);
            if (outputCargo == null)
            {
                WriteText($"No output cargo found with name {outputCargoName}", false);
                return;
            }

            // Obtener todos los contenedores de entrada
            var warehouseCargos = GetBlocksOfType<IMyCargoContainer>(inventoryCargoName);
            if (warehouseCargos.Count == 0)
            {
                WriteText($"No warehouse cargo containers found with name {inventoryCargoName}", false);
                return;
            }

            var requestedItems = ReadItems(data);
            var outputInv = outputCargo.GetInventory();
            var orderItems = GetItemsFromCargo(outputInv);

            WriteText("Preparing Cargo...", false);

            // Recorrer cada item solicitado
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

                // Buscar ese item en los contenedores
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
                            WriteText($"Transfered {(int)toTransfer} of {item.Type}");
                        }
                    }
                }

                WriteText($"{itemType}: {(itemRemaining > 0 ? $"Missing {itemRemaining}" : "Transfered")}");
            }

            if (!anyMoved)
            {
                WriteText("No items moved");
                return;
            }

            var timer = GetBlockWithName<IMyTimerBlock>(timerName);
            if (timer == null)
            {
                WriteText($"No timer found with name {timerName}");
            }

            timer.Trigger();
            WriteText($"{timerName} triggered");
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
        void WriteText(string text, bool append = true)
        {
            Echo(text);

            if (!append)
            {
                sb.Clear();
            }

            sb.AppendLine(text);
        }
        void WriteInfoLCDs(List<IMyTextPanel> lcds)
        {
            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText($"Inventory Listener - {channel}. {DateTime.Now:HH:mm:ss}" + Environment.NewLine);
                lcd.WriteText($"Last query {DateTime.Now - lastQuery:hh\\:mm\\:ss}" + Environment.NewLine, true);
                lcd.WriteText(sb.ToString(), true);
            }
        }

        static string ReadConfig(string customData, string name)
        {
            string[] config = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return config.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }
    }
}
