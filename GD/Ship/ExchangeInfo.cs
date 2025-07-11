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

        public ExchangeInfo()
        {

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

        public void Initialize(ExchangeInfo info)
        {
            Exchange = info.Exchange;
            Forward = info.Forward;
            Up = info.Up;
            ApproachingWaypoints.Clear();
            ApproachingWaypoints.AddRange(info.ApproachingWaypoints);
            DepartingWaypoints.Clear();
            DepartingWaypoints.AddRange(info.DepartingWaypoints);
        }
        public void Initialize(string exchange, Vector3D forward, Vector3D up, List<Vector3D> waypoints)
        {
            Exchange = exchange;
            Forward = forward;
            Up = up;
            ApproachingWaypoints.Clear();
            ApproachingWaypoints.AddRange(waypoints);
            DepartingWaypoints.Clear();
            DepartingWaypoints.AddRange(waypoints);
            DepartingWaypoints.Reverse();
        }
        public void Clear()
        {
            Exchange = null;
            Forward = Vector3D.Zero;
            Up = Vector3D.Zero;
            ApproachingWaypoints.Clear();
            DepartingWaypoints.Clear();
        }
    }
}
