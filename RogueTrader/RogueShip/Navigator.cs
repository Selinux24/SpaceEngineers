using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Navigator
    {
        readonly Program ship;
        int tickCount = 0;
        bool monitorize = false;
        Vector3D monitorizePosition;
        double monitorizeDistance;

        DateTime alignThrustStart = DateTime.Now;
        bool thrusting = false;
        MyDetectedEntityInfo lastHit;
        readonly List<Vector3D> evadingPoints = new List<Vector3D>();

        public bool Landing = false;
        public Vector3D Parking;
        public string Exchange = null;
        public Vector3D Forward = new Vector3D(1, 0, 0);
        public Vector3D Up = new Vector3D(0, 1, 0);
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentWp = 0;
        public string Callback = null;
        public ExchangeTasks ExchangeTask = ExchangeTasks.None;

        public NavigatorTasks Task = NavigatorTasks.None;
        public NavigatorAtmStatus AtmStatus = NavigatorAtmStatus.None;
        public NavigatorCrsStatus CrsStatus = NavigatorCrsStatus.None;

        public Config Config => ship.Config;

        public Vector3D CurrentPos => ship.GetDockingPosition();
        public Vector3D TargetWaypoint => Waypoints[CurrentWp];
        public Vector3D ToTargetWaypoint => TargetWaypoint - CurrentPos;
        public double DistanceToNextWaypoint => ToTargetWaypoint.Length();

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public TimeSpan EstimatedArrival => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double TotalDistance => GetTotalDistance();
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;

        public Navigator(Program ship)
        {
            this.ship = ship;
        }

        public void ApproachToDock(Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onAproximationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Parking = parking;
            Exchange = exchange;
            Forward = -Vector3D.Normalize(fw);
            Up = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentWp = 0;
            Callback = onAproximationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Approach;

            //Program the remote pilot control to navigate to the parking position.
            ship.ConfigureRemotePilot(parking, "Parking position", Config.TaxiSpeed, true);

            //Monitorize the proximity to the parking position.
            monitorize = true;
            monitorizePosition = parking;
            monitorizeDistance = Config.DockingDistanceThrWaypoints;
        }
        public void SeparateFromDock(Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onSeparationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Parking = parking;
            Exchange = exchange;
            Forward = -Vector3D.Normalize(fw);
            Up = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentWp = 0;
            Callback = onSeparationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Separate;

            //Start the undocking process.
            ship.Undock();
        }
        public void NavigateTo(bool landing, List<Vector3D> wpList, string onNavigationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Landing = landing;

            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentWp = 0;

            Callback = onNavigationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Navigate;
        }
        public void Clear()
        {
            Landing = false;

            Exchange = null;
            Forward = Vector3D.Zero;
            Up = Vector3D.Zero;
            Waypoints.Clear();
            CurrentWp = 0;
            Callback = null;
            ExchangeTask = ExchangeTasks.None;

            Task = NavigatorTasks.None;
            AtmStatus = NavigatorAtmStatus.None;
            CrsStatus = NavigatorCrsStatus.None;

            monitorize = false;
            monitorizePosition = Vector3D.Zero;
            monitorizeDistance = 0;
        }

        public void Update()
        {
            if (Task == NavigatorTasks.None)
            {
                return;
            }

            if (!Tick())
            {
                return;
            }

            if (Task == NavigatorTasks.Approach) { MonitorizeApproach(); }
            else if (Task == NavigatorTasks.Separate) { MonitorizeSeparate(); }
            else if (Task == NavigatorTasks.Navigate) { MonitorizeNavigate(); }
        }
        bool Tick()
        {
            if (++tickCount < Config.NavigationTicks)
            {
                return false;
            }
            tickCount = 0;
            return true;
        }

        void MonitorizeApproach()
        {
            if (ship.IsConnected())
            {
                //If the remote control is connected, we are not in a docking process.
                return;
            }

            //Monitorize approach to the parking position.
            if (monitorize)
            {
                var distance = Vector3D.Distance(CurrentPos, monitorizePosition);
                if (distance > monitorizeDistance)
                {
                    return;
                }

                //Reached
                ship.DisableRemotePilot();
                monitorize = false;
            }

            //Monitorize last waypoint.
            if (CurrentWp >= Waypoints.Count)
            {
                ship.ExecuteCallback(Callback, ExchangeTask);

                Clear();
                ship.ResetGyros();
                ship.ResetThrust();

                return;
            }

            bool corrected = ship.AlignToVectors(Forward, Up, Config.GyrosThr);
            if (corrected)
            {
                //Wait until aligned
                ship.ResetThrust();
                return;
            }

            Taxi();
        }
        void MonitorizeSeparate()
        {
            if (ship.IsConnected())
            {
                //If the remote control is connected, we are not in a undocking process.
                return;
            }

            //Monitorize approach to the parking position.
            if (monitorize)
            {
                var distance = Vector3D.Distance(CurrentPos, monitorizePosition);
                if (distance > monitorizeDistance)
                {
                    return;
                }

                //Reached
                ship.DisableRemotePilot();
                monitorize = false;

                ship.ExecuteCallback(Callback, ExchangeTask);

                Clear();
                ship.ResetGyros();
                ship.ResetThrust();
            }

            //Monitorize last waypoint.
            if (CurrentWp >= Waypoints.Count)
            {
                //Program the remote pilot to navigate from the last waypoint to the parking position.
                ship.ConfigureRemotePilot(Parking, "Parking position", Config.TaxiSpeed, true);

                //Monitorize the proximity to the parking position.
                monitorize = true;
                monitorizePosition = Parking;
                monitorizeDistance = Config.DockingDistanceThrWaypoints;

                ship.ResetGyros();
                ship.ResetThrust();

                return;
            }

            bool corrected = ship.AlignToVectors(Forward, Up, Config.GyrosThr);
            if (corrected)
            {
                //Wait until aligned
                ship.ResetThrust();
                return;
            }

            Taxi();
        }
        void Taxi()
        {
            ship.WriteInfoLCDs(GetState());

            var distance = DistanceToNextWaypoint;
            if (distance < Config.DockingDistanceThrWaypoints)
            {
                CurrentWp++;
                ship.ResetThrust();
                return;
            }

            //Always take the data from the docking remote control.
            double desiredSpeed = CalculateDesiredSpeed(distance);
            var currentVelocity = ship.GetDockingLinearVelocity();
            var currentGravity = ship.GetDockingNaturalGravity();
            double mass = ship.GetDockingPhysicalMass();

            var neededForce = Utils.CalculateThrustForce(ToTargetWaypoint, desiredSpeed, currentVelocity, mass);

            ship.ApplyThrust(neededForce, currentGravity, mass);
        }

        void MonitorizeNavigate()
        {
            if (Waypoints.Count == 0 || CurrentWp >= Waypoints.Count)
            {
                return;
            }

            var position = ship.GetPosition();
            var toTarget = TargetWaypoint - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = GetRemainingDistance(position);
            Speed = Landing ? ship.GetLandingSpeed() : ship.GetPilotSpeed();

            if (toTarget.Length() <= 1000)
            {
                CurrentWp++;
            }

            //Determine if the ship is in gravity.
            var inGravity = ship.GetPilotNaturalGravity().Length() > 0.001;

            if (inGravity)
            {
                //If the ship is in gravity, do the trip in atmospheric mode.
                switch (AtmStatus)
                {
                    case NavigatorAtmStatus.Accelerating:
                        AtmNavigationAccelerate();
                        break;
                    case NavigatorAtmStatus.Decelerating:
                        AtmNavigationDecelerate();
                        break;
                }
            }
            else
            {
                //If the ship is not in gravity, do the trip in cruise mode.
                switch (CrsStatus)
                {
                    case NavigatorCrsStatus.Locating:
                        CrsNavigationLocate();
                        break;
                    case NavigatorCrsStatus.Accelerating:
                        CrsNavigationAccelerate();
                        break;
                    case NavigatorCrsStatus.Cruising:
                        CrsNavigationCruise();
                        break;
                    case NavigatorCrsStatus.Decelerating:
                        CrsNavigationDecelerate();
                        break;

                    case NavigatorCrsStatus.Avoiding:
                        CrsNavigationAvoid();
                        break;
                }
            }
        }

        void AtmNavigationAccelerate()
        {
            ship.AlignToDirection(Landing, DirectionToTarget, Config.AtmNavigationAlignThr);

            if (DistanceToTarget < Config.AtmNavigationDistanceThr)
            {
                AtmStatus = NavigatorAtmStatus.Decelerating;

                return;
            }

            //Accelerate
            ship.WriteInfoLCDs(GetState());

            ship.ThrustToTarget(Landing, DirectionToTarget, Config.AtmNavigationMaxSpeed);
        }
        void AtmNavigationDecelerate()
        {
            ship.ResetThrust();
            ship.ResetGyros();

            var shipVelocity = ship.GetSpeed(Landing);
            if (shipVelocity <= 0.1)
            {
                //Reached the waypoint.
                ship.ExecuteCallback(Callback, ExchangeTask);
                Clear();
            }
        }

        void CrsNavigationLocate()
        {
            if (!ship.AlignToDirection(false, DirectionToTarget, Config.CrsNavigationAlignThr))
            {
                CrsStatus = NavigatorCrsStatus.Accelerating;

                return;
            }

            ship.ResetThrust();
        }
        void CrsNavigationAccelerate()
        {
            if (ship.IsObstacleAhead(Config.CrsNavigationCollisionDetectRange, ship.GetPilotLinearVelocity(), out lastHit))
            {
                CrsStatus = NavigatorCrsStatus.Avoiding;

                return;
            }

            if (DistanceToTarget < Config.CrsNavigationWaypointDistanceThr)
            {
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            bool inGravity = ship.GetPilotNaturalGravity().Length() > 0.001;
            var shipVelocity = ship.GetPilotSpeed();
            if (!inGravity && shipVelocity >= Config.CrsNavigationMaxCruiseSpeed * Config.CrsNavigationMaxSpeedThr)
            {
                CrsStatus = NavigatorCrsStatus.Cruising;

                return;
            }

            //Accelerate
            var maxSpeed = Config.CrsNavigationMaxCruiseSpeed;
            if (DistanceToTarget <= Config.CrsNavigationDestinationDistanceThr)
            {
                maxSpeed = Config.CrsNavigationMaxAccelerationSpeed;
            }
            ship.ThrustToTarget(false, DirectionToTarget, maxSpeed);
        }
        void CrsNavigationCruise()
        {
            if (ship.IsObstacleAhead(Config.CrsNavigationCollisionDetectRange, ship.GetPilotLinearVelocity(), out lastHit))
            {
                CrsStatus = NavigatorCrsStatus.Avoiding;

                return;
            }

            if (DistanceToTarget < Config.CrsNavigationWaypointDistanceThr)
            {
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            //Maintain speed
            ship.WriteInfoLCDs(GetState());

            bool inGravity = ship.GetPilotNaturalGravity().Length() > 0.001;
            if (inGravity || ship.AlignToDirection(false, DirectionToTarget, Config.CrsNavigationAlignThr))
            {
                ship.WriteInfoLCDs("Not aligned");

                //Thrust until the velocity vector is aligned again with the vector to the target
                ship.ThrustToTarget(false, DirectionToTarget, Config.CrsNavigationMaxCruiseSpeed);
                alignThrustStart = DateTime.Now;
                thrusting = true;

                return;
            }

            if (thrusting)
            {
                ship.WriteInfoLCDs("Thrusters started to regain alignment");

                //Thrusters started to regain alignment
                if (!inGravity && (DateTime.Now - alignThrustStart).TotalSeconds > Config.CrsNavigationAlignSeconds)
                {
                    //Out of gravity and alignment time consumed. Deactivate thrusters.
                    CrsNavigationEnterCruise();
                    thrusting = false;
                }

                return;
            }

            var shipVelocity = ship.GetPilotSpeed();
            if (shipVelocity > Config.CrsNavigationMaxCruiseSpeed)
            {
                ship.WriteInfoLCDs("Maximum speed exceeded");

                //Maximum speed exceeded. Engage thrusters in neutral to brake.
                ship.ResetThrust();
                ship.ResetGyros();

                return;
            }

            if (shipVelocity < Config.CrsNavigationMaxCruiseSpeed * Config.CrsNavigationMaxSpeedThr)
            {
                ship.WriteInfoLCDs("Below the desired speed");

                //Below the desired speed. Accelerate until reaching it.
                ship.ThrustToTarget(false, DirectionToTarget, Config.CrsNavigationMaxCruiseSpeed);

                return;
            }

            CrsNavigationEnterCruise();
        }
        void CrsNavigationDecelerate()
        {
            ship.ResetThrust();
            ship.ResetGyros();
            ship.EnableSystems();

            var shipVelocity = ship.GetPilotSpeed();
            if (shipVelocity <= 0.1)
            {
                ship.ExecuteCallback(Callback, ExchangeTask);
                Clear();
            }
        }
        void CrsNavigationAvoid()
        {
            ship.WriteInfoLCDs(PrintObstacle());

            if (DistanceToTarget < Config.CrsNavigationWaypointDistanceThr)
            {
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            //Calculate evading points
            if (!CalculateEvadingWaypoints(Config.CrsNavigationCollisionDetectRange * 0.5))
            {
                //Cannot calculate evading point
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            //Navigate between evading points
            if (evadingPoints.Count > 0)
            {
                CrsNavigationEvadingTo(evadingPoints[0], Config.CrsNavigationMaxEvadingSpeed);

                if (evadingPoints.Count == 0)
                {
                    //Clear obstacle information
                    lastHit = new MyDetectedEntityInfo();

                    ship.ResetThrust();

                    //Return to navigation when the last navigation point is reached
                    CrsStatus = NavigatorCrsStatus.Locating;
                }

                return;
            }
        }
        void CrsNavigationEvadingTo(Vector3D wayPoint, double maxSpeed)
        {
            var toTarget = wayPoint - ship.GetPosition();
            var d = toTarget.Length();
            if (d <= Config.CrsNavigationEvadingWaypointDistance)
            {
                //Waypoint reached
                evadingPoints.RemoveAt(0);

                return;
            }

            ship.WriteInfoLCDs($"Following evading route...");
            ship.WriteInfoLCDs($"Distance to waypoint {Utils.DistanceToStr(d)}");

            ship.ThrustToTarget(false, Vector3D.Normalize(toTarget), maxSpeed);
        }
        void CrsNavigationEnterCruise()
        {
            ship.DisableSystems();
            ship.ResetThrust();
            ship.StopThrust();
            ship.ResetGyros();
        }

        double GetTotalDistance()
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
        double GetRemainingDistance(Vector3D position)
        {
            if (Waypoints.Count < 2)
            {
                return 0;
            }

            double d = 0;
            for (int i = CurrentWp; i < Waypoints.Count; i++)
            {
                var p = i == CurrentWp ? position : Waypoints[i - 1];
                d += Vector3D.Distance(p, Waypoints[i]);
            }
            return d;
        }

        bool CalculateEvadingWaypoints(double safetyDistance)
        {
            if (evadingPoints.Count > 0)
            {
                //Evading points have already been calculated
                return true;
            }

            if (lastHit.IsEmpty())
            {
                return false;
            }

            var camera = ship.GetCameraPilot();
            var obstacleCenter = lastHit.Position;
            var obstacleSize = Math.Max(lastHit.BoundingBox.Extents.X, Math.Max(lastHit.BoundingBox.Extents.Y, lastHit.BoundingBox.Extents.Z));

            //Point on the obstacle from the ship's point of view
            var p1 = obstacleCenter + (camera.WorldMatrix.Up * obstacleSize);
            evadingPoints.Add(p1);

            //Point on the other side of the obstacle from the ship's point of view
            var p2 = obstacleCenter + (camera.WorldMatrix.Forward * (obstacleSize + safetyDistance));
            evadingPoints.Add(p2);

            return true;
        }
        double CalculateDesiredSpeed(double distance)
        {
            //Calculates desired speed based on distance, when we are moving towards the last waypoint.
            double approachSpeed;
            if (CurrentWp == 0) approachSpeed = Config.DockingSpeedWaypointFirst; //Speed ​​to the first approach point.
            else if (CurrentWp == Waypoints.Count - 1) approachSpeed = Config.DockingSpeedWaypointLast; //Speed ​​from the last approach point.
            else approachSpeed = Config.DockingSpeedWaypoints; //Speed ​​between approach points.

            double desiredSpeed = approachSpeed;
            if (distance < Config.DockingSlowdownDistance && (CurrentWp == 0 || CurrentWp == Waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / Config.DockingSlowdownDistance * approachSpeed, 0.5);
            }

            return desiredSpeed;
        }

        string GetState()
        {
            if (Task == NavigatorTasks.Navigate)
            {
                return
                    $"Trip: {Utils.DistanceToStr(TotalDistance)}" + Environment.NewLine +
                    $"To target: {Utils.DistanceToStr(DistanceToTarget)}" + Environment.NewLine +
                    $"Speed: {Speed:F2}" + Environment.NewLine +
                    $"ETC: {EstimatedArrival:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                    $"Progress {Progress:P1}" + Environment.NewLine;
            }
            else
            {
                return
                    $"To target: {Utils.DistanceToStr(DistanceToNextWaypoint)}" + Environment.NewLine +
                    $"Progress: {CurrentWp + 1}/{Waypoints.Count}." + Environment.NewLine;
            }
        }
        string PrintObstacle()
        {
            if (!lastHit.IsEmpty())
            {
                return $"Obstacle detected. {lastHit.Name} - Type {lastHit.Type}";
            }

            return "";
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            Landing = Utils.ReadInt(parts, "Landing") == 1;

            Parking = Utils.ReadVector(parts, "Parking");

            Exchange = Utils.ReadString(parts, "Exchange");
            Forward = Utils.ReadVector(parts, "Forward");
            Up = Utils.ReadVector(parts, "Up");
            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(parts, "Waypoints"));
            CurrentWp = Utils.ReadInt(parts, "CurrentWp");

            Callback = Utils.ReadString(parts, "Callback");
            ExchangeTask = (ExchangeTasks)Utils.ReadInt(parts, "ExchangeTask");

            Task = (NavigatorTasks)Utils.ReadInt(parts, "Task");
            AtmStatus = (NavigatorAtmStatus)Utils.ReadInt(parts, "AtmStatus");
            CrsStatus = (NavigatorCrsStatus)Utils.ReadInt(parts, "CrsStatus");

            monitorize = Utils.ReadInt(parts, "Monitorize") == 1;
            monitorizePosition = Utils.ReadVector(parts, "MonitorizePosition");
            monitorizeDistance = Utils.ReadDouble(parts, "MonitorizeDistance");

            thrusting = Utils.ReadInt(parts, "Thrusting") == 1;
            evadingPoints.Clear();
            evadingPoints.AddRange(Utils.ReadVectorList(parts, "EvadingPoints"));
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"Landing={(Landing ? 1 : 0)}",

                $"Parking={Utils.VectorToStr(Parking)}",

                $"Exchange={Exchange}",
                $"Forward={Utils.VectorToStr(Forward)}",
                $"Up={Utils.VectorToStr(Up)}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"CurrentWp={CurrentWp}",

                $"Callback={Callback}",
                $"ExchangeTask={(int)ExchangeTask}",

                $"Task={(int)Task}",
                $"AtmStatus={(int)AtmStatus}",
                $"CrsStatus={(int)CrsStatus}",

                $"Monitorize={(monitorize ? 1 : 0)}",
                $"MonitorizePosition={Utils.VectorToStr(monitorizePosition)}",
                $"MonitorizeDistance={monitorizeDistance:F2}",

                $"Thrusting={(thrusting?1:0)}",
                $"EvadingPoints={Utils.VectorListToStr(evadingPoints)}",
            };

            return string.Join("¬", parts);
        }
    }
}
