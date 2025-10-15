﻿using Sandbox.ModAPI.Ingame;
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
        bool search = true;
        string filter = "*";
        string drills_orientation = "y";
        bool drills_rotate = false;
        bool drills_flip_x = false;
        bool drills_flip_y = false;
        bool drills_info = false;
        float drills_size = 50f;
        float drills_padding_x = 0f;
        float drills_padding_y = 0f;
        BlockSystem<IMyShipDrill> drill_inventories;

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
            drills_orientation = MyIni.Get("Drills", "orientation").ToString("y");
            drills_rotate = MyIni.Get("Drills", "rotate").ToBoolean(false);
            drills_flip_x = MyIni.Get("Drills", "flip_x").ToBoolean(false);
            drills_flip_y = MyIni.Get("Drills", "flip_y").ToBoolean(false);
            drills_size = MyIni.Get("Drills", "size").ToSingle(50f);
            drills_info = MyIni.Get("Drills", "info").ToBoolean(false);
            drills_padding_x = MyIni.Get("Drills", "padding_x").ToSingle(0f);
            drills_padding_y = MyIni.Get("Drills", "padding_y").ToSingle(0f);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Drills", "panel", panel);
            MyIni.Set("Drills", "on", enable);
            MyIni.Set("Drills", "filter", filter);
            MyIni.Set("Drills", "orientation", drills_orientation);
            MyIni.Set("Drills", "rotate", drills_rotate);
            MyIni.Set("Drills", "flip_x", drills_flip_x);
            MyIni.Set("Drills", "flip_y", drills_flip_y);
            MyIni.Set("Drills", "size", drills_size);
            MyIni.Set("Drills", "info", drills_info);
            MyIni.Set("Drills", "padding_x", drills_padding_x);
            MyIni.Set("Drills", "padding_y", drills_padding_y);
        }
        private void Search()
        {
            var block_filter = BlockFilter<IMyShipDrill>.Create(displayLcd.Block, filter);
            drill_inventories = BlockSystem<IMyShipDrill>.SearchByFilter(program, block_filter);

            search = false;
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
            if (search) Search();

            float width = drills_size;
            float padding = 4f;
            float x_min = 0f;
            float x_max = 0f;
            float y_min = 0f;
            float y_max = 0f;
            bool first = true;
            var padding_screen = new Vector2(drills_padding_x, drills_padding_y);
            var style = new StyleGauge()
            {
                Orientation = SpriteOrientation.Horizontal,
                Fullscreen = false,
                Width = width,
                Height = width,
                Padding = new StylePadding(0),
                Round = false,
                RotationOrScale = 0.5f,
                Percent = drills_size > 49,
                Thresholds = program.MyProperty.ChestThresholds
            };

            if (drills_info)
            {
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = $"Drill Number:{drill_inventories.List.Count} ({filter})",
                    Size = new Vector2(width, width),
                    Color = Color.DimGray,
                    Position = surface.Position + new Vector2(0, 0),
                    RotationOrScale = 0.5f,
                    FontId = surface.Font,
                    Alignment = TextAlignment.LEFT

                });
                surface.Position += new Vector2(0, 20);
            }

            drill_inventories.ForEach(delegate (IMyShipDrill drill)
            {
                switch (drills_orientation)
                {
                    case "x":
                        if (first || drill.Position.Y < x_min) x_min = drill.Position.Y;
                        if (first || drill.Position.Y > x_max) x_max = drill.Position.Y;
                        if (first || drill.Position.Z < y_min) y_min = drill.Position.Z;
                        if (first || drill.Position.Z > y_max) y_max = drill.Position.Z;
                        break;
                    case "y":
                        if (first || drill.Position.X < x_min) x_min = drill.Position.X;
                        if (first || drill.Position.X > x_max) x_max = drill.Position.X;
                        if (first || drill.Position.Z < y_min) y_min = drill.Position.Z;
                        if (first || drill.Position.Z > y_max) y_max = drill.Position.Z;
                        break;
                    default:
                        if (first || drill.Position.X < x_min) x_min = drill.Position.X;
                        if (first || drill.Position.X > x_max) x_max = drill.Position.X;
                        if (first || drill.Position.Y < y_min) y_min = drill.Position.Y;
                        if (first || drill.Position.Y > y_max) y_max = drill.Position.Y;
                        break;
                }
                first = false;
            });

            drill_inventories.ForEach(delegate (IMyShipDrill drill)
            {
                IMyInventory block_inventory = drill.GetInventory(0);
                long volume = block_inventory.CurrentVolume.RawValue;
                long maxVolume = block_inventory.MaxVolume.RawValue;
                float x = 0;
                float y = 0;
                switch (drills_orientation)
                {
                    case "x":
                        x = Math.Abs(drill.Position.Y - x_min);
                        y = Math.Abs(drill.Position.Z - y_min);
                        break;
                    case "y":
                        x = Math.Abs(drill.Position.X - x_min);
                        y = Math.Abs(drill.Position.Z - y_min);
                        break;
                    default:
                        x = Math.Abs(drill.Position.X - x_min);
                        y = Math.Abs(drill.Position.Y - y_min);
                        break;
                }
                if (drills_flip_x) x = Math.Abs(x_max - x_min) - x;
                if (drills_flip_y) y = Math.Abs(y_max - y_min) - y;
                var position_relative = drills_rotate ?
                    new Vector2(y * (width + padding), x * (width + padding)) :
                    new Vector2(x * (width + padding), y * (width + padding));

                surface.DrawGauge(surface.Position + position_relative + padding_screen, volume, maxVolume, style);
            });
        }
    }
}
