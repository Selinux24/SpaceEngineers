using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "1.3";
        const UpdateType CommandUpdate = UpdateType.Trigger | UpdateType.Terminal;

        readonly MyCommandLine commandLine = new MyCommandLine();
        readonly Dictionary<long, DisplayLcd> displayLcds = new Dictionary<long, DisplayLcd>();
        readonly BlockSystem<Block> blocks = new BlockSystem<Block>();
        readonly BlockSystem<IMyTextPanel> lcds = new BlockSystem<IMyTextPanel>();
        readonly BlockSystem<IMyCockpit> cockpits = new BlockSystem<IMyCockpit>();
        readonly IMyTextSurface pbScreen;

        internal Config Config { get; }

        public Program()
        {
            Config = new Config(this);
            Config.Load();

            pbScreen = Me.GetSurface(0);
            pbScreen.ContentType = ContentType.TEXT_AND_IMAGE;

            Search();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            Config.Save();
        }

        public void Main(string argument, UpdateType updateType)
        {
            if ((updateType & CommandUpdate) != 0) RunCommand(argument);

            if ((updateType & UpdateType.Update100) != 0) RunContinuousLogic();
        }

        void RunCommand(string argument)
        {
            Config.Load();

            if (argument == null) return;

            commandLine.TryParse(argument);
            var command = commandLine.Argument(0);
            if (command != null) command = command.Trim().ToLower();
            switch (command)
            {
                case "default":
                    Config.Reset();
                    break;
                default:
                    Search();
                    break;
            }
        }

        void RunContinuousLogic()
        {
            Display();
            RunLcd();
        }
        void Display()
        {
            pbScreen.WriteText($"*** Inventory {Version} ***\n", false);
            pbScreen.WriteText($"---------------------------\n", true);
            pbScreen.WriteText($"Managed LCDs:{blocks.List.Count}\n", true);
        }
        void RunLcd()
        {
            blocks.List.ForEach(block =>
            {
                if (string.IsNullOrWhiteSpace(block.MyBlock.CustomData)) return;

                block.Update();

                if (!block.Ini.ContainsSection("Inventory") && !block.MyBlock.CustomData.Trim().Equals("prepare")) return;

                var displayLcd = GetDisplayLcd(block.MyBlock);

                if (block.Changed) displayLcd.Load(block.Ini);
                else displayLcd.Draw();
            });
        }
        DisplayLcd GetDisplayLcd(IMyTerminalBlock block)
        {
            if (displayLcds.ContainsKey(block.EntityId)) return displayLcds[block.EntityId];

            var displayLcd = new DisplayLcd(this, block);
            displayLcds.Add(block.EntityId, displayLcd);
            return displayLcd;
        }

        void Search()
        {
            Echo($"Version {Version}");

            blocks.List.Clear();

            BlockSystem<IMyTextPanel>.SearchByFilter(this, lcds, Config.TextPanelFilter);
            if (!lcds.IsEmpty)
            {
                blocks.List.AddRange(lcds.List.Select(bl => new Block(bl)));
            }

            BlockSystem<IMyCockpit>.SearchByFilter(this, cockpits, Config.CockpitFilter);
            if (!cockpits.IsEmpty)
            {
                blocks.List.AddRange(cockpits.List.Select(bl => new Block(bl)));
            }
        }
    }
}
