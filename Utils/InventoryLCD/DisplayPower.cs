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
        readonly MyDefinitionId powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        readonly Dictionary<string, Power> outputs = new Dictionary<string, Power>();
        BlockSystem<IMyTerminalBlock> producers;
        BlockSystem<IMyTerminalBlock> consummers;
        Power batteriesStore;
        float currentInput = 0f;
        float maxInput = 0f;

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        bool showDetails = true;
        bool showBatteries = true;
        bool showInput = true;

        public DisplayPower(Program program)
        {
            this.program = program;
        }

        public void Load(MyIni ini)
        {
            panel = ini.Get("Power", "panel").ToInt32(0);
            enable = ini.Get("Power", "on").ToBoolean(false);
            scale = ini.Get("Power", "scale").ToSingle(1f);
            showDetails = ini.Get("Power", "show_details").ToBoolean(true);
            showBatteries = ini.Get("Power", "show_batteries").ToBoolean(true);
            showInput = ini.Get("Power", "show_input").ToBoolean(true);

            producers = BlockSystem<IMyTerminalBlock>.SearchBlocks(program, block => block.Components.Has<MyResourceSourceComponent>());
            consummers = BlockSystem<IMyTerminalBlock>.SearchBlocks(program, block => block.Components.Has<MyResourceSinkComponent>());
        }
        public void Save(MyIni ini)
        {
            ini.Set("Power", "panel", panel);
            ini.Set("Power", "on", enable);
            ini.Set("Power", "scale", scale);
            ini.Set("Power", "show_details", showDetails);
            ini.Set("Power", "show_batteries", showBatteries);
            ini.Set("Power", "show_input", showInput);
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
                Thresholds = program.MyProperty.PowerThresholds
            };

            surface.DrawGauge(surface.Position, outputs["all"].Current, outputs["all"].Max, style);
            surface.Position += new Vector2(0, height) + padding;

            var deltaPosition = new Vector2(0, 45) * scale;

            foreach (var kvp in outputs)
            {
                string title = kvp.Key;

                string data;
                if (kvp.Key.Equals("all"))
                {
                    data = $"Global Generation\n Out: {Math.Round(kvp.Value.Current, 2)}MW / {Math.Round(kvp.Value.Max, 2)}MW";
                }
                else if (showDetails)
                {
                    data = $"{title} (n={kvp.Value.Count})\n";
                    data += $" Out: {Math.Round(kvp.Value.Current, 2)}MW / {Math.Round(kvp.Value.Max, 2)}MW";
                    data += $" (Moy={kvp.Value.Moyen}MW)";
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
                    FontId = surface.Font,
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
                    FontId = surface.Font,
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
                    FontId = surface.Font,
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
                string type = block.BlockDefinition.SubtypeName;
                if (block is IMyBatteryBlock)
                {
                    var battery = (IMyBatteryBlock)block;
                    batteriesStore.AddCurrent(battery.CurrentStoredPower);
                    batteriesStore.AddMax(battery.MaxStoredPower);
                }
                else if (block is IMyPowerProducer)
                {
                    var producer = (IMyPowerProducer)block;
                    var global_output = outputs["all"];
                    global_output.AddCurrent(producer.CurrentOutput);
                    global_output.AddMax(producer.MaxOutput);

                    if (!outputs.ContainsKey(type)) outputs.Add(type, new Power() { Type = type });
                    var current_output = outputs[type];
                    current_output.AddCurrent(producer.CurrentOutput);
                    current_output.AddMax(producer.MaxOutput);
                }
            });

            currentInput = 0f;
            maxInput = 0f;
            consummers.ForEach(block =>
            {
                if (!(block is IMyBatteryBlock))
                {
                    MyResourceSinkComponent resourceSink;
                    block.Components.TryGet(out resourceSink);
                    if (resourceSink != null)
                    {
                        var myDefinitionIds = resourceSink.AcceptedResources;
                        if (myDefinitionIds.Contains(powerDefinitionId))
                        {
                            maxInput += resourceSink.RequiredInputByType(powerDefinitionId);
                            currentInput += resourceSink.CurrentInputByType(powerDefinitionId);
                        }
                    }
                }
            });
        }
    }
}
