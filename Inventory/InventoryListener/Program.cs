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
        const string Version = "1.2";

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
                return;
            }

            foreach (var listener in config.Listeners)
            {
                //Get the output container
                var outputCargo = GetBlockWithNames<IMyCargoContainer>(listener, config.OutputCargo);
                if (outputCargo == null)
                {
                    Echo($"No output cargo found with name {listener} {config.OutputCargo}");
                    return;
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

                listeners.Add(listener, new Listener(listener, outputCargo, timerOpen, timerClose));
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
            infoText.Clear();

            if ((updateSource & UpdateType.IGC) != 0)
            {
                while (bl.HasPendingMessage)
                {
                    var msg = bl.AcceptMessage();

                    if (msg.Tag != config.Channel) continue;

                    string name;
                    string items;
                    if (!ParseMessage(msg.Data.ToString(), out name, out items)) continue;

                    if (!listeners.ContainsKey(name)) continue;
                    listeners[name].Prepare(msg.Source, items);
                    IGC.SendUnicastMessage(msg.Source, config.Channel, "1");
                }
            }

            if ((updateSource & UpdateType.Update100) != 0)
            {
                infoText.AppendLine($"Inventory Listener v{Version} - {config.Channel}. {DateTime.Now:HH:mm:ss}");
                foreach (var listener in listeners.Values)
                {
                    if (listener.Preparing(warehouseCargos))
                    {
                        IGC.SendUnicastMessage(listener.SenderId, config.Channel, "2");
                    }
                    infoText.Append(listener.GetState());
                }
            }

            WriteInfo();
        }
        bool ParseMessage(string data, out string name, out string items)
        {
            string[] dataBits = data.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            name = dataBits.Length > 0 ? dataBits[0] : "";
            items = dataBits.Length > 1 ? dataBits[1] : "";

            return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(items);
        }

        T GetBlockWithNames<T>(string name1, string name2) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name1) && b.CustomName.Contains(name2));
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
