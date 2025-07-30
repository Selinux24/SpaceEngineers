using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Navigation data for ships
    /// </summary>
    class NavigationData
    {
        readonly Config config;
        int tickCount = 0;
        DateTime separationTime = DateTime.MinValue;

        public bool Landing = false;
        public NavigationStatus CurrentState = NavigationStatus.Idle;
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentWaypointIndex = 0;
        public readonly ExchangeInfo Exchange = new ExchangeInfo();
        public ExchangeTasks ExchangeTask = ExchangeTasks.None;
        public string Command = null;
        public bool HasTarget = false;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public TimeSpan EstimatedArrival => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double TotalDistance => GetTotalDistance();
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;
        public TimeSpan SeparationSecs => TimeSpan.FromSeconds(config.AtmNavigationSeparationSecs);

        public NavigationData(Config config)
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

        public void Initialize(bool landing, List<Vector3D> waypoints, string commad)
        {
            CurrentState = NavigationStatus.Undocking;
            Landing = landing;
            Waypoints.Clear();
            Waypoints.AddRange(waypoints);
            Command = commad;
            HasTarget = true;
        }
        public void Clear()
        {
            CurrentState = NavigationStatus.Idle;
            Landing = false;
            Waypoints.Clear();
            Command = null;
            HasTarget = false;

            ClearExchange();
        }

        public void SetExchange(ExchangeInfo info, ExchangeTasks task)
        {
            Exchange.Initialize(info);
            ExchangeTask = task;
        }
        public void ClearExchange()
        {
            Exchange.Clear();
            ExchangeTask = ExchangeTasks.None;
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

        public void StartSeparation()
        {
            separationTime = DateTime.Now;
        }
        public bool IsSeparationTimeReached()
        {
            var elapsed = DateTime.Now - separationTime;

            return elapsed.TotalSeconds >= config.AtmNavigationSeparationSecs;
        }

        public string GetTripState()
        {
            if (CurrentState == NavigationStatus.Accelerating ||
                CurrentState == NavigationStatus.Decelerating)
            {
                return
                    $"Trip: {Utils.DistanceToStr(TotalDistance)}" + Environment.NewLine +
                    $"To target: {Utils.DistanceToStr(DistanceToTarget)}" + Environment.NewLine +
                    $"Speed: {Speed:F2}" + Environment.NewLine +
                    $"ETC: {EstimatedArrival:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                    $"Progress {Progress:P1}" + Environment.NewLine;
            }

            if (CurrentState == NavigationStatus.Docking ||
                CurrentState == NavigationStatus.Undocking ||
                CurrentState == NavigationStatus.Separating)
            {
                return "Docking";
            }

            if (CurrentState == NavigationStatus.Exchanging)
            {
                return "Exchanging";
            }

            return "No trip in progress.";
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            CurrentState = (NavigationStatus)Utils.ReadInt(parts, "CurrentState");
            Landing = Utils.ReadInt(parts, "Landing") == 1;
            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(parts, "Waypoints"));
            Command = Utils.ReadString(parts, "Command");
            HasTarget = Utils.ReadInt(parts, "HasTarget") == 1;

            Exchange.Initialize(
                Utils.ReadString(parts, "ExchangeName"),
                Utils.ReadVector(parts, "ExchangeForward"),
                Utils.ReadVector(parts, "ExchangeUp"),
                Utils.ReadVectorList(parts, "ExchangeApproachingWaypoints"));

            ExchangeTask = (ExchangeTasks)Utils.ReadInt(parts, "ExchangeTask");
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"CurrentState={(int)CurrentState}",
                $"Landing={(Landing?1:0)}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"Command={Command}",
                $"HasTarget={(HasTarget?1:0)}",

                $"ExchangeName={Exchange.Exchange}",
                $"ExchangeForward={Utils.VectorToStr(Exchange.Forward)}",
                $"ExchangeUp={Utils.VectorToStr(Exchange.Up)}",
                $"ExchangeApproachingWaypoints={Utils.VectorListToStr(Exchange.ApproachingWaypoints)}",
                $"ExchangeTask={(int)ExchangeTask}"
            };

            return string.Join("¬", parts);
        }
    }
}
