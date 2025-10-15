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
        readonly MyDefinitionId powerDefinitionId;

        int panel = 0;
        bool enable = false;
        float scale = 1f;

        public DisplayPower(Program program)
        {
            this.program = program;
            powerDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        }

        public void Load(MyIni MyIni)
        {
            panel = MyIni.Get("Power", "panel").ToInt32(0);
            enable = MyIni.Get("Power", "on").ToBoolean(false);
            scale = MyIni.Get("Power", "scale").ToSingle(1f);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Power", "panel", panel);
            MyIni.Set("Power", "on", enable);
            MyIni.Set("Power", "scale", scale);
        }
        public void Draw(Drawing drawing)
        {
            if (!enable) return;
            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();
            Draw(surface);
        }
        public void Draw(SurfaceDrawing surface)
        {
            if (!enable) return;
            var producers = BlockSystem<IMyTerminalBlock>.SearchBlocks(program, block => block.Components.Has<MyResourceSourceComponent>());
            var consummers = BlockSystem<IMyTerminalBlock>.SearchBlocks(program, block => block.Components.Has<MyResourceSinkComponent>());
            var outputs = new Dictionary<string, Power>();
            var batteries_store = new Power() { Type = "Batteries" };
            outputs.Add("all", new Power() { Type = "All" });
            float current_input = 0f;
            float max_input = 0f;
            float height = 30f * scale;
            var deltaPosition = new Vector2(0, 45) * scale;
            var padding = new Vector2(0, 6) * scale;

            var style = new StyleGauge()
            {
                Orientation = SpriteOrientation.Horizontal,
                Fullscreen = true,
                Width = height,
                Height = height,
                Padding = new StylePadding(0),
                Round = false,
                RotationOrScale = 0.5f * scale,
                Thresholds = program.MyProperty.PowerThresholds
            };

            var text = new MySprite()
            {
                Type = SpriteType.TEXT,
                Color = Color.DimGray,
                Position = surface.Position,
                RotationOrScale = 0.75f * scale,
                FontId = EnumFont.BuildInfo,
                Alignment = TextAlignment.LEFT
            };

            producers.ForEach(delegate (IMyTerminalBlock block)
            {
                string type = block.BlockDefinition.SubtypeName;
                if (block is IMyBatteryBlock)
                {
                    var battery = (IMyBatteryBlock)block;
                    batteries_store.AddCurrent(battery.CurrentStoredPower);
                    batteries_store.AddMax(battery.MaxStoredPower);
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

            surface.DrawGauge(surface.Position, outputs["all"].Current, outputs["all"].Max, style);
            surface.Position += new Vector2(0, height) + padding;

            foreach (var kvp in outputs)
            {
                string title = kvp.Key;

                if (kvp.Key.Equals("all"))
                {
                    text.Data = $"Global Generator\n Out: {Math.Round(kvp.Value.Current, 2)}MW / {Math.Round(kvp.Value.Max, 2)}MW";
                }
                else
                {
                    text.Data = $"{title} (n={kvp.Value.Count})\n";
                    text.Data += $" Out: {Math.Round(kvp.Value.Current, 2)}MW / {Math.Round(kvp.Value.Max, 2)}MW";
                    text.Data += $" (Moy={kvp.Value.Moyen}MW)";
                }
                text.Position = surface.Position;
                surface.AddSprite(text);
                surface.Position += deltaPosition;
            }

            surface.Position += padding;
            surface.DrawGauge(surface.Position, batteries_store.Current, batteries_store.Max, style);
            surface.Position += deltaPosition;
            text.Data = $"Battery Store (n={batteries_store.Count})\n Store: {Math.Round(batteries_store.Current, 2)}MW / {Math.Round(batteries_store.Max, 2)}MW";
            text.Position = surface.Position;
            surface.AddSprite(text);

            consummers.ForEach(delegate (IMyTerminalBlock block)
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
                            max_input += resourceSink.RequiredInputByType(powerDefinitionId);
                            current_input += resourceSink.CurrentInputByType(powerDefinitionId);
                        }
                    }
                }
            });
            surface.Position += deltaPosition;
            surface.DrawGauge(surface.Position, current_input, max_input, style);
            surface.Position += deltaPosition;
            text.Data = $"Power In: {Math.Round(current_input, 2)}MW / {Math.Round(max_input, 2)}MW";
            text.Position = surface.Position;
            surface.AddSprite(text);

            surface.Position += deltaPosition;
        }
    }
}
