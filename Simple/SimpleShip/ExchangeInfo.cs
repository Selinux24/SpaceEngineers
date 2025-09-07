using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class ExchangeInfo
    {
        public string Exchange = null;
        public Vector3D Forward = Vector3D.Zero;
        public Vector3D Up = Vector3D.Zero;
        public readonly List<Vector3D> ApproachingWaypoints = new List<Vector3D>();
        public readonly List<Vector3D> DepartingWaypoints = new List<Vector3D>();

        public ExchangeInfo(string[] lines)
        {
            Exchange = Utils.ReadString(lines, "Exchange");
            Forward = Utils.ReadVector(lines, "Forward");
            Up = Utils.ReadVector(lines, "Up");
            ApproachingWaypoints = Utils.ReadVectorList(lines, "Waypoints");
            DepartingWaypoints = new List<Vector3D>(ApproachingWaypoints);
            DepartingWaypoints.Reverse();
        }
    }
}
