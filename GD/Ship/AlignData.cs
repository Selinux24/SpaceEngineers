using System.Collections.Generic;
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
        public string Command = null;

        public void Initialize(string data)
        {
            Clear();

            var parts = data.Split('¬');
            if (parts.Length != 2) return;

            var coords = parts[0].Split('|');
            if (coords.Length != 3) return;

            TargetForward = -Vector3D.Normalize(Utils.StrToVector(coords[0]));
            TargetUp = Vector3D.Normalize(Utils.StrToVector(coords[1]));
            Waypoints = Utils.StrToVectorList(coords[2]);

            Command = parts[1];

            HasTarget = true;
        }
        public void Clear()
        {
            CurrentTarget = 0;
            Waypoints.Clear();
            HasTarget = false;
            Command = null;
        }

        public void Next()
        {
            CurrentTarget++;
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');
            if (parts.Length != 6) return;

            Waypoints = Utils.ReadVectorList(parts, "Waypoints");
            CurrentTarget = Utils.ReadInt(parts, "CurrentTarget");
            TargetForward = Utils.ReadVector(parts, "TargetForward");
            TargetUp = Utils.ReadVector(parts, "TargetUp");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;
            Command = Utils.ReadString(parts, "Command");
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"CurrentTarget={CurrentTarget}",
                $"TargetForward={Utils.VectorToStr(TargetForward)}",
                $"TargetUp={Utils.VectorToStr(TargetUp)}",
                $"HasTarget={(HasTarget ? 1 : 0)}",
                $"Command={Command}",
            };

            return string.Join("¬", parts);
        }
    }
}
