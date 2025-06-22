using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class AlignData
    {
        readonly Config config;

        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentTarget = 0;
        public Vector3D TargetForward = new Vector3D(1, 0, 0);
        public Vector3D TargetUp = new Vector3D(0, 1, 0);
        public bool HasTarget = false;
        public string Command = null;

        public bool Done => HasTarget && CurrentTarget >= Waypoints.Count;

        public AlignData(Config config)
        {
            this.config = config;
        }

        public void Initialize(Vector3D forward, Vector3D up, List<Vector3D> wayPoints, string command)
        {
            TargetForward = -Vector3D.Normalize(forward);
            TargetUp = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wayPoints);
            Command = command;
            HasTarget = true;
        }
        public void Clear()
        {
            CurrentTarget = 0;
            Waypoints.Clear();
            HasTarget = false;
            Command = null;
        }

        public void Next()
        {
            CurrentTarget++;
        }
        public double CalculateDesiredSpeed(double distance)
        {
            //Calcula velocidad deseada basada en distancia, cuando estemos avanzando hacia el último waypoint.
            double approachSpeed;
            if (CurrentTarget == 0) approachSpeed = config.AlignMaxApproachSpeed; //Velocidad hasta el primer punto de aproximación.
            else if (CurrentTarget == Waypoints.Count - 1) approachSpeed = config.AlignMaxApproachSpeedLocking; //Velocidad desde el último punto de aproximación.
            else approachSpeed = config.AlignMaxApproachSpeedAprox; //Velocidad entre puntos de aproximación.

            double desiredSpeed = approachSpeed;
            if (distance < config.AlignSlowdownDistance && (CurrentTarget == 0 || CurrentTarget == Waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / config.AlignSlowdownDistance * approachSpeed, 0.5);
            }

            return desiredSpeed;
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
            Command = Utils.ReadString(parts, "Command");
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
                $"Command={Command}",
            };

            return string.Join("¬", parts);
        }
    }
}
