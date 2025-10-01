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
        const string Version = "1.3";
        const char AttributeSep = '=';
        const string WildcardLCDs = "[INV]";

        readonly List<IMyCargoContainer> cargoContainers;
        readonly List<IMyTextPanel> infoLCDs;

        readonly string channel;
        readonly string name;
        readonly Dictionary<string, int> required;
        readonly TimeSpan queryInterval;
        readonly double threshold;
        bool retained = false;
        DateTime lastQuery = DateTime.MinValue;
        bool lastQueryHastItems = false;
        DateTime lastMessageDate = DateTime.MinValue;
        readonly StringBuilder lastMessage = new StringBuilder();
        readonly StringBuilder infoText = new StringBuilder();
        readonly StringBuilder message = new StringBuilder();

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "Channel=name\n" +
                    "Name=name\n" +
                    "CargoContainerName=name\n" +
                    "QueryInterval=int\n" +
                    "Threshold=int\n" +
                    "Inventory=item1:quantity1;itemN:quantityN;\n" +
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

            name = ReadConfig(Me.CustomData, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                Echo("Name not set.");
                return;
            }

            string inventory = ReadConfig(Me.CustomData, "Inventory");
            if (string.IsNullOrWhiteSpace(inventory))
            {
                Echo("Inventory not set.");
                return;
            }
            required = ReadConfigInventory(inventory);

            var interval = ReadConfigInt(Me.CustomData, "QueryInterval");
            if (!interval.HasValue || interval.Value < 1)
            {
                Echo("QueryInterval minutes not valid. Must be a positive integer.");
                return;
            }
            queryInterval = TimeSpan.FromMinutes(interval.Value);

            var thr = ReadConfigDouble(Me.CustomData, "Threshold");
            if (!thr.HasValue || thr.Value < 0 || thr.Value > 1)
            {
                Echo("Threshold not valid. Must be a positive double major than 0 and less or equal than 1.");
                return;
            }
            threshold = thr.Value;

            string cargoContainerName = ReadConfig(Me.CustomData, "CargoContainerName");
            if (string.IsNullOrWhiteSpace(cargoContainerName))
            {
                Echo("CargoContainerName not set.");
                return;
            }

            cargoContainers = GetBlocksOfType<IMyCargoContainer>(cargoContainerName);
            if (cargoContainers.Count == 0)
            {
                Echo("Cargo Containers Not Found.");
                return;
            }

            string wildcard = ReadConfig(Me.CustomData, "WildcardLCDs") ?? WildcardLCDs;
            infoLCDs = GetBlocksOfType<IMyTextPanel>(wildcard);

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Echo("Working...");
        }
        static string ReadConfig(string customData, string name)
        {
            string[] config = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return config.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "");
        }
        static int? ReadConfigInt(string customData, string name)
        {
            string value = ReadConfig(customData, name);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return int.Parse(value);
        }
        static double? ReadConfigDouble(string customData, string name)
        {
            string value = ReadConfig(customData, name);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return double.Parse(value);
        }
        static Dictionary<string, int> ReadConfigInventory(string inventory)
        {
            var required = new Dictionary<string, int>();

            var lines = inventory.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length == 2)
                {
                    string item = parts[0].Trim();
                    int amount = int.Parse(parts[1].Trim());
                    required[item] = amount;
                }
            }

            return required;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
            {
                if (argument == "FORCE")
                {
                    Send();
                    return;
                }
            }

            if ((updateSource & UpdateType.IGC) != 0)
            {
                while (IGC.UnicastListener.HasPendingMessage)
                {
                    var msg = IGC.UnicastListener.AcceptMessage();
                    string state = msg.Data.ToString();
                    if (state == "1")
                    {
                        //Retain next query
                        retained = true;
                    }
                    else if (state == "2")
                    {
                        //Free query
                        retained = false;
                        lastQuery = DateTime.Now;
                    }
                }
            }

            if ((updateSource & UpdateType.Update100) != 0)
            {
                infoText.Clear();
                infoText.AppendLine($"Inventory Monitor v{Version} - {channel}. {DateTime.Now:HH:mm:ss}");
                if (lastMessageDate.Ticks > 0) infoText.AppendLine($"Last message sent {(int)(DateTime.Now - lastMessageDate).TotalMinutes} minutes ago.");
                infoText.Append(lastMessage.ToString().Replace(";", Environment.NewLine));
                infoText.AppendLine($"Last query has items? {lastQueryHastItems}");

                Monitorize();

                WriteInfo();
            }
        }

        void Monitorize()
        {
            if (retained)
            {
                return;
            }

            var time = DateTime.Now - lastQuery;
            if (time < queryInterval)
            {
                infoText.AppendLine($"Waiting for next query: {queryInterval - time:hh\\:mm\\:ss}");
                return;
            }

            Send();
        }
        void Send()
        {
            lastQuery = DateTime.Now;

            lastQueryHastItems = WriteMessage();

            if (!lastQueryHastItems) return;

            lastMessageDate = DateTime.Now;
            lastMessage.Clear();
            lastMessage.AppendLine(message.ToString());

            IGC.SendBroadcastMessage(channel, message.ToString());
        }
        bool WriteMessage()
        {
            message.Clear();
            bool anyNeeded = false;

            var current = GetCurrentItemsInStores();

            message.AppendLine(name);

            foreach (var req in required)
            {
                //Add line only if needed quantity is major then the required quantity times the threshold
                var reqThr = (int)(req.Value * threshold);
                var curr = current.ContainsKey(req.Key) ? (int)current[req.Key] : 0;

                int c = reqThr - curr;
                if (c > 0)
                {
                    message.Append($"{req.Key}={req.Value - curr};");
                    anyNeeded = true;
                }
            }

            return anyNeeded;
        }
        Dictionary<string, MyFixedPoint> GetCurrentItemsInStores()
        {
            var list = new Dictionary<string, MyFixedPoint>();

            foreach (var cargo in cargoContainers)
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

        List<T> GetBlocksOfType<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
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

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            retained = ReadInt(storageLines, "retained", 0) == 1;
            lastQuery = new DateTime(ReadLong(storageLines, "lastQuery", 0));
            lastQueryHastItems = ReadInt(storageLines, "lastQueryHastItems", 0) == 1;
            lastMessageDate = new DateTime(ReadLong(storageLines, "lastMessageDate", 0));
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"retained={(retained ? 1 : 0)}",
                $"lastQuery={lastQuery.Ticks}",
                $"lastQueryHastItems={(lastQueryHastItems ? 1 : 0)}",
                $"lastMessageDate={lastMessageDate.Ticks}"
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
