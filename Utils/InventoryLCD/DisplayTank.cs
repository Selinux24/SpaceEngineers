using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class DisplayTank
    {
        readonly Program program;

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        bool tank_h2 = false;
        bool tank_o2 = false;

        public DisplayTank(Program program)
        {
            this.program = program;
        }

        public void Load(MyIni MyIni)
        {
            panel = MyIni.Get("Tank", "panel").ToInt32(0);
            enable = MyIni.Get("Tank", "on").ToBoolean(false);
            scale = MyIni.Get("Tank", "scale").ToSingle(1f);
            tank_h2 = MyIni.Get("Tank", "H2").ToBoolean(true);
            tank_o2 = MyIni.Get("Tank", "O2").ToBoolean(true);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Tank", "panel", panel);
            MyIni.Set("Tank", "on", enable);
            MyIni.Set("Tank", "scale", scale);
            MyIni.Set("Tank", "H2", tank_h2);
            MyIni.Set("Tank", "O2", tank_o2);
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
            var types = new List<string>();
            if (tank_h2) types.Add("Hydrogen");
            if (tank_o2) types.Add("Oxygen");
            if (types.Count > 0)
            {
                foreach (string type in types)
                {
                    var tanks = BlockSystem<IMyGasTank>.SearchBlocks(program, block => String.IsNullOrEmpty(block.BlockDefinition.SubtypeName) ? block.BlockDefinition.TypeIdString.Contains(type) : block.BlockDefinition.SubtypeName.Contains(type));
                    float volumes = 0f;
                    float capacity = 0f;
                    float width = 50f * scale;
                    var style = new StyleGauge()
                    {
                        Orientation = SpriteOrientation.Horizontal,
                        Fullscreen = true,
                        Width = width,
                        Height = width,
                        Padding = new StylePadding(0),
                        Round = false,
                        RotationOrScale = 1f * scale,
                        Thresholds = program.MyProperty.TankThresholds
                    };

                    var text = new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Color = Color.DimGray,
                        Position = surface.Position + new Vector2(0, 0),
                        RotationOrScale = 1f * scale,
                        FontId = surface.Font,
                        Alignment = TextAlignment.LEFT

                    };

                    tanks.ForEach(delegate (IMyGasTank block)
                    {
                        volumes += (float)block.FilledRatio * block.Capacity / 1000;
                        capacity += block.Capacity / 1000;
                    });

                    surface.DrawGauge(surface.Position, volumes, capacity, style);
                    surface.Position += new Vector2(0, 60 * scale);
                    switch (type)
                    {
                        case "Hydrogen":
                            text.Data = $"H2: {Math.Round(volumes, 2)}M³/{Math.Round(capacity, 2)}M³";
                            break;
                        case "Oxygen":
                            text.Data = $"O2: {Math.Round(volumes, 2)}M³/{Math.Round(capacity, 2)}M³";
                            break;
                    }
                    text.Position = surface.Position;
                    surface.AddSprite(text);
                    surface.Position += new Vector2(0, 60 * scale);
                }
            }
        }
    }
}
