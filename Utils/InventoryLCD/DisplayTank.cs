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
        readonly List<string> types = new List<string>();
        readonly BlockSystem<IMyGasTank> tanks = new BlockSystem<IMyGasTank>();

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        bool tankH2 = false;
        bool tankO2 = false;

        public DisplayTank(Program program)
        {
            this.program = program;
        }

        public void Load(MyIni ini)
        {
            panel = ini.Get("Tank", "panel").ToInt32(0);
            enable = ini.Get("Tank", "on").ToBoolean(false);
            scale = ini.Get("Tank", "scale").ToSingle(1f);
            tankH2 = ini.Get("Tank", "H2").ToBoolean(true);
            tankO2 = ini.Get("Tank", "O2").ToBoolean(true);

            types.Clear();
            if (tankH2) types.Add("Hydrogen");
            if (tankO2) types.Add("Oxygen");
        }
        public void Save(MyIni ini)
        {
            ini.Set("Tank", "panel", panel);
            ini.Set("Tank", "on", enable);
            ini.Set("Tank", "scale", scale);
            ini.Set("Tank", "H2", tankH2);
            ini.Set("Tank", "O2", tankO2);
        }

        public void Draw(Drawing drawing)
        {
            if (!enable) return;
            if (types.Count == 0) return;

            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();

            Draw(surface);
        }
        void Draw(SurfaceDrawing surface)
        {
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
                Thresholds = program.Config.TankThresholds
            };

            var deltaPosition = new Vector2(0, 45) * scale;
            foreach (string type in types)
            {
                BlockSystem<IMyGasTank>.SearchBlocks(program, tanks, block => string.IsNullOrEmpty(block.BlockDefinition.SubtypeName) ? block.BlockDefinition.TypeIdString.Contains(type) : block.BlockDefinition.SubtypeName.Contains(type));
                if (tanks.List.Count == 0) continue;

                float volumes = 0f;
                float capacity = 0f;
                tanks.ForEach(block =>
                {
                    volumes += (float)block.FilledRatio * block.Capacity;
                    capacity += block.Capacity;
                });
                volumes /= 1000f;
                capacity /= 1000f;

                surface.DrawGauge(surface.Position, volumes, capacity, style);
                surface.Position += new Vector2(0, height) + padding;

                string data;
                if (type.Equals("Hydrogen"))
                {
                    data = $"H2: {Math.Round(volumes, 2):#,000.00}M³/{Math.Round(capacity, 2):#,000.00}M³";
                }
                else if (type.Equals("Oxygen"))
                {
                    data = $"O2: {Math.Round(volumes, 2):#,000.00}M³/{Math.Round(capacity, 2):#,000.00}M³";
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
        }
    }
}
