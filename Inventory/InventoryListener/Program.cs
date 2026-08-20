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
        const string Version = "1.10";
        const string Separate = "------";

        readonly List<IMyCargoContainer> warehouseCargos;
        readonly List<IMyTextPanel> infoLCDs;

        readonly IMyBroadcastListener bl;
        readonly Config config;
        readonly Dictionary<string, Listener> listeners = new Dictionary<string, Listener>();

        readonly StringBuilder infoText = new StringBuilder();

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
            if (!string.IsNullOrEmpty(argument))
            {
                string name;
                string items;
                if (!ParseMessage(argument, "|".ToCharArray(), out name, out items)) return;

                ProcessListenerMessage(name, items, -1);
            }

            if ((updateSource & UpdateType.IGC) != 0)
            {
                while (bl.HasPendingMessage)
                {
                    var msg = bl.AcceptMessage();

                    if (msg.Tag != config.Channel) continue;

                    string name;
                    string items;
                    if (!ParseMessage(msg.Data.ToString(), Environment.NewLine.ToCharArray(), out name, out items)) continue;

                    ProcessListenerMessage(name, items, msg.Source);
                }
            }

            if ((updateSource & UpdateType.Update100) != 0)
            {
                infoText.Clear();

                infoText.AppendLine($"Inventory Listener v{Version} - {config.Channel}. {DateTime.Now:HH:mm:ss}");
                foreach (var listener in listeners.Values)
                {
                    if (listener.Preparing(warehouseCargos))
                    {
                        IGC.SendUnicastMessage(listener.SenderId, config.Channel, "2");

                        var msgList = listener.FreeConnectors();
                        foreach (var msg in msgList)
                        {
                            BroadcastMessage(msg);
                        }
                    }
                    infoText.Append(listener.GetState(Runtime.TimeSinceLastRun));
                    infoText.AppendLine(Separate);
                }

                WriteInfo();
            }
        }
        bool ParseMessage(string msg, char[] separator, out string name, out string items)
        {
            string[] dataBits = msg.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            name = dataBits.Length > 0 ? dataBits[0] : "";
            items = dataBits.Length > 1 ? dataBits[1] : "";

            return !string.IsNullOrWhiteSpace(name);
        }
        void ProcessListenerMessage(string name, string items, long source)
        {
            if (!listeners.ContainsKey(name)) return;
            listeners[name].Prepare(source, items);

            if (source < 0) return;
            IGC.SendUnicastMessage(source, config.Channel, "1");
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

            foreach (var listener in listeners)
            {
                string listenerData = Utils.ReadString(storageLines, listener.Key, null);
                listener.Value.LoadFromStorage(listenerData);
            }
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>();

            foreach (var listener in listeners)
            {
                parts.Add($"{listener.Key}{Utils.AttributeSep}{listener.Value.SaveToStorage()}");
            }

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
