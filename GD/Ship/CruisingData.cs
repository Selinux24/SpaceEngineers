using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class CruisingData
    {
        readonly Config config;
        int tickCount = 0;

        public CruisingStatus CurrentState = CruisingStatus.Idle;
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentWaypointIndex = 0;
        public string TerminalMessage = null;
        public bool HasTarget = false;
        public bool Thrusting = false;
        public readonly List<Vector3D> EvadingPoints = new List<Vector3D>();
        public DateTime AlignThrustStart = DateTime.Now;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public TimeSpan EstimatedArrival => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double TotalDistance => GetTotalDistance();
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;
        public Vector3D NextWaypoint => CurrentWaypointIndex < Waypoints.Count ? Waypoints[CurrentWaypointIndex] : Vector3D.Zero;

        private MyDetectedEntityInfo lastHit;

        public CruisingData(Config config)
        {
            this.config = config;
        }

        public bool Tick()
        {
            if (++tickCount < config.CruisingTicks)
            {
                return false;
            }
            tickCount = 0;
            return true;
        }

        public void Initialize(Vector3D position, List<Vector3D> waypoints, string terminalMessage)
        {
            Waypoints.Clear();
            Waypoints.Add(position);
            Waypoints.AddRange(waypoints);
            CurrentWaypointIndex = 1;
            TerminalMessage = terminalMessage;
            HasTarget = true;
            EvadingPoints.Clear();
            CurrentState = CruisingStatus.Locating;
            Thrusting = false;
            EvadingPoints.Clear();

            lastHit = new MyDetectedEntityInfo();
        }
        public void Clear()
        {
            Waypoints.Clear();
            CurrentWaypointIndex = 0;
            TerminalMessage = null;
            HasTarget = false;
            EvadingPoints.Clear();
            CurrentState = CruisingStatus.Idle;
            Thrusting = false;
            EvadingPoints.Clear();
            lastHit = new MyDetectedEntityInfo();
        }

        public double GetTotalDistance()
        {
            if (Waypoints.Count < 2)
            {
                return 0;
            }

            double d = 0;
            for (int i = 1; i < Waypoints.Count; i++)
            {
                d += Vector3D.Distance(Waypoints[i - 1], Waypoints[i]);
            }
            return d;
        }
        public double GetRemainingDistance(Vector3D position)
        {
            if (Waypoints.Count < 2)
            {
                return 0;
            }

            double d = 0;
            for (int i = CurrentWaypointIndex; i < Waypoints.Count; i++)
            {
                var p = i == CurrentWaypointIndex ? position : Waypoints[i - 1];
                d += Vector3D.Distance(p, Waypoints[i]);
            }
            return d;
        }

        public void UpdatePositionAndVelocity(Vector3D position, double speed)
        {
            if (Waypoints.Count == 0 || CurrentWaypointIndex >= Waypoints.Count)
            {
                return;
            }

            var target = Waypoints[CurrentWaypointIndex];
            var toTarget = target - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = GetRemainingDistance(position);
            Speed = speed;

            if (toTarget.Length() <= 1000)
            {
                CurrentWaypointIndex++;
            }
        }

        public bool IsObstacleAhead(IMyCameraBlock camera, double collisionDetectRange, Vector3D velocity)
        {
            camera.EnableRaycast = true;

            MatrixD cameraMatrixInv = MatrixD.Invert(camera.WorldMatrix);
            Vector3D localDirection = Vector3D.TransformNormal(Vector3D.Normalize(velocity), cameraMatrixInv);
            if (camera.CanScan(collisionDetectRange, localDirection))
            {
                lastHit = camera.Raycast(collisionDetectRange, localDirection);
                return
                    !lastHit.IsEmpty() &&
                    lastHit.Type != MyDetectedEntityType.Planet &&
                    Vector3D.Distance(lastHit.HitPosition.Value, camera.GetPosition()) <= collisionDetectRange;
            }

            return false;
        }
        public string PrintObstacle()
        {
            if (!lastHit.IsEmpty())
            {
                return $"Obstacle detected. {lastHit.Name} - Type {lastHit.Type}";
            }

            return "";
        }
        public bool CalculateEvadingWaypoints(IMyCameraBlock camera, double safetyDistance)
        {
            if (EvadingPoints.Count > 0)
            {
                //Evading points have already been calculated
                return true;
            }

            if (lastHit.IsEmpty())
            {
                return false;
            }

            var obstacleCenter = lastHit.Position;
            var obstacleSize = Math.Max(lastHit.BoundingBox.Extents.X, Math.Max(lastHit.BoundingBox.Extents.Y, lastHit.BoundingBox.Extents.Z));

            //Point on the obstacle from the ship's point of view
            var p1 = obstacleCenter + (camera.WorldMatrix.Up * obstacleSize);
            EvadingPoints.Add(p1);

            //Point on the other side of the obstacle from the ship's point of view
            var p2 = obstacleCenter + (camera.WorldMatrix.Forward * (obstacleSize + safetyDistance));
            EvadingPoints.Add(p2);

            return true;
        }
        public void ClearObstacle()
        {
            lastHit = new MyDetectedEntityInfo();
        }

        public string GetTripState()
        {
            return
                $"Trip: {Utils.DistanceToStr(TotalDistance)}" + Environment.NewLine +
                $"To target: {Utils.DistanceToStr(DistanceToTarget)}" + Environment.NewLine +
                $"Speed: {Speed:F2}" + Environment.NewLine +
                $"ETC: {EstimatedArrival:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                $"Progress {Progress:P1}" + Environment.NewLine;
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            CurrentState = (CruisingStatus)Utils.ReadInt(parts, "CurrentState");
            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(parts, "Waypoints"));
            TerminalMessage = Utils.ReadString(parts, "TerminalMessage");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;
            Thrusting = Utils.ReadInt(parts, "Thrusting") == 1;
            EvadingPoints.Clear();
            EvadingPoints.AddRange(Utils.ReadVectorList(parts, "EvadingPoints"));
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"CurrentState={(int)CurrentState}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"TerminalMessage={TerminalMessage}",
                $"HasTarget={(HasTarget?1:0)}",
                $"Thrusting={(Thrusting?1:0)}",
                $"EvadingPoints={Utils.VectorListToStr(EvadingPoints)}",
            };

            return string.Join("¬", parts);
        }
    }
}
