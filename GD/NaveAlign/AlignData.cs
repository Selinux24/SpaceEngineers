using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    class AlignData
    {
        public List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentTarget = 0;
        public Vector3D TargetForward = new Vector3D(1, 0, 0);
        public Vector3D TargetUp = new Vector3D(0, 1, 0);
        public bool HasTarget = false;
        public string ReachCommand = null;

        public void InitAlignShip(string data)
        {
            CurrentTarget = 0;
            Waypoints.Clear();
            HasTarget = false;

            var parts = data.Split('¬');
            if (parts.Length != 2) return;

            var coords = parts[0].Split('|');
            if (coords.Length != 3) return;
            TargetForward = -Vector3D.Normalize(Utils.StrToVector(coords[0]));
            TargetUp = Vector3D.Normalize(Utils.StrToVector(coords[1]));
            Waypoints = Utils.StrToVectorList(coords[2]);

            ReachCommand = parts[1];

            HasTarget = true;
        }
        public void Next()
        {
            CurrentTarget++;
        }
        public void Clear()
        {
            CurrentTarget = 0;
            Waypoints.Clear();
            HasTarget = false;
            ReachCommand = null;
        }

        public void LoadFromStorage(string[] storageLines)
        {
            if (storageLines.Length == 0)
            {
                return;
            }

            Waypoints = Utils.StrToVectorList(Utils.ReadString(storageLines, "Waypoints"));
            CurrentTarget = Utils.ReadInt(storageLines, "CurrentTarget");
            TargetForward = Utils.ReadVector(storageLines, "TargetForward");
            TargetUp = Utils.ReadVector(storageLines, "TargetUp");
            HasTarget = Utils.ReadInt(storageLines, "HasTarget") == 1;
            ReachCommand = Utils.ReadString(storageLines, "ReachCommand");
        }
        public string SaveToStorage()
        {
            Dictionary<string, string> datos = new Dictionary<string, string>();

            datos["Waypoints"] = string.Join(";", Waypoints.Select(Utils.VectorToStr));
            datos["CurrentTarget"] = CurrentTarget.ToString();
            datos["TargetForward"] = Utils.VectorToStr(TargetForward);
            datos["TargetUp"] = Utils.VectorToStr(TargetUp);
            datos["HasTarget"] = HasTarget ? "1" : "0";
            datos["ReachCommand"] = ReachCommand ?? "";

            var lineas = new List<string>();
            foreach (var kvp in datos)
            {
                lineas.Add($"{kvp.Key}={kvp.Value}");
            }

            return string.Join(Environment.NewLine, lineas);
        }
    }
}
