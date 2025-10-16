using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const UpdateType CommandUpdate = UpdateType.Trigger | UpdateType.Terminal;

        readonly string version = "1.1";
        readonly MyCommandLine commandLine = new MyCommandLine();
        readonly Dictionary<long, DisplayLcd> displayLcds = new Dictionary<long, DisplayLcd>();
        BlockSystem<IMyTerminalBlock> blocks = null;
        bool search = true;

        internal KProperty MyProperty { get; private set; }
        internal bool ForceUpdate { get; private set; } = false;
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
        void Search()
        {
            Echo($"Version {version}");
            blocks = new BlockSystem<IMyTerminalBlock>();
            var block_filter = BlockFilter<IMyTextPanel>.Create(Me, MyProperty.LCDFilter);
            var lcds = BlockSystem<IMyTextPanel>.SearchByFilter(this, block_filter);

            if (lcds.IsEmpty == false)
            {
                blocks.List.AddRange(lcds.List.Cast<IMyTerminalBlock>().ToList());
            }

            var cockpit_filter = BlockFilter<IMyCockpit>.Create(Me, MyProperty.LCDFilter);
            var cockpits = BlockSystem<IMyCockpit>.SearchByFilter(this, cockpit_filter);

            if (cockpits.IsEmpty == false)
            {
                blocks.List.AddRange(cockpits.List.Cast<IMyTerminalBlock>().ToList());
            }

            search = false;
        }

        public void Save()
        {
            MyProperty.Save();
        }

        public void Main(string argument, UpdateType updateType)
        {
            if ((updateType & CommandUpdate) != 0)
            {
                RunCommand(argument);
            }
            if ((updateType & UpdateType.Update100) != 0)
            {
                RunContinuousLogic();
            }
        }

        void RunCommand(string argument)
        {
            MyProperty.Load();
            if (argument == null)
            {
                return;
            }

            commandLine.TryParse(argument);
            var command = commandLine.Argument(0);
            if (command != null) command = command.Trim().ToLower();
            switch (command)
            {
                case "default":
                    Me.CustomData = "";
                    MyProperty.Load();
                    MyProperty.Save();
                    break;
                case "forceupdate":
                    ForceUpdate = true;
                    break;
                case "test":
                    var lcd = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(commandLine.Argument(1));
                    lcd.ScriptBackgroundColor = Color.Black;
                    var drawing = new Drawing(lcd);
                    var surfaceDrawing = drawing.GetSurfaceDrawing();
                    surfaceDrawing.Test(this);
                    surfaceDrawing.Dispose();
                    break;
                case "getname":
                    int index;
                    int.TryParse(commandLine.Argument(1), out index);
                    var names = new List<string>();
                    DrawingSurface.GetSprites(names);
                    Echo($"Sprite {index} name={names[index]}");
                    var lcdResult = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("Result Name");
                    lcdResult.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcdResult.WriteText($"Sprite {index}\n", false);
                    lcdResult.WriteText($"name={names[index]}", true);
                    break;
                case "gettype":
                    DiplayGetType(commandLine.Argument(1));
                    break;
                default:
                    search = true;
                    Search();
                    break;
            }
        }
        void DiplayGetType(string name)
        {
            var block = GridTerminalSystem.GetBlockWithName(name);
            var lcdResult2 = GridTerminalSystem.GetBlockWithName("Result Type") as IMyTextPanel;
            if (lcdResult2 != null)
            {
                lcdResult2.ContentType = ContentType.TEXT_AND_IMAGE;
                lcdResult2.WriteText($"Block {name}\n", false);
                lcdResult2.WriteText($"Type Name={block.GetType().Name}\n", true);
                lcdResult2.WriteText($"SubtypeName={block.BlockDefinition.SubtypeName}\n", true);
                lcdResult2.WriteText($"SubtypeId={block.BlockDefinition.SubtypeId}\n", true);
            }
            else
            {
                Echo($"Block {name}");
                Echo($"Type Name={block.GetType().Name}");
                Echo($"SubtypeName={block.BlockDefinition.SubtypeName}");
                Echo($"SubtypeId={block.BlockDefinition.SubtypeId}");
            }
        }

        void RunContinuousLogic()
        {
            if (search) Search();
            Display();
            RunLcd();
        }
        void Display()
        {
            DrawingSurface.WriteText($"*** LCDInventory {version} ***\n", false);
            DrawingSurface.WriteText($"------------------------------\n", true);
            DrawingSurface.WriteText($"LCD list size:{blocks.List.Count}\n", true);
        }
        void RunLcd()
        {
            blocks.List.ForEach(block =>
            {
                if (block.CustomData != null && !block.CustomData.Equals(""))
                {
                    MyIni MyIni = new MyIni();

                    MyIniParseResult result;
                    MyIni.TryParse(block.CustomData, out result);

                    if (MyIni.ContainsSection("Inventory") || block.CustomData.Trim().Equals("prepare"))
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
                        displayLcd.Load(MyIni);
                        displayLcd.Draw();
                    }
                }
            });
            ForceUpdate = false;
        }
    }
}
