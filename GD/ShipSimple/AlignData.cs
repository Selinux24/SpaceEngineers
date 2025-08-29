using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class AlignData
    {
        readonly Config config;
        int tickCount = 0;

        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentTarget = 0;
        public Vector3D CurrentPos = Vector3D.Zero;
        public Vector3D TargetForward = new Vector3D(1, 0, 0);
        public Vector3D TargetUp = new Vector3D(0, 1, 0);
        public bool HasTarget = false;

        public Vector3D TargetPos => Waypoints[CurrentTarget];
        public Vector3D ToTarget => TargetPos - CurrentPos;
        public double Distance => ToTarget.Length();
        public bool Done => HasTarget && CurrentTarget >= Waypoints.Count;

        public AlignData(Config config)
        {
            this.config = config;
        }

        public bool Tick()
        {
            if (++tickCount < config.AlignTicks)
            {
                return false;
            }
            tickCount = 0;
            return true;
        }

        public void Initialize(ExchangeInfo info)
        {
            TargetForward = -Vector3D.Normalize(info.Forward);
            TargetUp = Vector3D.Normalize(info.Up);
            Waypoints.Clear();
            Waypoints.AddRange(info.ApproachingWaypoints);
            HasTarget = true;
        }
        public void Clear()
        {
            CurrentTarget = 0;
            Waypoints.Clear();
            HasTarget = false;
        }

        public void UpdatePosition(Vector3D position)
        {
            CurrentPos = position;
        }

        public void Next()
        {
            CurrentTarget++;
        }
        public double CalculateDesiredSpeed(double distance)
        {
            //Calculates desired speed based on distance, when we are moving towards the last waypoint.
            double approachSpeed;
            if (CurrentTarget == 0) approachSpeed = config.AlignSpeedWaypointFirst; //Speed ​​to the first approach point.
            else if (CurrentTarget == Waypoints.Count - 1) approachSpeed = config.AlignSpeedWaypointLast; //Speed ​​from the last approach point.
            else approachSpeed = config.AlignSpeedWaypoints; //Speed ​​between approach points.

            double desiredSpeed = approachSpeed;
            if (distance < config.AlignExchangeSlowdownDistance && (CurrentTarget == 0 || CurrentTarget == Waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / config.AlignExchangeSlowdownDistance * approachSpeed, 0.5);
            }

            return desiredSpeed;
        }

        public string GetAlignState()
        {
            return
                $"Distance to destination: {Utils.DistanceToStr(Distance)}" + Environment.NewLine +
                $"Progress: {CurrentTarget + 1}/{Waypoints.Count}." + Environment.NewLine;
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');
            if (parts.Length != 6) return;

            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(parts, "Waypoints"));
            CurrentTarget = Utils.ReadInt(parts, "CurrentTarget");
            TargetForward = Utils.ReadVector(parts, "TargetForward");
            TargetUp = Utils.ReadVector(parts, "TargetUp");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"CurrentTarget={CurrentTarget}",
                $"TargetForward={Utils.VectorToStr(TargetForward)}",
                $"TargetUp={Utils.VectorToStr(TargetUp)}",
                $"HasTarget={(HasTarget ? 1 : 0)}",
            };

            return string.Join("¬", parts);
        }
    }
}
