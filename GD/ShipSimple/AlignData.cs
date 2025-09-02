using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class AlignData
    {
        readonly Program ship;
        int tickCount = 0;

        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentTarget = 0;
        public Vector3D CurrentPos = Vector3D.Zero;
        public Vector3D TargetForward = new Vector3D(1, 0, 0);
        public Vector3D TargetUp = new Vector3D(0, 1, 0);
        public bool HasTarget = false;

        public Config Config => ship.Config;
        public Vector3D TargetPos => Waypoints[CurrentTarget];
        public Vector3D ToTarget => TargetPos - CurrentPos;
        public double Distance => ToTarget.Length();

        public AlignData(Program ship)
        {
            this.ship = ship;
        }

        public void Update()
        {
            if (!HasTarget) return;

            if (!Tick()) return;

            MonitorizeAlign();
        }
        bool Tick()
        {
            if (++tickCount < Config.AlignTicks) return false;

            CurrentPos = ship.GetDockPosition();
            tickCount = 0;
            return true;
        }

        void MonitorizeAlign()
        {
            if (CurrentTarget >= Waypoints.Count)
            {
                Clear();
                ship.ResetGyros();
                ship.ResetThrust();
                ship.Dock();
                return;
            }

            if (ship.IsDocked())
            {
                return;
            }

            bool corrected = AlignToVectors(TargetForward, TargetUp, Config.GyrosThr);
            if (corrected)
            {
                //Wait until aligned
                ship.ResetThrust();
                return;
            }

            NavigateWaypoints();
        }
        bool AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipForward = ship.GetDockingForwardDirection();
            var shipUp = ship.GetDockingUpDirection();

            double angleFW = Utils.AngleBetweenVectors(shipForward, targetForward);
            double angleUP = Utils.AngleBetweenVectors(shipUp, targetUp);
            ship.WriteInfoLCDs($"Target angles: {angleFW:F2} | {angleUP:F2}");

            if (angleFW <= thr && angleUP <= thr)
            {
                ship.ResetGyros();
                ship.WriteInfoLCDs("Aligned.");
                return false;
            }
            ship.WriteInfoLCDs("Aligning...");

            bool corrected = false;
            if (angleFW > thr)
            {
                var rotationAxisFW = Vector3D.Cross(shipForward, targetForward);
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ship.ApplyGyroOverride(rotationAxisFW);
                corrected = true;
            }

            if (angleUP > thr)
            {
                var rotationAxisUP = Vector3D.Cross(shipUp, targetUp);
                if (rotationAxisUP.Length() <= 0.001) rotationAxisUP = new Vector3D(1, 0, 0);
                ship.ApplyGyroOverride(rotationAxisUP);
                corrected = true;
            }

            return corrected;
        }
        void NavigateWaypoints()
        {
            ship.WriteInfoLCDs(GetAlignState());

            var distance = Distance;
            if (distance < Config.AlignDistanceThrWaypoints)
            {
                Next();
                ship.ResetThrust();
                return;
            }

            var desiredSpeed = CalculateDesiredSpeed(distance);
            var currentVelocity = ship.GetVelocity();
            var mass = ship.GetMass();
            var neededForce = Utils.CalculateThrustForce(ToTarget, desiredSpeed, currentVelocity, mass);

            ship.ApplyThrust(neededForce);
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

        void Next()
        {
            CurrentTarget++;
        }
        double CalculateDesiredSpeed(double distance)
        {
            //Calculates desired speed based on distance, when we are moving towards the last waypoint.
            double approachSpeed;
            if (CurrentTarget == 0) approachSpeed = Config.AlignSpeedWaypointFirst; //Speed ​​to the first approach point.
            else if (CurrentTarget == Waypoints.Count - 1) approachSpeed = Config.AlignSpeedWaypointLast; //Speed ​​from the last approach point.
            else approachSpeed = Config.AlignSpeedWaypoints; //Speed ​​between approach points.

            double desiredSpeed = approachSpeed;
            if (distance < Config.AlignExchangeSlowdownDistance && (CurrentTarget == 0 || CurrentTarget == Waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / Config.AlignExchangeSlowdownDistance * approachSpeed, 0.5);
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
