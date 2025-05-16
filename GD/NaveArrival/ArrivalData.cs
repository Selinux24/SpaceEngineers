using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class ArrivalData
    {
        public bool HasPosition = false;
        public Vector3D TargetPosition = Vector3D.Zero;
        public string ArrivalMessage = null;

        public void Initialize(Vector3D position, string arrivalMessage)
        {
            HasPosition = true;
            TargetPosition = position;
            ArrivalMessage = arrivalMessage;
        }
        public void Clear()
        {
            HasPosition = false;
            TargetPosition = Vector3D.Zero;
            ArrivalMessage = null;
        }

        public void LoadFromStorage(string[] storageLines)
        {
            if (storageLines.Length == 0)
            {
                return;
            }

            TargetPosition = Utils.StrToVector(Utils.ReadString(storageLines, "TargetPosition"));
            HasPosition = Utils.ReadInt(storageLines, "HasPosition") == 1;
            ArrivalMessage = Utils.ReadString(storageLines, "ArrivalMessage");
        }
        public string SaveToStorage()
        {
            Dictionary<string, string> datos = new Dictionary<string, string>();

            datos["TargetPosition"] = Utils.VectorToStr(TargetPosition);
            datos["HasPosition"] = HasPosition ? "1" : "0";
            datos["ArrivalMessage"] = ArrivalMessage ?? "";

            var lineas = new List<string>();
            foreach (var kvp in datos)
            {
                lineas.Add($"{kvp.Key}={kvp.Value}");
            }

            return string.Join(Environment.NewLine, lineas);
        }
    }
}
