using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    public class Route
    {
        public string LoadBase;
        public string UnloadBase;
        public readonly List<Vector3D> ToLoadBaseWaypoints = new List<Vector3D>();
        public readonly List<Vector3D> ToUnloadBaseWaypoints = new List<Vector3D>();

        public Route(string loadBase, string unloadBase, List<Vector3D> toLoadBaseWaypoints, List<Vector3D> toUnloadBaseWaypoints)
        {
            LoadBase = loadBase;
            UnloadBase = unloadBase;

            ToLoadBaseWaypoints = new List<Vector3D>();
            if (toLoadBaseWaypoints != null) ToLoadBaseWaypoints.AddRange(toLoadBaseWaypoints);

            ToUnloadBaseWaypoints = new List<Vector3D>();
            if (toUnloadBaseWaypoints != null) ToUnloadBaseWaypoints.AddRange(toUnloadBaseWaypoints);
        }

        public List<Vector3D> GetLoadWaypoints(Vector3D position)
        {
            List<Vector3D> waypoints = new List<Vector3D>
            {
                position
            };
            waypoints.AddRange(ToLoadBaseWaypoints);

            return waypoints;
        }
        public List<Vector3D> GetUnLoadWaypoints(Vector3D position)
        {
            List<Vector3D> waypoints = new List<Vector3D>
            {
                position
            };
            waypoints.AddRange(ToUnloadBaseWaypoints);

            return waypoints;
        }
    }
}
