using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string MessageCallback = "InventorySystem";

        readonly IMyBroadcastListener bl;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData = 
                    "Channel=name\n" +
                    "OutputCargo=name\n" +
                    "InventoryCargo=name\n" +
                    "InventoryTimer=name";

                Echo("CustomData not set.");
                return;
            }

            string channel = ReadConfig(Me.CustomData, "Channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                Echo("Channel not set.");
                return;
            }

            bl = IGC.RegisterBroadcastListener(channel);
            bl.SetMessageCallback(MessageCallback);
            Echo($"Listener registered on {channel}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) == 0 || argument != MessageCallback)
            {
                return;
            }

            while (bl.HasPendingMessage)
            {
                var msg = bl.AcceptMessage();
                var requestedItems = ReadItems(msg.Data.ToString());

                Prepare(requestedItems);
            }
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
        void Prepare(Dictionary<string, int> requestedItems)
        {
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

            string timerName = ReadConfig(Me.CustomData, "InventoryTimer");
            if (string.IsNullOrWhiteSpace(timerName))
            {
                Echo("InventoryTimer name not set.");
                return;
            }

            // Obtener el contenedor de salida
            var outputCargo = GetBlockWithName<IMyCargoContainer>(outputCargoName);
            if (outputCargo == null)
            {
                Echo("Output cargo not found.");
                return;
            }

            // Obtener todos los contenedores de entrada
            var warehouseCargos = GetBlocksOfType<IMyCargoContainer>(inventoryCargoName);
            if (warehouseCargos.Count == 0)
            {
                Echo("No warehouse cargo containers found.");
                return;
            }

            var outputInv = outputCargo.GetInventory();
            var orderItems = GetItemsFromCargo(outputInv);

            // Recorrer cada item solicitado
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
                            itemRemaining -= (int)toTransfer;
                            Echo($"Transfered {(int)toTransfer} of {item.Type}");
                        }
                    }
                }

                Echo($"{itemType}: {(itemRemaining > 0 ? $"Missing {itemRemaining}" : "Transfered")}");
            }

            GetBlockWithName<IMyTimerBlock>(timerName).Trigger();
            Echo($"{timerName} triggered");
        }
        List<T> GetBlocksOfType<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks;
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
        static string ReadConfig(string customData, string name)
        {
            string[] config = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return config.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }
    }
}
