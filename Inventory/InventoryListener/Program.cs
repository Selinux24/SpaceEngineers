using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "1.11";
        const string Separate = "------";

        readonly List<IMyCargoContainer> warehouseCargos;
        readonly List<IMyTextPanel> infoLCDs;

        readonly IMyBroadcastListener bl;
        readonly Config config;
        readonly Dictionary<string, Listener> listeners = new Dictionary<string, Listener>();
        readonly Dictionary<string, Order> orders = new Dictionary<string, Order>();

        readonly StringBuilder infoText = new StringBuilder();

        readonly TimeSpan orderQuery = TimeSpan.FromSeconds(10);
        TimeSpan lastOrderUpdate = TimeSpan.Zero;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData = Config.GetDefault();

                Echo("CustomData not set.");
                return;
            }

            config = new Config(Me.CustomData);
            if (!config.IsValid())
            {
                Echo(config.GetErrors());
                return;
            }

            //Get all input containers
            warehouseCargos = GetBlocksOfType<IMyCargoContainer>(config.InventoryCargo);
            if (warehouseCargos.Count == 0)
            {
                Echo($"No warehouse cargo containers found with name {config.InventoryCargo}");
            }

            foreach (var listener in config.Listeners)
            {
                //Get the output container
                var outputCargos = GetBlocksWithNames<IMyCargoContainer>(listener, config.OutputCargo);
                if (outputCargos.Count == 0)
                {
                    Echo($"No output cargos found with name {listener} {config.OutputCargo}");
                }

                var timerOpen = GetBlockWithNames<IMyTimerBlock>(listener, config.TimerOpen);
                if (timerOpen == null)
                {
                    Echo($"No timer found with name {listener} {config.TimerOpen}");
                }

                var timerClose = GetBlockWithNames<IMyTimerBlock>(listener, config.TimerClose);
                if (timerClose == null)
                {
                    Echo($"No timer found with name {listener} {config.TimerClose}");
                }

                var connectors = GetBlocksWithNames<IMyShipConnector>(listener, config.Connector);
                if (connectors.Count == 0)
                {
                    connectors = GetBlocksWithName<IMyShipConnector>(config.Connector);
                    if (connectors.Count == 0)
                    {
                        Echo($"No connectors found with name {listener} {config.Connector}");
                        continue;
                    }
                }

                //Find the route for this listener
                var route = config.Routes.Find(r => r.Name == listener);
                if (route == null || !route.IsValid())
                {
                    Echo($"No valid route found for {listener}");
                    continue;
                }

                listeners.Add(listener, new Listener(listener, route, connectors, outputCargos, timerOpen, timerClose));
            }

            if (listeners.Count == 0)
            {
                Echo("No valid listeners found.");
                return;
            }

            infoLCDs = GetBlocksOfType<IMyTextPanel>(config.WildcardLCDs);

            bl = IGC.RegisterBroadcastListener(config.Channel);
            bl.SetMessageCallback(config.Channel);
            Echo($"Listener registered on {config.Channel}");

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ProcessCommand(argument);

            if ((updateSource & UpdateType.IGC) != 0)
            {
                while (bl.HasPendingMessage)
                {
                    var msg = bl.AcceptMessage();

                    if (msg.Tag != config.Channel) continue;

                    ReadOrder(msg.Data.ToString(), Environment.NewLine, msg.Source);
                }
            }

            if ((updateSource & UpdateType.Update100) != 0)
            {
                lastOrderUpdate -= Runtime.TimeSinceLastRun;
                if (lastOrderUpdate > TimeSpan.Zero) return;
                lastOrderUpdate = orderQuery;

                UpdateListeners();

                WriteInfo();
            }
        }

        void ProcessCommand(string argument)
        {
            if (string.IsNullOrEmpty(argument)) return;

            if (argument == "RESET") Reset();

            ReadOrder(argument, "|", -1);
        }

        void Reset()
        {
            foreach (var listener in listeners.Values)
            {
                listener.Reset();
            }

            orders.Clear();
        }

        void ReadOrder(string msg, string separator, long source)
        {
            string name;
            string items;
            if (!ParseMessage(msg, separator.ToCharArray(), out name, out items)) return;

            QueueListenerMessage(name, items, source);
        }
        bool ParseMessage(string msg, char[] separator, out string name, out string items)
        {
            string[] dataBits = msg.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            name = dataBits.Length > 0 ? dataBits[0] : "";
            items = dataBits.Length > 1 ? dataBits[1] : "";

            return !string.IsNullOrWhiteSpace(name);
        }
        void QueueListenerMessage(string name, string items, long source)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            if (!listeners.ContainsKey(name)) return;

            if (orders.ContainsKey(name))
            {
                orders[name].Items = items;
                orders[name].SourceId = source;
            }
            else
            {
                orders.Add(name, new Order { Items = items, SourceId = source });
            }
        }

        void UpdateListeners()
        {
            infoText.Clear();

            infoText.AppendLine($"Inventory Listener v{Version} - {config.Channel}. {DateTime.Now:HH:mm:ss}");

            infoText.AppendLine(Separate);
            foreach (var lst in listeners)
            {
                bool hasOrder = orders.ContainsKey(lst.Key);

                infoText.AppendLine($"{lst.Key} {(hasOrder ? "order pending." : "")}");
            }
            infoText.AppendLine(Separate);

            bool working = ProcessListeners();
            if (working)
            {
                infoText.AppendLine("Processing order...");
            }
            else
            {
                ProcessOrders();
            }
        }
        bool ProcessListeners()
        {
            bool working = false;

            foreach (var listener in listeners.Values)
            {
                if (listener.Prepared()) continue;

                bool prepared = listener.Preparing(warehouseCargos);
                if (prepared)
                {
                    IGC.SendUnicastMessage(listener.SenderId, config.Channel, "2");

                    var msgList = listener.FreeConnectors();
                    foreach (var msg in msgList)
                    {
                        BroadcastMessage(msg);
                    }
                }
                else
                {
                    working = true;
                }

                break;
            }

            foreach (var listener in listeners.Values)
            {
                infoText.Append(listener.GetState(Runtime.TimeSinceLastRun));
                infoText.AppendLine(Separate);
            }

            return working;
        }
        void ProcessOrders()
        {
            if (orders.Count == 0) return;

            infoText.AppendLine("Dequeuing order...");

            string erase = null;
            foreach (var order in orders)
            {
                string name = order.Key;

                if (!listeners.ContainsKey(name))
                {
                    erase = name;
                    break;
                }

                string items = order.Value.Items;
                long source = order.Value.SourceId;

                string reason;
                if (listeners[name].Start(source, items, warehouseCargos, out reason))
                {
                    erase = name;

                    if (source < 0) break;
                    IGC.SendUnicastMessage(source, config.Channel, "1");

                    break;
                }
                else
                {
                    infoText.AppendLine($"Order {name} not ready for processing. {reason}");
                }
            }

            if (erase != null)
            {
                orders.Remove(erase);
            }
        }

        T GetBlockWithNames<T>(string name1, string name2) where T : class, IMyTerminalBlock
        {
            return GetBlocksWithNames<T>(name1, name2).FirstOrDefault();
        }
        List<T> GetBlocksWithNames<T>(string name1, string name2) where T : class, IMyTerminalBlock
        {
            if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2)) return new List<T>();

            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name1) && b.CustomName.Contains(name2));
            return blocks;
        }
        List<T> GetBlocksWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            if (string.IsNullOrWhiteSpace(name)) return new List<T>();

            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks;
        }
        List<T> GetBlocksOfType<T>(string filter) where T : class, IMyTerminalBlock
        {
            if (string.IsNullOrWhiteSpace(filter)) return new List<T>();

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
        void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            IGC.SendBroadcastMessage(config.Channel, message);
        }

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            orders.Clear();
            foreach (var listener in listeners)
            {
                string listenerData = Utils.ReadString(storageLines, $"listener.{listener.Key}", null);
                listener.Value.LoadFromStorage(listenerData);

                string orderData = Utils.ReadString(storageLines, $"order.{listener.Key}", null);
                if (string.IsNullOrEmpty(orderData)) continue;
                Order order = new Order();
                order.LoadFromStorage(orderData);
                orders[listener.Key] = order;
            }
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>();

            foreach (var listener in listeners)
            {
                parts.Add($"listener.{listener.Key}{Utils.AttributeSep}{listener.Value.SaveToStorage()}");
            }

            foreach (var order in orders)
            {
                parts.Add($"order.{order.Key}{Utils.AttributeSep}{order.Value.SaveToStorage()}");
            }

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
