using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "1.2";
        const UpdateType CommandUpdate = UpdateType.Trigger | UpdateType.Terminal;

        readonly MyCommandLine commandLine = new MyCommandLine();
        readonly Dictionary<long, DisplayLcd> displayLcds = new Dictionary<long, DisplayLcd>();
        readonly BlockSystem<Block> blocks = new BlockSystem<Block>();

        internal KProperty MyProperty { get; private set; }
        internal IMyTextSurface DrawingSurface { get; }

        public Program()
        {
            MyProperty = new KProperty(this);
            MyProperty.Load();

            DrawingSurface = Me.GetSurface(0);
            DrawingSurface.ContentType = ContentType.TEXT_AND_IMAGE;

            Search();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            MyProperty.Save();
        }

        public void Main(string argument, UpdateType updateType)
        {
            if ((updateType & CommandUpdate) != 0) RunCommand(argument);

            if ((updateType & UpdateType.Update100) != 0) RunContinuousLogic();
        }

        void RunCommand(string argument)
        {
            MyProperty.Load();

            if (argument == null) return;

            commandLine.TryParse(argument);
            var command = commandLine.Argument(0);
            if (command != null) command = command.Trim().ToLower();
            switch (command)
            {
                case "default":
                    MyProperty.Reset();
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
            DrawingSurface.WriteText($"*** LCDInventory {Version} ***\n", false);
            DrawingSurface.WriteText($"------------------------------\n", true);
            DrawingSurface.WriteText($"LCD list size:{blocks.List.Count}\n", true);
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
            DisplayLcd displayLcd;
            if (displayLcds.ContainsKey(block.EntityId))
            {
                displayLcd = displayLcds[block.EntityId];
            }
            else
            {
                displayLcd = new DisplayLcd(this, block);
                displayLcds.Add(block.EntityId, displayLcd);
            }
            return displayLcd;
        }

        void Search()
        {
            Echo($"Version {Version}");

            blocks.Clear();

            var lcds = BlockSystem<IMyTextPanel>.SearchByFilter(this, MyProperty.TextPanelFilter);
            if (!lcds.IsEmpty)
            {
                blocks.List.AddRange(lcds.List.Select(bl => new Block(bl)));
            }

            var cockpits = BlockSystem<IMyCockpit>.SearchByFilter(this, MyProperty.CockpitFilter);
            if (!cockpits.IsEmpty)
            {
                blocks.List.AddRange(cockpits.List.Select(bl => new Block(bl)));
            }
        }
    }
}
