using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class DisplayDrill
    {
        const string SECTION = "Drills";
        const string SECTION_CHEST_THRESHOLDS = "DrillsChestThresholds";

        readonly Program program;
        readonly DisplayLcd displayLcd;
        readonly BlockSystem<IMyShipDrill> drillInventories = new BlockSystem<IMyShipDrill>();
        float xMin = 0f;
        float xMax = 0f;
        float yMin = 0f;
        float yMax = 0f;

        int panel = 0;
        bool enable = false;
        string filter = "*";
        string drillsOrientation = "y";
        bool drillsRotate = false;
        bool drillsFlipX = false;
        bool drillsFlipY = false;
        bool drillsInfo = false;
        float drillsSize = 50f;
        float drillsPaddingX = 0f;
        float drillsPaddingY = 0f;
        GaugeThresholds chestThresholds;

        public DisplayDrill(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni ini)
        {
            if (!ini.ContainsSection(SECTION)) return;

            panel = ini.Get(SECTION, "panel").ToInt32(0);
            enable = ini.Get(SECTION, "on").ToBoolean(false);
            filter = ini.Get(SECTION, "filter").ToString("GM:Drills");
            drillsOrientation = ini.Get(SECTION, "orientation").ToString("y");
            drillsRotate = ini.Get(SECTION, "rotate").ToBoolean(false);
            drillsFlipX = ini.Get(SECTION, "flip_x").ToBoolean(false);
            drillsFlipY = ini.Get(SECTION, "flip_y").ToBoolean(false);
            drillsSize = ini.Get(SECTION, "size").ToSingle(50f);
            drillsInfo = ini.Get(SECTION, "info").ToBoolean(false);
            drillsPaddingX = ini.Get(SECTION, "padding_x").ToSingle(0f);
            drillsPaddingY = ini.Get(SECTION, "padding_y").ToSingle(0f);

            chestThresholds = GaugeThresholds.LoadThresholds(ini, SECTION_CHEST_THRESHOLDS);
            if (chestThresholds == null) chestThresholds = GaugeThresholds.DefaultChestThesholds();

            var blockFilter = BlockFilter<IMyShipDrill>.Create(displayLcd.Block, filter);
            BlockSystem<IMyShipDrill>.SearchByFilter(program, drillInventories, blockFilter);
        }
        public void Save(MyIni ini)
        {
            ini.Set(SECTION, "panel", panel);
            ini.Set(SECTION, "on", enable);
            ini.Set(SECTION, "filter", filter);
            ini.Set(SECTION, "orientation", drillsOrientation);
            ini.Set(SECTION, "rotate", drillsRotate);
            ini.Set(SECTION, "flip_x", drillsFlipX);
            ini.Set(SECTION, "flip_y", drillsFlipY);
            ini.Set(SECTION, "size", drillsSize);
            ini.Set(SECTION, "info", drillsInfo);
            ini.Set(SECTION, "padding_x", drillsPaddingX);
            ini.Set(SECTION, "padding_y", drillsPaddingY);

            GaugeThresholds.SaveThresholds(ini, chestThresholds, SECTION_CHEST_THRESHOLDS);
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
            float width = drillsSize;

            var style = new StyleGauge()
            {
                Orientation = SpriteOrientation.Horizontal,
                Fullscreen = false,
                Width = width,
                Height = width,
                Padding = new StylePadding(0),
                Round = false,
                RotationOrScale = 0.5f,
                Percent = drillsSize > 49,
                Thresholds = chestThresholds
            };

            if (drillsInfo)
            {
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = $"Drill Number:{drillInventories.List.Count} ({filter})",
                    Size = new Vector2(width, width),
                    Color = Color.DimGray,
                    Position = surface.Position + new Vector2(0, 0),
                    RotationOrScale = 0.5f,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });
                surface.Position += new Vector2(0, 20);
            }

            DrillLimits();

            float padding = width + 4f;
            var paddingScreen = new Vector2(drillsPaddingX, drillsPaddingY);
            drillInventories.ForEach(drill =>
            {
                var blockInventory = drill.GetInventory(0);
                long volume = blockInventory.CurrentVolume.RawValue;
                long maxVolume = blockInventory.MaxVolume.RawValue;

                var positionRelative = GetRelativePosition(drill, padding);

                surface.DrawGauge(surface.Position + positionRelative + paddingScreen, volume, maxVolume, style);
            });
        }
        void DrillLimits()
        {
            xMin = 0f;
            xMax = 0f;
            yMin = 0f;
            yMax = 0f;
            bool first = true;
            drillInventories.ForEach(drill =>
            {
                switch (drillsOrientation)
                {
                    case "x":
                        if (first || drill.Position.Y < xMin) xMin = drill.Position.Y;
                        if (first || drill.Position.Y > xMax) xMax = drill.Position.Y;
                        if (first || drill.Position.Z < yMin) yMin = drill.Position.Z;
                        if (first || drill.Position.Z > yMax) yMax = drill.Position.Z;
                        break;
                    case "y":
                        if (first || drill.Position.X < xMin) xMin = drill.Position.X;
                        if (first || drill.Position.X > xMax) xMax = drill.Position.X;
                        if (first || drill.Position.Z < yMin) yMin = drill.Position.Z;
                        if (first || drill.Position.Z > yMax) yMax = drill.Position.Z;
                        break;
                    default:
                        if (first || drill.Position.X < xMin) xMin = drill.Position.X;
                        if (first || drill.Position.X > xMax) xMax = drill.Position.X;
                        if (first || drill.Position.Y < yMin) yMin = drill.Position.Y;
                        if (first || drill.Position.Y > yMax) yMax = drill.Position.Y;
                        break;
                }
                first = false;
            });
        }
        Vector2 GetRelativePosition(IMyShipDrill drill, float padding)
        {
            float x;
            float y;
            switch (drillsOrientation)
            {
                case "x":
                    x = Math.Abs(drill.Position.Y - xMin);
                    y = Math.Abs(drill.Position.Z - yMin);
                    break;
                case "y":
                    x = Math.Abs(drill.Position.X - xMin);
                    y = Math.Abs(drill.Position.Z - yMin);
                    break;
                default:
                    x = Math.Abs(drill.Position.X - xMin);
                    y = Math.Abs(drill.Position.Y - yMin);
                    break;
            }
            if (drillsFlipX) x = Math.Abs(xMax - xMin) - x;
            if (drillsFlipY) y = Math.Abs(yMax - yMin) - y;

            var p = drillsRotate ? new Vector2(y, x) : new Vector2(x, y);
            return p * padding;
        }
    }
}
