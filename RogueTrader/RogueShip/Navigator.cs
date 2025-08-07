using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Navigator
    {
        readonly Program parent;
        readonly Config config;
        int tickCount = 0;

        public bool InGravity = false;
        public string Exchange = null;
        public Vector3D TargetForward = new Vector3D(1, 0, 0);
        public Vector3D TargetUp = new Vector3D(0, 1, 0);
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentTarget = 0;
        public Action Callback = null;
        public bool HasTarget = false;

        public Vector3D CurrentPos = Vector3D.Zero;
        public Vector3D TargetPos => Waypoints[CurrentTarget];
        public Vector3D ToTarget => TargetPos - CurrentPos;
        public double Distance => ToTarget.Length();
        public bool Done => HasTarget && CurrentTarget >= Waypoints.Count;

        public Navigator(Program parent, Config config)
        {
            this.parent = parent;
            this.config = config;
        }

        public void AproximateToDock(bool inGravity, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, Action onAproximationCompleted)
        {
            InGravity = inGravity;
            Exchange = exchange;
            TargetForward = -Vector3D.Normalize(fw);
            TargetUp = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentTarget = 0;
            Callback = onAproximationCompleted;
            HasTarget = true;
        }
        public void SeparateFromDock(bool inGravity, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, Action onSeparationCompleted)
        {
            InGravity = inGravity;
            Exchange = exchange;
            TargetForward = -Vector3D.Normalize(fw);
            TargetUp = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentTarget = 0;
            Callback = onSeparationCompleted;
            HasTarget = true;
        }
        public void NavigateTo(List<Vector3D> waypoints, Action onNavigationCompleted)
        {
            Callback = onNavigationCompleted;
        }
        public void Clear()
        {
            InGravity = false;
            Exchange = null;
            TargetForward = Vector3D.Zero;
            TargetUp = Vector3D.Zero;
            Waypoints.Clear();
            CurrentTarget = 0;
            Callback = null;
            HasTarget = false;
        }

        public void Update()
        {
            if (!HasTarget)
            {
                return;
            }

            if (!Tick())
            {
                return;
            }

            MonitorizeDock();
        }
        bool Tick()
        {
            if (++tickCount < config.NavigationTicks)
            {
                return false;
            }
            tickCount = 0;
            return true;
        }
        void MonitorizeDock()
        {
            if (CurrentTarget >= Waypoints.Count)
            {
                Callback?.Invoke();
                Clear();
                parent.ResetGyros();
                parent.ResetThrust();
                return;
            }

            if (parent.IsConnected())
            {
                return;
            }

            bool corrected = parent.AlignToVectors(TargetForward, TargetUp, config.GyrosThr);
            if (corrected)
            {
                //Wait until aligned
                parent.ResetThrust();
                return;
            }

            CurrentPos = parent.GetPosition();

            NavigateWaypoints();
        }
        void NavigateWaypoints()
        {
            parent.WriteInfoLCDs(GetState());

            var distance = Distance;
            if (distance < config.DockingDistanceThrWaypoints)
            {
                CurrentTarget++;
                parent.ResetThrust();
                return;
            }

            double desiredSpeed = CalculateDesiredSpeed(distance);
            var currentVelocity = parent.GetLinearVelocity();
            double mass = parent.GetPhysicalMass();
            var neededForce = Utils.CalculateThrustForce(ToTarget, desiredSpeed, currentVelocity, mass);

            parent.ApplyThrust(neededForce);
        }
        double CalculateDesiredSpeed(double distance)
        {
            //Calculates desired speed based on distance, when we are moving towards the last waypoint.
            double approachSpeed;
            if (CurrentTarget == 0) approachSpeed = config.DockingSpeedWaypointFirst; //Speed ​​to the first approach point.
            else if (CurrentTarget == Waypoints.Count - 1) approachSpeed = config.DockingSpeedWaypointLast; //Speed ​​from the last approach point.
            else approachSpeed = config.DockingSpeedWaypoints; //Speed ​​between approach points.

            double desiredSpeed = approachSpeed;
            if (distance < config.DockingSlowdownDistance && (CurrentTarget == 0 || CurrentTarget == Waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / config.DockingSlowdownDistance * approachSpeed, 0.5);
            }

            return desiredSpeed;
        }

        string GetState()
        {
            return
                $"Distance to destination: {Utils.DistanceToStr(Distance)}" + Environment.NewLine +
                $"Progress: {CurrentTarget + 1}/{Waypoints.Count}." + Environment.NewLine;
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');
            if (parts.Length != 6) return;

            InGravity = Utils.ReadInt(parts, "InGravity") == 1;
            Exchange = Utils.ReadString(parts, "Exchange");
            TargetForward = Utils.ReadVector(parts, "TargetForward");
            TargetUp = Utils.ReadVector(parts, "TargetUp");
            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(parts, "Waypoints"));
            CurrentTarget = Utils.ReadInt(parts, "CurrentTarget");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"InGravity={(InGravity ? 1 : 0)}",
                $"Exchange={Exchange}",
                $"TargetForward={Utils.VectorToStr(TargetForward)}",
                $"TargetUp={Utils.VectorToStr(TargetUp)}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"CurrentTarget={CurrentTarget}",
                $"HasTarget={(HasTarget ? 1 : 0)}",
            };

            return string.Join("¬", parts);
        }
    }
}
