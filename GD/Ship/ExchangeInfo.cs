using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class ExchangeInfo
    {
        public readonly string Exchange;
        public readonly Vector3D Forward;
        public readonly Vector3D Up;
        public readonly List<Vector3D> ApproachingWaypoints;
        public readonly List<Vector3D> DepartingWaypoints;

        public ExchangeInfo()
        {
            Exchange = null;
            Forward = Vector3D.Zero;
            Up = Vector3D.Zero;
            ApproachingWaypoints = new List<Vector3D>();
            DepartingWaypoints = new List<Vector3D>();
        }

        public ExchangeInfo(string exchange, Vector3D forward, Vector3D up, List<Vector3D> waypoints)
        {
            Exchange = exchange;
            Forward = forward;
            Up = up;
            ApproachingWaypoints = new List<Vector3D>(waypoints);
            DepartingWaypoints = new List<Vector3D>(waypoints);
            DepartingWaypoints.Reverse();
        }
        public ExchangeInfo(string[] lines)
        {
            Exchange = Utils.ReadString(lines, "Exchange");
            Forward = Utils.ReadVector(lines, "Forward");
            Up = Utils.ReadVector(lines, "Up");
            ApproachingWaypoints = Utils.ReadVectorList(lines, "WayPoints");
            DepartingWaypoints = new List<Vector3D>(ApproachingWaypoints);
            DepartingWaypoints.Reverse();
        }
    }
}
