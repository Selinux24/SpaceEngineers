using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Route
    {
        public readonly string Name;
        public readonly string LoadBase;
        public readonly string UnloadBase;
        public readonly bool LoadBaseOnPlanet;
        public readonly bool UnloadBaseOnPlanet;
        public readonly List<Vector3D> ToLoadBaseWaypoints;
        public readonly List<Vector3D> ToUnloadBaseWaypoints;

        public Route(string name, string loadBase, bool loadBaseOnPlanet, List<Vector3D> toLoadBase, string unloadBase, bool unloadBaseOnPlanet, List<Vector3D> toUnloadBase)
        {
            Name = name;
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

        public static bool Read(string cfgLine, out Route route)
        {

            if (string.IsNullOrWhiteSpace(cfgLine))
            {
                route = null;
                return false;
            }

            var parts = cfgLine.Split('|');
            if (parts.Length < 7)
            {
                route = null;
                return false;
            }

            var name = Utils.ReadString(parts, "Name");

            var loadBase = Utils.ReadString(parts, "LoadBase");
            var loadBaseOnPlanet = Utils.ReadBool(parts, "LoadBaseOnPlanet");
            var toLoadBase = Utils.ReadVectorList(parts, "ToLoadBaseWaypoints");

            var unloadBase = Utils.ReadString(parts, "UnloadBase");
            var unloadBaseOnPlanet = Utils.ReadBool(parts, "UnloadBaseOnPlanet");
            var toUnloadBase = Utils.ReadVectorList(parts, "ToUnloadBaseWaypoints");

            route = new Route(name, loadBase, loadBaseOnPlanet, toLoadBase, unloadBase, unloadBaseOnPlanet, toUnloadBase);
            return true;
        }
    }
}
