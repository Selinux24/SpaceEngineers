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
        public Vector3D Origin;
        public Vector3D Destination;
        public string Command = null;
        public bool HasTarget = false;
        public bool Thrusting = false;
        public readonly List<Vector3D> EvadingPoints = new List<Vector3D>();
        public DateTime AlignThrustStart = DateTime.Now;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public TimeSpan EstimatedArrival => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double TotalDistance => Vector3D.Distance(Origin, Destination);
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;

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

        public void Initialize(Vector3D origin, Vector3D destination, string commad)
        {
            Origin = origin;
            Destination = destination;
            Command = commad;
            HasTarget = true;
            EvadingPoints.Clear();
            CurrentState = CruisingStatus.Locating;
            Thrusting = false;
            EvadingPoints.Clear();

            lastHit = new MyDetectedEntityInfo();
        }
        public void Clear()
        {
            Origin = Vector3D.Zero;
            Destination = Vector3D.Zero;
            Command = null;
            HasTarget = false;
            EvadingPoints.Clear();
            CurrentState = CruisingStatus.Idle;
            Thrusting = false;
            EvadingPoints.Clear();
            lastHit = new MyDetectedEntityInfo();
        }

        public void UpdatePositionAndVelocity(Vector3D position, double speed)
        {
            var toTarget = Destination - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = toTarget.Length();
            Speed = speed;
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
            Origin = Utils.ReadVector(parts, "Origin");
            Destination = Utils.ReadVector(parts, "Destination");
            Command = Utils.ReadString(parts, "Command");
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
                $"Origin={Utils.VectorToStr(Origin)}",
                $"Destination={Utils.VectorToStr(Destination)}",
                $"Command={Command}",
                $"HasTarget={(HasTarget?1:0)}",
                $"Thrusting={(Thrusting?1:0)}",
                $"EvadingPoints={Utils.VectorListToStr(EvadingPoints)}",
            };

            return string.Join("¬", parts);
        }
    }
}
