using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class NavigationData
    {
        public NavigationStatus CurrentState = NavigationStatus.Idle;
        public Vector3D Origin;
        public Vector3D Destination;
        public string Command = null;
        public bool HasTarget = false;
        public bool Thrusting = false;
        public readonly List<Vector3D> EvadingPoints = new List<Vector3D>();
        public DateTime AlignThrustStart = DateTime.Now;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }

        private MyDetectedEntityInfo lastHit;

        public void Initialize(Vector3D origin, Vector3D destination, string commad)
        {
            Origin = origin;
            Destination = destination;
            Command = commad;
            HasTarget = true;
            EvadingPoints.Clear();
            CurrentState = NavigationStatus.Locating;
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
            CurrentState = NavigationStatus.Idle;
            Thrusting = false;
            EvadingPoints.Clear();
            lastHit = new MyDetectedEntityInfo();
        }

        public void UpdatePosition(Vector3D position)
        {
            var toTarget = Destination - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = toTarget.Length();
        }

        public bool IsObstacleAhead(IMyCameraBlock camera, double collisionDetectRange)
        {
            camera.EnableRaycast = true;
            if (camera.CanScan(collisionDetectRange))
            {
                lastHit = camera.Raycast(collisionDetectRange);
                return !lastHit.IsEmpty() && Vector3D.Distance(lastHit.HitPosition.Value, camera.GetPosition()) <= collisionDetectRange;
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
        public bool CalculateEvadingWaypoints(IMyCameraBlock camera)
        {
            if (EvadingPoints.Count > 0)
            {
                // Ya se han calculado los puntos de evasión
                return true;
            }

            if (lastHit.IsEmpty())
            {
                return false;
            }

            var obstacleCenter = lastHit.Position;
            var obstacleSize = Math.Max(lastHit.BoundingBox.Extents.X, Math.Max(lastHit.BoundingBox.Extents.Y, lastHit.BoundingBox.Extents.Z));

            //Punto sobre el obstáculo desde el punto de vista de la nave
            var p1 = obstacleCenter + (camera.WorldMatrix.Up * obstacleSize);
            EvadingPoints.Add(p1);

            //Punto al otro lado del obstáculo desde el punto de vista de la nave
            var p2 = obstacleCenter + (camera.WorldMatrix.Forward * obstacleSize);
            EvadingPoints.Add(p2);

            return true;
        }
        public void ClearObstacle()
        {
            lastHit = new MyDetectedEntityInfo();
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            CurrentState = (NavigationStatus)Utils.ReadInt(parts, "CurrentState");
            Origin = Utils.ReadVector(parts, "Origin");
            Destination = Utils.ReadVector(parts, "Destination");
            Command = Utils.ReadString(parts, "Command");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;
            Thrusting = Utils.ReadInt(parts, "Thrusting") == 1;
            int evadingCount = Utils.ReadInt(parts, "EvadingPoints");
            EvadingPoints.Clear();
            EvadingPoints.AddRange(Utils.StrToVectorList(parts[parts.Length - 1]));
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
                $"EvadingPoints={EvadingPoints.Count}",
                $"{Utils.VectorListToStr(EvadingPoints)}",
            };

            return string.Join("¬", parts);
        }
    }
}
