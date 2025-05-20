using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class ArrivalData
    {
        public Vector3D TargetPosition = Vector3D.Zero;
        public string Command = null;
        public bool HasPosition = false;

        public void Initialize(Vector3D position, string commad)
        {
            TargetPosition = position;
            Command = commad;
            HasPosition = true;
        }
        public void Clear()
        {
            TargetPosition = Vector3D.Zero;
            Command = null;
            HasPosition = false;
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');
            if (parts.Length != 3) return;

            TargetPosition = Utils.StrToVector(Utils.ReadString(parts, "TargetPosition"));
            Command = Utils.ReadString(parts, "Command");
            HasPosition = Utils.ReadInt(parts, "HasPosition") == 1;
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"TargetPosition={Utils.VectorToStr(TargetPosition)}",
                $"Command={Command}",
                $"HasPosition={(HasPosition ? 1 : 0)}",
            };

            return string.Join("¬", parts);
        }
    }
}
