using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string WildcardLCDs = "[INV]";

        readonly TimeSpan QueryInterval = TimeSpan.FromMinutes(10);

        DateTime lastQuery = DateTime.MinValue;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "Channel=name\n" +
                    "CargoContainerName=name\n" +
                    "Inventory=item1:quantity1;itemN:quantityN;";

                Echo("CustomData not set.");
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Echo("Working...");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var time = DateTime.Now - lastQuery;
            if (time < QueryInterval)
            {
                Echo($"Waiting for next query: {QueryInterval - time:hh\\:mm\\:ss}");
                return;
            }
            lastQuery = DateTime.Now;

            var infoLCDs = GetBlocksOfType<IMyTextPanel>(WildcardLCDs);

            WriteInfoLCDs(infoLCDs, "Inventory Monitor", false);

            string channel = ReadConfig(Me.CustomData, "Channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                WriteInfoLCDs(infoLCDs, "Channel not set.");
                return;
            }

            string cargoContainerName = ReadConfig(Me.CustomData, "CargoContainerName");
            if (string.IsNullOrWhiteSpace(cargoContainerName))
            {
                WriteInfoLCDs(infoLCDs, "CargoContainerName not set.");
                return;
            }

            string inventory = ReadConfig(Me.CustomData, "Inventory");
            if (string.IsNullOrWhiteSpace(inventory))
            {
                WriteInfoLCDs(infoLCDs, "Inventory not set.");
                return;
            }

            var cargoContainers = GetBlocksOfType<IMyCargoContainer>(cargoContainerName);
            if (cargoContainers.Count == 0)
            {
                WriteInfoLCDs(infoLCDs, "Cargo Containers Not Found.");
                return;
            }

            var required = ReadItemsFromCustomData(inventory);

            var current = GetCurrentItemsInStores(cargoContainers);

            string message = WriteMessage(required, current);
            if (message.Length > 0)
            {
                IGC.SendBroadcastMessage(channel, message.ToString());
                WriteInfoLCDs(infoLCDs, $"Sending from {channel}");
                WriteInfoLCDs(infoLCDs, message);
            }
        }

        List<T> GetBlocksOfType<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks;
        }
        void WriteInfoLCDs(List<IMyTextPanel> lcds, string text, bool append = true)
        {
            Echo(text);

            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text + Environment.NewLine, append);
            }
        }

        static string ReadConfig(string customData, string name)
        {
            string[] config = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return config.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }
        static Dictionary<string, int> ReadItemsFromCustomData(string customData)
        {
            Dictionary<string, int> required = new Dictionary<string, int>();

            string[] lines = customData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split(':');
                if (parts.Length == 2)
                {
                    string item = parts[0].Trim();
                    int amount = int.Parse(parts[1].Trim());
                    required[item] = amount;
                }
            }

            return required;
        }
        static Dictionary<string, MyFixedPoint> GetCurrentItemsInStores(List<IMyCargoContainer> cargos)
        {
            var list = new Dictionary<string, MyFixedPoint>();

            foreach (var cargo in cargos)
            {
                var inv = cargo.GetInventory();

                for (int i = 0; i < inv.ItemCount; i++)
                {
                    var item = inv.GetItemAt(i).Value;
                    string t = item.Type.SubtypeId;
                    if (!list.ContainsKey(t))
                    {
                        list[t] = 0;
                    }
                    list[t] += item.Amount;
                }
            }

            return list;
        }
        static string WriteMessage(Dictionary<string, int> required, Dictionary<string, MyFixedPoint> inventory)
        {
            StringBuilder message = new StringBuilder();

            foreach (var req in required)
            {
                int c = req.Value - (inventory.ContainsKey(req.Key) ? (int)inventory[req.Key] : 0);
                if (c > 0)
                {
                    message.Append($"{req.Key}={c};");
                }
            }

            return message.ToString();
        }
    }
}
