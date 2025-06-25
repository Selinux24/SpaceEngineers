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
        readonly Config config;
        int tickCount = 0;
        DateTime separationTime = DateTime.MinValue;

        public AtmNavigationStatus CurrentState = AtmNavigationStatus.Idle;
        public bool Landing = false;
        public Vector3D Origin;
        public Vector3D Destination;
        public string ExchangeName;
        public Vector3D ExchangeForward;
        public Vector3D ExchangeUp;
        public readonly List<Vector3D> ExchangeApproachingWaypoints = new List<Vector3D>();
        public ExchangeTasks ExchangeTask = ExchangeTasks.None;
        public string Command = null;
        public bool HasTarget = false;
        public string StateMsg;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public TimeSpan EstimatedArrival => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double TotalDistance => Vector3D.Distance(Origin, Destination);
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;
        public TimeSpan SeparationSecs => TimeSpan.FromSeconds(config.AtmNavigationSeparationSecs);

        public AtmNavigationData(Config config)
        {
            this.config = config;
        }

        public bool Tick()
        {
            if (++tickCount < config.AtmNavigationTicks)
            {
                return false;
            }
            tickCount = 0;
            return true;
        }

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

        public void SetExchange(string name, Vector3D forward, Vector3D up, List<Vector3D> waypoints, ExchangeTasks task)
        {
            ExchangeName = name;
            ExchangeForward = forward;
            ExchangeUp = up;
            ExchangeApproachingWaypoints.Clear();
            ExchangeApproachingWaypoints.AddRange(waypoints);
            ExchangeTask = task;
        }
        public void ClearExchange()
        {
            ExchangeName = null;
            ExchangeForward = Vector3D.Zero;
            ExchangeUp = Vector3D.Zero;
            ExchangeApproachingWaypoints.Clear();
            ExchangeTask = ExchangeTasks.None;
        }

        public void UpdatePositionAndVelocity(Vector3D position, double speed)
        {
            var toTarget = Destination - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = toTarget.Length();
            Speed = speed;
        }

        public void StartSeparation()
        {
            separationTime = DateTime.Now;
        }
        public bool IsSeparationTimeReached()
        {
            var elapsed = DateTime.Now - separationTime;

            return elapsed.TotalSeconds >= config.AtmNavigationSeparationSecs;
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
            ExchangeTask = (ExchangeTasks)Utils.ReadInt(parts, "ExchangeTask");
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
                $"ExchangeTask={(int)ExchangeTask}"
            };

            return string.Join("¬", parts);
        }
    }
}
