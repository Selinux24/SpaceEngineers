using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    public class Route
    {
        public string LoadBase;
        public string UnloadBase;
        public bool LoadBaseOnPlanet;
        public bool UnloadBaseOnPlanet;
        public readonly List<Vector3D> ToLoadBaseWaypoints;
        public readonly List<Vector3D> ToUnloadBaseWaypoints;

        public Route(string loadBase, bool loadBaseOnPlanet, List<Vector3D> toLoadBase, string unloadBase, bool unloadBaseOnPlanet, List<Vector3D> toUnloadBase)
        {
            LoadBase = loadBase;
            LoadBaseOnPlanet = loadBaseOnPlanet;
            ToLoadBaseWaypoints = new List<Vector3D>();
            if (toLoadBase != null) ToLoadBaseWaypoints.AddRange(toLoadBase);

            UnloadBase = unloadBase;
            UnloadBaseOnPlanet = unloadBaseOnPlanet;
            ToUnloadBaseWaypoints = new List<Vector3D>();
            if (toUnloadBase != null) ToUnloadBaseWaypoints.AddRange(toUnloadBase);
        }

        public bool IsValid()
        {
            return
                !string.IsNullOrWhiteSpace(LoadBase) &&
                ToLoadBaseWaypoints.Count > 0 &&
                !string.IsNullOrWhiteSpace(UnloadBase) &&
                ToUnloadBaseWaypoints.Count > 0;
        }

        public string GetState()
        {
            return IsValid() ?
                $"From {LoadBase}({ToLoadBaseWaypoints.Count}wp) To {UnloadBase}({ToUnloadBaseWaypoints.Count}wp)" :
                "No route defined.";
        }

        public void Clear()
        {
            LoadBase = "";
            LoadBaseOnPlanet = false;
            ToLoadBaseWaypoints.Clear();
            UnloadBase = "";
            UnloadBaseOnPlanet = false;
            ToUnloadBaseWaypoints.Clear();
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            var loadBase = Utils.ReadString(parts, "LoadBase");
            var unloadBase = Utils.ReadString(parts, "UnloadBase");

            if (string.IsNullOrWhiteSpace(loadBase) || string.IsNullOrWhiteSpace(unloadBase))
            {
                return;
            }

            LoadBase = loadBase;
            LoadBaseOnPlanet = Utils.ReadInt(parts, "LoadBaseOnPlanet") == 1;
            ToLoadBaseWaypoints.Clear();
            ToLoadBaseWaypoints.AddRange(Utils.ReadVectorList(parts, "ToLoadBaseWaypoints"));

            UnloadBase = unloadBase;
            UnloadBaseOnPlanet = Utils.ReadInt(parts, "UnloadBaseOnPlanet") == 1;
            ToUnloadBaseWaypoints.Clear();
            ToUnloadBaseWaypoints.AddRange(Utils.ReadVectorList(parts, "ToUnloadBaseWaypoints"));
        }
        public string SaveToStorage()
        {
            if (!IsValid())
            {
                return "";
            }

            var parts = new List<string>
            {
                $"LoadBase={LoadBase}",
                $"LoadBaseOnPlanet={(LoadBaseOnPlanet ? 1 : 0)}",
                $"ToLoadBaseWaypoints={Utils.VectorListToStr(ToLoadBaseWaypoints)}",

                $"UnloadBase={UnloadBase}",
                $"UnloadBaseOnPlanet={(UnloadBaseOnPlanet ? 1 : 0)}",
                $"ToUnloadBaseWaypoints={Utils.VectorListToStr(ToUnloadBaseWaypoints)}"
            };

            return string.Join("¬", parts);
        }
    }
}
