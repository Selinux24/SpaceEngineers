using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "1.31";
        const UpdateType CommandUpdate = UpdateType.Trigger | UpdateType.Terminal;

        readonly MyCommandLine commandLine = new MyCommandLine();
        readonly Dictionary<long, DisplayLcd> displayLcds = new Dictionary<long, DisplayLcd>();
        readonly BlockSystem<Block> blocks = new BlockSystem<Block>();
        readonly BlockSystem<IMyTextPanel> lcds = new BlockSystem<IMyTextPanel>();
        readonly BlockSystem<IMyCockpit> cockpits = new BlockSystem<IMyCockpit>();
        readonly IMyTextSurface pbScreen;

        int maxBlocks = 0;
        int currBlock = 0;

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

            if (blocks.List.Count == 0) return;

            int i;
            for (i = 0; i < maxBlocks; i++)
            {
                if (!RunLcd()) break;
            }

            Echo($"Blocks Processed: {i}");
        }
        void Display()
        {
            Echo($"Version {Version}");
            Echo($"Managed LCDs: {blocks.List.Count}");

            pbScreen.WriteText($"*** Inventory {Version} ***\n", false);
            pbScreen.WriteText($"---------------------------\n", true);
            pbScreen.WriteText($"Managed LCDs:{blocks.List.Count}\n", true);
        }
        bool RunLcd()
        {
            var block = blocks.List[currBlock];
            currBlock++;
            currBlock %= maxBlocks;

            if (string.IsNullOrWhiteSpace(block.MyBlock.CustomData)) return true;

            block.Update();

            if (!block.Ini.ContainsSection("Inventory") && !block.MyBlock.CustomData.Trim().Equals("prepare")) return true;

            var displayLcd = GetDisplayLcd(block.MyBlock);

            if (block.Changed) displayLcd.Load(block.Ini);
            else displayLcd.Draw();

            return Runtime.CurrentInstructionCount < 25000;
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

            maxBlocks = blocks.List.Count;
        }
    }
}
