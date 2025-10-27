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
        readonly BlockSystem<IMyThrust> thrusts = new BlockSystem<IMyThrust>();
        readonly BlockSystem<IMyCockpit> cockpit = new BlockSystem<IMyCockpit>();
        float mass = 0f;

        int panel = 0;
        bool enable = false;
        double scale = 1d;
        bool oneLine = false;

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

            BlockSystem<IMyCockpit>.SearchBlocks(program, cockpit);
            BlockSystem<IMyThrust>.SearchBlocks(program, thrusts);
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
            if (cockpit.IsEmpty) return;

            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();

            UpdateMassAndForces();
            Draw(surface);
        }
        void UpdateMassAndForces()
        {
            mass = cockpit.First.CalculateShipMass().TotalMass;

            forces.Clear();
            forces.Add("Up", thrusts.List.FindAll(x => x.GridThrustDirection == Vector3I.Down));
            forces.Add("Down", thrusts.List.FindAll(x => x.GridThrustDirection == Vector3I.Up));
            forces.Add("Left", thrusts.List.FindAll(x => x.GridThrustDirection == Vector3I.Right));
            forces.Add("Right", thrusts.List.FindAll(x => x.GridThrustDirection == Vector3I.Left));
            forces.Add("Forward", thrusts.List.FindAll(x => x.GridThrustDirection == Vector3I.Backward));
            forces.Add("Backward", thrusts.List.FindAll(x => x.GridThrustDirection == Vector3I.Forward));
        }
        void Draw(SurfaceDrawing surface)
        {
            var offset = new Vector2(0, 35f * (float)scale);

            var text = new MySprite()
            {
                Type = SpriteType.TEXT,
                Position = surface.Position,
                RotationOrScale = (float)scale,
                FontId = EnumFont.Monospace,
                Alignment = TextAlignment.LEFT,
            };

            if (oneLine == true)
            {
                text.Data = "Thrusts:";
                text.Color = Color.LightGreen;
                text.Position = surface.Position;
                surface.AddSprite(text);
                surface.Position += offset;
            }

            foreach (var item in forces)
            {
                if (oneLine)
                {
                    Draw1Line(surface, item, text, offset);
                }
                else
                {
                    Draw2Line(surface, item, text, offset);
                }
            }
        }
        void Draw1Line(SurfaceDrawing surface, KeyValuePair<string, List<IMyThrust>> item, MySprite text, Vector2 offset)
        {
            var force = item.Value.Select(x => x.MaxThrust).Sum();
            var speed = Math.Round(force / mass, 1);

            text.Data = $"{force / 1000,6}kN {speed,6}m/s² {item.Key}";
            text.Color = Color.LightGreen;
            text.Position = surface.Position;
            surface.AddSprite(text);
            surface.Position += offset;
        }
        void Draw2Line(SurfaceDrawing surface, KeyValuePair<string, List<IMyThrust>> item, MySprite text, Vector2 offset)
        {
            var force = item.Value.Select(x => x.MaxThrust).Sum();
            var speed = Math.Round(force / mass, 1);
            var count = item.Value.Count;

            text.Data = $"Thrusts {item.Key}: {count}";
            text.Color = Color.DimGray;
            text.Position = surface.Position;
            surface.AddSprite(text);
            surface.Position += offset;

            text.Data = $"{force / 1000,8}kN {speed,8}m/s²";
            text.Color = Color.LightGreen;
            text.Position = surface.Position;
            surface.AddSprite(text);
            surface.Position += offset;
        }
    }
}
