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
        const string Version = "1.8";
        const char AttributeSep = '=';
        const string WildcardLCDs = "[INV]";
        const int ItemsQueryTicks = 3;

        readonly List<IMyCargoContainer> cargoContainers;
        readonly List<IMyTextPanel> infoLCDs;

        readonly string channel;
        readonly string name;
        readonly Dictionary<string, int> required;
        readonly CompareTypes compareType = CompareTypes.GreaterThan;
        readonly TimeSpan queryInterval;
        readonly double threshold;
        readonly bool verbose;

        readonly StringBuilder lastMessage = new StringBuilder();
        readonly StringBuilder infoText = new StringBuilder();
        readonly StringBuilder message = new StringBuilder();

        bool retained = false;
        TimeSpan lastQuery = TimeSpan.Zero;
        bool lastQueryHastItems = false;
        TimeSpan lastMessageDate = TimeSpan.Zero;
        int currentTick = 0;
        bool itemsNeeded = false;

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
                    "CompareType=int [1,-1]\n" +
                    "Inventory=item1:quantity1;itemN:quantityN;\n" +
                    "Verbose=true\n" +
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

            var vbs = ReadConfigBoolean(Me.CustomData, "Verbose");
            verbose = vbs ?? false;

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
        static bool? ReadConfigBoolean(string customData, string name)
        {
            string value = ReadConfig(customData, name);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return bool.Parse(value);
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
                        lastQuery = queryInterval;
                    }
                }
            }

            if ((updateSource & UpdateType.Update100) != 0)
            {
                infoText.Clear();
                infoText.AppendLine($"Inventory Monitor v{Version} - {channel}. {DateTime.Now:HH:mm:ss}");

                Monitorize();
                PrintCurrentMessage();
                PrintLastMessage();

                WriteInfo();
            }
        }
        void PrintCurrentMessage()
        {
            if (itemsNeeded)
                infoText.AppendLine(message.ToString().Replace(";", Environment.NewLine));
            else
                infoText.AppendLine("All items in required quantities.");
        }
        void PrintLastMessage()
        {
            if (lastMessageDate.Ticks > 0) infoText.AppendLine($"Last message sent {DateTime.Now - lastMessageDate:hh\\:mm} minutes ago.");
            infoText.AppendLine($"Last query has items? {lastQueryHastItems}");
            infoText.Append(lastMessage.ToString().Replace(";", Environment.NewLine));
        }

        void Monitorize()
        {
            if (retained)
            {
                infoText.AppendLine("Waiting for delivery to complete.");
                return;
            }

            lastQuery -= Runtime.TimeSinceLastRun;
            if (lastQuery <= TimeSpan.Zero)
            {
                Send();
                return;
            }

            infoText.AppendLine($"Waiting for next query: {lastQuery:hh\\:mm\\:ss}");

            if (currentTick++ > ItemsQueryTicks)
            {
                currentTick = 0;
                itemsNeeded = WriteMessage();
            }
        }
        void Send()
        {
            lastQuery = queryInterval;

            lastQueryHastItems = WriteMessage();

            if (!lastQueryHastItems) return;

            lastMessage.AppendLine(message.ToString());

            IGC.SendBroadcastMessage(channel, message.ToString());
        }
        bool WriteMessage()
        {
            message.Clear();
            bool anyNeeded = false;

            float currentCapacity;
            var current = GetCurrentItemsInStores(out currentCapacity);

            message.AppendLine(name);

            lastMessageDate = queryInterval;
            lastMessage.Clear();
            lastMessage.AppendLine($"Required {compareType}:");

            foreach (var req in required)
            {
                string reqItem = req.Key;

                if (reqItem.ToUpper() == "ANY")
                {
                    double curr = currentCapacity;
                    double reqValue = req.Value / 100.0;

                    string compareSymbol;
                    bool conditionMet;
                    switch (compareType)
                    {
                        case CompareTypes.LessThan:
                            compareSymbol = "<";
                            conditionMet = curr < reqValue;
                            break;
                        case CompareTypes.GreaterThan:
                            compareSymbol = ">";
                            conditionMet = curr > reqValue;
                            break;
                        default:
                            compareSymbol = "";
                            conditionMet = false;
                            break;
                    }

                    if (conditionMet)
                    {
                        message.Append($"{reqItem}={reqValue - curr};");
                        anyNeeded = true;
                        lastMessage.AppendLine($"{reqItem} {reqValue:P1} {compareSymbol} {curr:P1}");
                    }
                    else
                    {
                        lastMessage.AppendLine($"{reqItem} {reqValue:P1} {compareSymbol} {curr:P1} OK");
                    }
                }
                else
                {
                    //For Less/LessOrEqual: margin below required (safety buffer for replenishment)
                    //For Greater/GreaterOrEqual: margin above required (safety buffer for excess)
                    int curr = current.ContainsKey(reqItem) ? (int)current[reqItem] : 0;
                    int reqValue = req.Value;

                    string compareSymbol;
                    double reqThr;
                    bool conditionMet;
                    double qty;
                    string w;
                    switch (compareType)
                    {
                        case CompareTypes.LessThan:
                            compareSymbol = ">";
                            reqThr = curr - (reqValue + (int)(reqValue * threshold));
                            conditionMet = reqThr > 0;
                            qty = curr - reqValue;
                            w = "excess";
                            break;
                        case CompareTypes.GreaterThan:
                            compareSymbol = "<";
                            reqThr = curr - (reqValue - (int)(reqValue * threshold));
                            conditionMet = reqThr < 0;
                            qty = reqValue - curr;
                            w = "shortage";
                            break;
                        default:
                            compareSymbol = "";
                            reqThr = 0;
                            conditionMet = false;
                            qty = 0;
                            w = "";
                            break;
                    }

                    if (conditionMet)
                    {
                        message.Append($"{reqItem}={qty};");
                        anyNeeded = true;
                        lastMessage.AppendLine($"{reqItem} {reqValue}{compareSymbol}{curr} {w}");
                    }
                    else if (verbose)
                    {
                        lastMessage.AppendLine($"{reqItem} {reqValue}{compareSymbol}{curr} OK");
                    }
                }
            }

            return anyNeeded;
        }
        Dictionary<string, MyFixedPoint> GetCurrentItemsInStores(out float capacity)
        {
            var list = new Dictionary<string, MyFixedPoint>();
            capacity = 0;

            if (cargoContainers.Count == 0)
            {
                return list;
            }

            foreach (var cargo in cargoContainers)
            {
                var inv = cargo.GetInventory();
                capacity += inv.VolumeFillFactor;

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

            capacity /= cargoContainers.Count;

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
            lastQuery = new TimeSpan(ReadLong(storageLines, "lastQuery", 0));
            lastQueryHastItems = ReadInt(storageLines, "lastQueryHastItems", 0) == 1;
            lastMessageDate = new TimeSpan(ReadLong(storageLines, "lastMessageDate", 0));
            currentTick = ReadInt(storageLines, "currentTick", 0);
            itemsNeeded = ReadInt(storageLines, "itemsNeeded", 0) == 1;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"retained={(retained ? 1 : 0)}",
                $"lastQuery={lastQuery.Ticks}",
                $"lastQueryHastItems={(lastQueryHastItems ? 1 : 0)}",
                $"lastMessageDate={lastMessageDate.Ticks}",
                $"currentTick={currentTick}",
                $"itemsNeeded={(itemsNeeded?1:0)}"
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
