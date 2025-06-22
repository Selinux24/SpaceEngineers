using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Atmospheric navigation data for ships, used to track take-off and landing operations.
    /// </summary>
    class AtmNavigationData
    {
        public AtmNavigationStatus CurrentState = AtmNavigationStatus.Idle;
        public bool Landing = false;
        public Vector3D Origin;
        public Vector3D Destination;
        public string ExchangeName;
        public Vector3D ExchangeForward;
        public Vector3D ExchangeUp;
        public readonly List<Vector3D> ExchangeApproachingWaypoints = new List<Vector3D>();
        public string Command = null;
        public bool HasTarget = false;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public TimeSpan EstimatedArrival => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double TotalDistance => Vector3D.Distance(Origin, Destination);
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;

        public void Initialize(bool landing, Vector3D origin, Vector3D destination, string commad)
        {
            CurrentState = AtmNavigationStatus.Undocking;
            Landing = landing;
            Origin = origin;
            Destination = destination;
            Command = commad;
            HasTarget = true;
        }
        public void Clear()
        {
            CurrentState = AtmNavigationStatus.Idle;
            Landing = false;
            Origin = Vector3D.Zero;
            Destination = Vector3D.Zero;
            Command = null;
            HasTarget = false;

            ClearExchange();
        }

        public void SetExchange(string name, Vector3D forward, Vector3D up, List<Vector3D> waypoints)
        {
            ExchangeName = name;
            ExchangeForward = forward;
            ExchangeUp = up;
            ExchangeApproachingWaypoints.Clear();
            ExchangeApproachingWaypoints.AddRange(waypoints);
        }
        public void ClearExchange()
        {
            ExchangeName = null;
            ExchangeForward = Vector3D.Zero;
            ExchangeUp = Vector3D.Zero;
            ExchangeApproachingWaypoints.Clear();
        }

        public void UpdatePositionAndVelocity(Vector3D position, double speed)
        {
            var toTarget = Destination - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = toTarget.Length();
            Speed = speed;
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            CurrentState = (AtmNavigationStatus)Utils.ReadInt(parts, "CurrentState");
            Landing = Utils.ReadInt(parts, "Landing") == 1;
            Origin = Utils.ReadVector(parts, "Origin");
            Destination = Utils.ReadVector(parts, "Destination");
            Command = Utils.ReadString(parts, "Command");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;

            ExchangeName = Utils.ReadString(parts, "ExchangeName");
            ExchangeForward = Utils.ReadVector(parts, "ExchangeForward");
            ExchangeUp = Utils.ReadVector(parts, "ExchangeUp");
            ExchangeApproachingWaypoints.Clear();
            ExchangeApproachingWaypoints.AddRange(Utils.ReadVectorList(parts, "ExchangeApproachingWaypoints"));
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"CurrentState={(int)CurrentState}",
                $"Landing={(Landing?1:0)}",
                $"Origin={Utils.VectorToStr(Origin)}",
                $"Destination={Utils.VectorToStr(Destination)}",
                $"Command={Command}",
                $"HasTarget={(HasTarget?1:0)}",

                $"ExchangeName={ExchangeName}",
                $"ExchangeForward={Utils.VectorToStr(ExchangeForward)}",
                $"ExchangeUp={Utils.VectorToStr(ExchangeUp)}",
                $"ExchangeApproachingWaypoints={Utils.VectorListToStr(ExchangeApproachingWaypoints)}",
            };

            return string.Join("¬", parts);
        }
    }
}
