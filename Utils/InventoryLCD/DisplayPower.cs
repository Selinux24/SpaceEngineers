using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    class DisplayPower
    {
        readonly Program program;
        readonly DisplayLcd displayLcd;
        readonly MyDefinitionId powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        readonly Dictionary<string, Power> outputs = new Dictionary<string, Power>();
        readonly BlockSystem<IMyTerminalBlock> producers = new BlockSystem<IMyTerminalBlock>();
        readonly BlockSystem<IMyTerminalBlock> consummers = new BlockSystem<IMyTerminalBlock>();
        Power batteriesStore;
        float currentInput = 0f;
        float maxInput = 0f;

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        string producersFilter = "*";
        string consummersFilter = "*";
        bool showDetails = true;
        bool showBatteries = true;
        bool showInput = true;
        bool showPercent = true;

        public DisplayPower(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni ini)
        {
            panel = ini.Get("Power", "panel").ToInt32(0);
            enable = ini.Get("Power", "on").ToBoolean(false);
            scale = ini.Get("Power", "scale").ToSingle(1f);
            producersFilter = ini.Get("Power", "producers_filter").ToString("*");
            consummersFilter = ini.Get("Power", "consummers_filter").ToString("*");
            showDetails = ini.Get("Power", "show_details").ToBoolean(true);
            showBatteries = ini.Get("Power", "show_batteries").ToBoolean(true);
            showInput = ini.Get("Power", "show_input").ToBoolean(true);
            showPercent = ini.Get("Power", "show_percent").ToBoolean(true);

            Search();
        }
        public void Save(MyIni ini)
        {
            ini.Set("Power", "panel", panel);
            ini.Set("Power", "on", enable);
            ini.Set("Power", "scale", scale);
            ini.Set("Power", "producers_filter", producersFilter);
            ini.Set("Power", "consummers_filter", consummersFilter);
            ini.Set("Power", "show_details", showDetails);
            ini.Set("Power", "show_batteries", showBatteries);
            ini.Set("Power", "show_input", showInput);
            ini.Set("Power", "show_percent", showPercent);
        }

        void Search()
        {
            var blockProdFilter = BlockFilter<IMyTerminalBlock>.Create(displayLcd.Block, producersFilter);
            BlockSystem<IMyTerminalBlock>.SearchByFilter(program, producers, blockProdFilter, block => block.Components.Has<MyResourceSourceComponent>() && (block is IMyBatteryBlock || block is IMyPowerProducer));

            var blockConsFilter = BlockFilter<IMyTerminalBlock>.Create(displayLcd.Block, consummersFilter);
            BlockSystem<IMyTerminalBlock>.SearchByFilter(program, consummers, blockConsFilter, block => block.Components.Has<MyResourceSinkComponent>() && (!(block is IMyBatteryBlock)));
        }

        public void Draw(Drawing drawing)
        {
            if (!enable) return;

            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();

            Draw(surface);
        }
        void Draw(SurfaceDrawing surface)
        {
            UpdateOutputs();

            float height = 40f * scale;
            var padding = new Vector2(0, 6) * scale;

            var style = new StyleGauge()
            {
                Orientation = SpriteOrientation.Horizontal,
                Fullscreen = true,
                Width = height,
                Height = height,
                Padding = new StylePadding(0),
                Round = false,
                RotationOrScale = scale,
                Thresholds = program.Config.PowerThresholds,
                Percent = showPercent,
            };

            surface.DrawGauge(surface.Position, outputs["all"].Current, outputs["all"].Max, style);
            surface.Position += new Vector2(0, height) + padding;

            var deltaPosition = new Vector2(0, 45) * scale;

            foreach (var v in outputs)
            {
                string title = v.Key;
                var value = v.Value;

                string data;
                if (title.Equals("all"))
                {
                    data = $"Global Generation\n Out: {Math.Round(value.Current, 2)}MW / {Math.Round(value.Max, 2)}MW";
                }
                else if (showDetails)
                {
                    data = $"{title} (n={value.Count})\n";
                    data += $" Out: {Math.Round(value.Current, 2)}MW / {Math.Round(value.Max, 2)}MW";
                    data += $" (Moy={value.Moyen}MW)";
                }
                else
                {
                    continue;
                }

                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Color = Color.DimGray,
                    Position = surface.Position,
                    Data = data,
                    RotationOrScale = 0.75f * scale,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });

                surface.Position += deltaPosition;
            }
            surface.Position += padding;

            if (showBatteries)
            {
                surface.DrawGauge(surface.Position, batteriesStore.Current, batteriesStore.Max, style);
                surface.Position += deltaPosition;
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Color = Color.DimGray,
                    Position = surface.Position,
                    Data = $"Battery Store (n={batteriesStore.Count})\n Store: {Math.Round(batteriesStore.Current, 2)}MW / {Math.Round(batteriesStore.Max, 2)}MW",
                    RotationOrScale = 0.75f * scale,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });
                surface.Position += deltaPosition;
            }

            if (showInput)
            {
                surface.DrawGauge(surface.Position, currentInput, maxInput, style);
                surface.Position += deltaPosition;
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Color = Color.DimGray,
                    Position = surface.Position,
                    Data = $"Power In: {Math.Round(currentInput, 2)}MW / {Math.Round(maxInput, 2)}MW",
                    RotationOrScale = 0.75f * scale,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });
                surface.Position += deltaPosition;
            }
        }
        void UpdateOutputs()
        {
            outputs.Clear();
            outputs.Add("all", new Power() { Type = "All" });

            batteriesStore = new Power() { Type = "Batteries" };
            producers.ForEach(block =>
            {
                if (block is IMyBatteryBlock)
                {
                    var battery = (IMyBatteryBlock)block;

                    batteriesStore.AddCurrent(battery.CurrentStoredPower);
                    batteriesStore.AddMax(battery.MaxStoredPower);
                }
                else if (block is IMyPowerProducer)
                {
                    var producer = (IMyPowerProducer)block;

                    var global = outputs["all"];
                    global.AddCurrent(producer.CurrentOutput);
                    global.AddMax(producer.MaxOutput);

                    string type = block.BlockDefinition.SubtypeName;
                    if (!outputs.ContainsKey(type)) outputs.Add(type, new Power() { Type = type });
                    var current = outputs[type];
                    current.AddCurrent(producer.CurrentOutput);
                    current.AddMax(producer.MaxOutput);
                }
            });

            currentInput = 0f;
            maxInput = 0f;
            consummers.ForEach(block =>
            {
                if (block is IMyBatteryBlock) return;

                MyResourceSinkComponent resourceSink;
                if (!block.Components.TryGet(out resourceSink)) return;

                var myDefinitionIds = resourceSink.AcceptedResources;
                if (myDefinitionIds.Contains(powerDefinitionId))
                {
                    maxInput += resourceSink.RequiredInputByType(powerDefinitionId);
                    currentInput += resourceSink.CurrentInputByType(powerDefinitionId);
                }
            });
        }
    }
}
