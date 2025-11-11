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

        StyleGauge styleGauge;
        float padding;
        Vector2 paddingScreen;

        float xMin = 0f;
        float xMax = 0f;
        float yMin = 0f;
        float yMax = 0f;

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

            styleGauge = new StyleGauge()
            {
                Orientation = SpriteOrientation.Horizontal,
                Fullscreen = false,
                Width = drillsSize,
                Height = drillsSize,
                Padding = new StylePadding(0),
                Round = false,
                RotationOrScale = 0.5f,
                Percent = drillsSize > 49,
                Thresholds = chestThresholds
            };

            padding = drillsSize + 4f;
            paddingScreen = new Vector2(drillsPaddingX, drillsPaddingY);
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
            if (drillsInfo)
            {
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = $"Drill Number:{drillInventories.List.Count} ({filter})",
                    Size = new Vector2(drillsSize),
                    Color = Color.DimGray,
                    Position = surface.Position,
                    RotationOrScale = 0.5f,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });
                surface.Position += new Vector2(0, 20);
            }

            DrillLimits();

            drillInventories.ForEach(drill =>
            {
                var p = GetRelativePosition(drill.Position) * padding;

                var bl = drill.GetInventory(0);
                long volume = bl.CurrentVolume.RawValue;
                long maxVolume = bl.MaxVolume.RawValue;

                surface.DrawGauge(surface.Position + p + paddingScreen, volume, maxVolume, styleGauge);
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
                var p = drill.Position;
                switch (drillsOrientation)
                {
                    case "x":
                        if (first || p.Y < xMin) xMin = p.Y;
                        if (first || p.Y > xMax) xMax = p.Y;
                        if (first || p.Z < yMin) yMin = p.Z;
                        if (first || p.Z > yMax) yMax = p.Z;
                        break;
                    case "y":
                        if (first || p.X < xMin) xMin = p.X;
                        if (first || p.X > xMax) xMax = p.X;
                        if (first || p.Z < yMin) yMin = p.Z;
                        if (first || p.Z > yMax) yMax = p.Z;
                        break;
                    default:
                        if (first || p.X < xMin) xMin = p.X;
                        if (first || p.X > xMax) xMax = p.X;
                        if (first || p.Y < yMin) yMin = p.Y;
                        if (first || p.Y > yMax) yMax = p.Y;
                        break;
                }
                first = false;
            });
        }
        Vector2 GetRelativePosition(Vector3I p)
        {
            float x;
            float y;
            switch (drillsOrientation)
            {
                case "x":
                    x = Math.Abs(p.Y - xMin);
                    y = Math.Abs(p.Z - yMin);
                    break;
                case "y":
                    x = Math.Abs(p.X - xMin);
                    y = Math.Abs(p.Z - yMin);
                    break;
                default:
                    x = Math.Abs(p.X - xMin);
                    y = Math.Abs(p.Y - yMin);
                    break;
            }
            if (drillsFlipX) x = Math.Abs(xMax - xMin) - x;
            if (drillsFlipY) y = Math.Abs(yMax - yMin) - y;

            return drillsRotate ? new Vector2(y, x) : new Vector2(x, y);
        }
    }
}
