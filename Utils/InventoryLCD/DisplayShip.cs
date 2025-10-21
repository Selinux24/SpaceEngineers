using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class DisplayShip
    {
        readonly Program program;
        readonly Dictionary<string, List<IMyThrust>> forces = new Dictionary<string, List<IMyThrust>>();

        int panel = 0;
        bool enable = false;
        double scale = 1d;
        bool oneLine = false;
        BlockSystem<IMyThrust> thrusts = null;
        BlockSystem<IMyCockpit> cockpit = null;

        public DisplayShip(Program program)
        {
            this.program = program;
        }

        public void Load(MyIni MyIni)
        {
            panel = MyIni.Get("Ship", "panel").ToInt32(0);
            enable = MyIni.Get("Ship", "on").ToBoolean(false);
            scale = MyIni.Get("Ship", "scale").ToDouble(1d);
            oneLine = MyIni.Get("Ship", "one_line").ToBoolean(false);

            cockpit = BlockSystem<IMyCockpit>.SearchBlocks(program);
            thrusts = BlockSystem<IMyThrust>.SearchBlocks(program);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Ship", "panel", panel);
            MyIni.Set("Ship", "on", enable);
            MyIni.Set("Ship", "scale", scale);
            MyIni.Set("Ship", "one_line", oneLine);
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
            float mass = 0f;
            if (!cockpit.IsEmpty)
            {
                var shipMass = cockpit.First.CalculateShipMass();
                mass = shipMass.TotalMass;
            }

            forces.Clear();

            var valueUp = thrusts.List.Where(x => x.GridThrustDirection == Vector3I.Down).ToList();
            forces.Add("Up", valueUp);

            var valueDown = thrusts.List.Where(x => x.GridThrustDirection == Vector3I.Up).ToList();
            forces.Add("Down", valueDown);

            var valueLeft = thrusts.List.Where(x => x.GridThrustDirection == Vector3I.Right).ToList();
            forces.Add("Left", valueLeft);

            var valueRight = thrusts.List.Where(x => x.GridThrustDirection == Vector3I.Left).ToList();
            forces.Add("Right", valueRight);

            var valueForward = thrusts.List.Where(x => x.GridThrustDirection == Vector3I.Backward).ToList();
            forces.Add("Forward", valueForward);

            var valueBackward = thrusts.List.Where(x => x.GridThrustDirection == Vector3I.Forward).ToList();
            forces.Add("Backward", valueBackward);

            var text = new MySprite()
            {
                Type = SpriteType.TEXT,
                Position = surface.Position + new Vector2(0, 0),
                RotationOrScale = (float)scale,
                FontId = EnumFont.Monospace,
                Alignment = TextAlignment.LEFT,
            };
            float offsetY = 35f * (float)scale;

            if (oneLine == true)
            {
                text.Data = "Thrusts:";
                text.Color = Color.LightGreen;
                text.Position = surface.Position;
                surface.AddSprite(text);
                surface.Position += new Vector2(0, offsetY);
            }

            foreach (var item in forces)
            {
                if (oneLine)
                {
                    Draw1Line(surface, item, text, mass, offsetY);
                }
                else
                {
                    Draw2Line(surface, item, text, mass, offsetY);
                }
            }
        }
        void Draw2Line(SurfaceDrawing surface, KeyValuePair<string, List<IMyThrust>> item, MySprite text, float mass, float offsetY)
        {
            var force = item.Value.Select(x => x.MaxThrust).Sum();
            var speed = Math.Round(force / mass, 1);
            var count = item.Value.Count();
            text.Data = $"Thrusts {item.Key}: {count}";
            text.Color = Color.DimGray;
            text.Position = surface.Position;
            surface.AddSprite(text);
            surface.Position += new Vector2(0, offsetY);

            text.Data = $"{force / 1000,8}kN {speed,8}m/s²";
            text.Color = Color.LightGreen;
            text.Position = surface.Position;
            surface.AddSprite(text);
            surface.Position += new Vector2(0, offsetY);
        }
        void Draw1Line(SurfaceDrawing surface, KeyValuePair<string, List<IMyThrust>> item, MySprite text, float mass, float offsetY)
        {
            var force = item.Value.Select(x => x.MaxThrust).Sum();
            var speed = Math.Round(force / mass, 1);
            var count = item.Value.Count();
            text.Data = $"{force / 1000,6}kN {speed,6}m/s² {item.Key}";
            text.Color = Color.LightGreen;
            text.Position = surface.Position;
            surface.AddSprite(text);
            surface.Position += new Vector2(0, offsetY);
        }
    }
}
