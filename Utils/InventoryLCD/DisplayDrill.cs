using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class DisplayDrill
    {
        readonly Program program;
        readonly DisplayLcd displayLcd;

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
        BlockSystem<IMyShipDrill> drillInventories;

        public DisplayDrill(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni MyIni)
        {
            panel = MyIni.Get("Drills", "panel").ToInt32(0);
            enable = MyIni.Get("Drills", "on").ToBoolean(false);
            filter = MyIni.Get("Drills", "filter").ToString("GM:Drills");
            drillsOrientation = MyIni.Get("Drills", "orientation").ToString("y");
            drillsRotate = MyIni.Get("Drills", "rotate").ToBoolean(false);
            drillsFlipX = MyIni.Get("Drills", "flip_x").ToBoolean(false);
            drillsFlipY = MyIni.Get("Drills", "flip_y").ToBoolean(false);
            drillsSize = MyIni.Get("Drills", "size").ToSingle(50f);
            drillsInfo = MyIni.Get("Drills", "info").ToBoolean(false);
            drillsPaddingX = MyIni.Get("Drills", "padding_x").ToSingle(0f);
            drillsPaddingY = MyIni.Get("Drills", "padding_y").ToSingle(0f);

            var blockFilter = BlockFilter<IMyShipDrill>.Create(displayLcd.Block, filter);
            drillInventories = BlockSystem<IMyShipDrill>.SearchByFilter(program, blockFilter);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Drills", "panel", panel);
            MyIni.Set("Drills", "on", enable);
            MyIni.Set("Drills", "filter", filter);
            MyIni.Set("Drills", "orientation", drillsOrientation);
            MyIni.Set("Drills", "rotate", drillsRotate);
            MyIni.Set("Drills", "flip_x", drillsFlipX);
            MyIni.Set("Drills", "flip_y", drillsFlipY);
            MyIni.Set("Drills", "size", drillsSize);
            MyIni.Set("Drills", "info", drillsInfo);
            MyIni.Set("Drills", "padding_x", drillsPaddingX);
            MyIni.Set("Drills", "padding_y", drillsPaddingY);
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
                Thresholds = program.MyProperty.ChestThresholds
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
                    FontId = surface.Font,
                    Alignment = TextAlignment.LEFT

                });
                surface.Position += new Vector2(0, 20);
            }

            float xMin = 0f;
            float xMax = 0f;
            float yMin = 0f;
            float yMax = 0f;
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

            float padding = 4f;
            var paddingScreen = new Vector2(drillsPaddingX, drillsPaddingY);
            drillInventories.ForEach(drill =>
            {
                var block_inventory = drill.GetInventory(0);
                long volume = block_inventory.CurrentVolume.RawValue;
                long maxVolume = block_inventory.MaxVolume.RawValue;
                float x = 0;
                float y = 0;
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
                var position_relative = drillsRotate ?
                    new Vector2(y * (width + padding), x * (width + padding)) :
                    new Vector2(x * (width + padding), y * (width + padding));

                surface.DrawGauge(surface.Position + position_relative + paddingScreen, volume, maxVolume, style);
            });
        }
    }
}
