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
        public readonly List<Vector3D> ToLoadBaseWaypoints = new List<Vector3D>();
        public readonly List<Vector3D> ToUnloadBaseWaypoints = new List<Vector3D>();

        public Route(string loadBase, bool loadBaseOnPlanet, List<Vector3D> toLoadBaseWaypoints, string unloadBase, bool unloadBaseOnPlanet, List<Vector3D> toUnloadBaseWaypoints)
        {
            LoadBase = loadBase;
            LoadBaseOnPlanet = loadBaseOnPlanet;
            ToLoadBaseWaypoints = new List<Vector3D>();
            if (toLoadBaseWaypoints != null) ToLoadBaseWaypoints.AddRange(toLoadBaseWaypoints);

            UnloadBase = unloadBase;
            UnloadBaseOnPlanet = unloadBaseOnPlanet;
            ToUnloadBaseWaypoints = new List<Vector3D>();
            if (toUnloadBaseWaypoints != null) ToUnloadBaseWaypoints.AddRange(toUnloadBaseWaypoints);
        }
    }
}
