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
        const string SECTION = "Tank";
        const string SECTION_TANK_THRESHOLD = "TankThresholds";

        readonly Program program;
        readonly DisplayLcd displayLcd;
        readonly Dictionary<string, BlockSystem<IMyGasTank>> types = new Dictionary<string, BlockSystem<IMyGasTank>>();

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        string filter = "*";
        bool tankH2 = false;
        bool tankO2 = false;
        bool showPercent = true;
        GaugeThresholds tankThresholds;

        public DisplayTank(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni ini)
        {
            if (!ini.ContainsSection(SECTION)) return;

            panel = ini.Get(SECTION, "panel").ToInt32(0);
            enable = ini.Get(SECTION, "on").ToBoolean(false);
            scale = ini.Get(SECTION, "scale").ToSingle(1f);
            filter = ini.Get(SECTION, "filter").ToString("*");
            tankH2 = ini.Get(SECTION, "H2").ToBoolean(true);
            tankO2 = ini.Get(SECTION, "O2").ToBoolean(true);
            showPercent = ini.Get(SECTION, "show_percent").ToBoolean(true);

            tankThresholds = GaugeThresholds.LoadThresholds(ini, SECTION_TANK_THRESHOLD);
            if (tankThresholds == null) tankThresholds = GaugeThresholds.DefaultTankThesholds();

            types.Clear();
            if (tankH2) types.Add("Hydrogen", new BlockSystem<IMyGasTank>());
            if (tankO2) types.Add("Oxygen", new BlockSystem<IMyGasTank>());

            Search();
        }
        public void Save(MyIni ini)
        {
            ini.Set(SECTION, "panel", panel);
            ini.Set(SECTION, "on", enable);
            ini.Set(SECTION, "scale", scale);
            ini.Set(SECTION, "filter", filter);
            ini.Set(SECTION, "H2", tankH2);
            ini.Set(SECTION, "O2", tankO2);
            ini.Set(SECTION, "show_percent", showPercent);

            GaugeThresholds.SaveThresholds(ini, tankThresholds, SECTION_TANK_THRESHOLD);
        }

        void Search()
        {
            var blockFilter = BlockFilter<IMyGasTank>.Create(displayLcd.Block, filter);

            foreach (var type in types)
            {
                Func<IMyGasTank, bool> collect =
                    (block) =>
                    {
                        if (string.IsNullOrEmpty(block.BlockDefinition.SubtypeName))
                        {
                            return block.BlockDefinition.TypeIdString.Contains(type.Key);
                        }
                        else
                        {
                            return block.BlockDefinition.SubtypeName.Contains(type.Key);
                        }
                    };

                BlockSystem<IMyGasTank>.SearchByFilter(program, type.Value, blockFilter, collect);
            }
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
                Thresholds = tankThresholds,
                Percent = showPercent,
            };

            var deltaPosition = new Vector2(0, 45) * scale;
            foreach (var t in types)
            {
                var type = t.Key;
                var tanks = t.Value;

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
                    data = $"H2: {tanks.List.Count} {Math.Round(volumes, 2):#,000.00}M³/{Math.Round(capacity, 2):#,000.00}M³";
                }
                else if (type.Equals("Oxygen"))
                {
                    data = $"O2: {tanks.List.Count} {Math.Round(volumes, 2):#,000.00}M³/{Math.Round(capacity, 2):#,000.00}M³";
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
