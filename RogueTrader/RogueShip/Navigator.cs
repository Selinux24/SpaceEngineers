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
        public int CurrentWpIdx = 0;
        public string Callback = null;
        public ExchangeTasks ExchangeTask = ExchangeTasks.None;

        public NavigatorTasks Task = NavigatorTasks.None;
        public NavigatorAtmStatus AtmStatus = NavigatorAtmStatus.None;
        public NavigatorCrsStatus CrsStatus = NavigatorCrsStatus.None;

        public Config Config => ship.Config;

        public Vector3D DirectionToTarget { get; private set; }
        public double DistanceToTarget { get; private set; }
        public double Speed { get; private set; } = 0;
        public double TotalDistance { get; private set; } = 0;
        public Vector3D CurrentWaypoint => Waypoints[CurrentWpIdx];

        public double DockSpeed => ship.GetDockingSpeed();
        public TimeSpan DockingETA => DockSpeed > 0.01 ? TimeSpan.FromSeconds(DistanceToNextDockWaypoint / DockSpeed) : TimeSpan.Zero;
        public Vector3D ConnectorPosition => ship.GetDockingPosition();
        public Vector3D ToDockWaypoint => CurrentWaypoint - ConnectorPosition;
        public double DistanceToNextDockWaypoint => ToDockWaypoint.Length();

        public TimeSpan NavigationETA => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToTarget / Speed) : TimeSpan.Zero;
        public double Progress => DistanceToTarget > 0 ? 1 - (DistanceToTarget / TotalDistance) : 1;

        public Navigator(Program ship)
        {
            this.ship = ship;
        }

        public void ApproachToDock(bool landing, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onAproximationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            ship.WriteLogLCDs($"Approaching to dock {exchange} with {wpList.Count} waypoints.");

            Landing = landing;
            Parking = parking;

            Exchange = exchange;
            Forward = -Vector3D.Normalize(fw);
            Up = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentWpIdx = 0;
            Callback = onAproximationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Approach;
            AtmStatus = NavigatorAtmStatus.None;
            CrsStatus = NavigatorCrsStatus.None;

            TotalDistance = GetTotalDistance();
        }
        public void SeparateFromDock(bool landing, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onSeparationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Landing = landing;
            Parking = parking;

            Exchange = exchange;
            Forward = -Vector3D.Normalize(fw);
            Up = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentWpIdx = 0;
            Callback = onSeparationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Separate;
            AtmStatus = NavigatorAtmStatus.None;
            CrsStatus = NavigatorCrsStatus.None;

            TotalDistance = GetTotalDistance();

            //Start the undocking process.
            ship.Undock();
        }
        public void NavigateTo(bool landing, List<Vector3D> wpList, string onNavigationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Landing = landing;
            Parking = Vector3D.Zero;

            Exchange = null;
            Forward = Vector3D.Zero;
            Up = Vector3D.Zero;
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentWpIdx = 0;
            Callback = onNavigationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Navigate;
            AtmStatus = NavigatorAtmStatus.None;
            CrsStatus = NavigatorCrsStatus.None;

            TotalDistance = GetTotalDistance();
        }
        public void Clear()
        {
            Landing = false;
            Parking = Vector3D.Zero;

            Exchange = null;
            Forward = Vector3D.Zero;
            Up = Vector3D.Zero;
            Waypoints.Clear();
            CurrentWpIdx = 0;
            Callback = null;
            ExchangeTask = ExchangeTasks.None;

            Task = NavigatorTasks.None;
            AtmStatus = NavigatorAtmStatus.None;
            CrsStatus = NavigatorCrsStatus.None;
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

        #region Approach and Separation from Dock
        void MonitorizeApproach()
        {
            //Monitorize last waypoint.
            if (CurrentWpIdx >= Waypoints.Count)
            {
                if (ship.IsNearConnector())
                {
                    ship.ExecuteCallback(Callback, ExchangeTask);

                    Clear();
                }

                ship.ResetGyros();
                ship.ResetThrust();

                return;
            }

            if (ship.IsConnected())
            {
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
            //Monitorize last waypoint.
            if (CurrentWpIdx >= Waypoints.Count)
            {
                ship.ExecuteCallback(Callback, ExchangeTask);

                Clear();

                ship.ResetGyros();
                ship.ResetThrust();

                return;
            }

            if (ship.IsConnected())
            {
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

            var distance = DistanceToNextDockWaypoint;
            if (distance < Config.DockingDistanceThrWaypoints)
            {
                ship.WriteLogLCDs($"Next Wp {distance:F1} < {Config.DockingDistanceThrWaypoints}.");

                CurrentWpIdx++;
                ship.ResetThrust();
                return;
            }

            //Always take the data from the docking remote control.
            double desiredSpeed = CalculateDesiredSpeed(distance);
            var currentVelocity = ship.GetDockingLinearVelocity();
            double mass = ship.GetMass();
            var neededForce = Utils.CalculateThrustForce(ToDockWaypoint, desiredSpeed, currentVelocity, mass);
            ship.ApplyThrust(neededForce);
        }
        #endregion

        #region Navigation
        void MonitorizeNavigate()
        {
            if (Waypoints.Count == 0 || CurrentWpIdx >= Waypoints.Count)
            {
                return;
            }

            var position = ship.GetPosition();
            var toTarget = CurrentWaypoint - position;
            DirectionToTarget = Vector3D.Normalize(toTarget);
            DistanceToTarget = GetRemainingDistance(position);
            Speed = Landing ? ship.GetLandingSpeed() : ship.GetPilotSpeed();

            if (toTarget.Length() <= 1000)
            {
                CurrentWpIdx++;
            }

            //Determine if the ship is in gravity.
            var inGravity = ship.IsInGravity();
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

            bool inGravity = ship.IsInGravity();
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

            bool inGravity = ship.IsInGravity();
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
        #endregion

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
            for (int i = CurrentWpIdx; i < Waypoints.Count; i++)
            {
                var p = i == CurrentWpIdx ? position : Waypoints[i - 1];
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
            if (CurrentWpIdx == 0) approachSpeed = Config.DockingSpeedWaypointFirst; //Speed ​​to the first approach point.
            else if (CurrentWpIdx == Waypoints.Count - 1) approachSpeed = Config.DockingSpeedWaypointLast; //Speed ​​from the last approach point.
            else approachSpeed = Config.DockingSpeedWaypoints; //Speed ​​between approach points.

            double desiredSpeed = approachSpeed;
            if (distance < Config.DockingSlowdownDistance && (CurrentWpIdx == 0 || CurrentWpIdx == Waypoints.Count - 1))
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
                    $"ETC: {NavigationETA:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                    $"Progress {Progress:P1}" + Environment.NewLine;
            }
            else
            {
                return
                    $"Trip: {Utils.DistanceToStr(TotalDistance)}" + Environment.NewLine +
                    $"To target: {Utils.DistanceToStr(DistanceToNextDockWaypoint)}" + Environment.NewLine +
                    $"Speed: {DockSpeed:F2}" + Environment.NewLine +
                    $"ETC: {DockingETA:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                    $"Progress: {CurrentWpIdx + 1}/{Waypoints.Count}." + Environment.NewLine;
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
            CurrentWpIdx = Utils.ReadInt(parts, "CurrentWp");

            Callback = Utils.ReadString(parts, "Callback");
            ExchangeTask = (ExchangeTasks)Utils.ReadInt(parts, "ExchangeTask");

            Task = (NavigatorTasks)Utils.ReadInt(parts, "Task");
            AtmStatus = (NavigatorAtmStatus)Utils.ReadInt(parts, "AtmStatus");
            CrsStatus = (NavigatorCrsStatus)Utils.ReadInt(parts, "CrsStatus");

            thrusting = Utils.ReadInt(parts, "Thrusting") == 1;
            evadingPoints.Clear();
            evadingPoints.AddRange(Utils.ReadVectorList(parts, "EvadingPoints"));

            TotalDistance = GetTotalDistance();
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
                $"CurrentWp={CurrentWpIdx}",

                $"Callback={Callback}",
                $"ExchangeTask={(int)ExchangeTask}",

                $"Task={(int)Task}",
                $"AtmStatus={(int)AtmStatus}",
                $"CrsStatus={(int)CrsStatus}",

                $"Thrusting={(thrusting?1:0)}",
                $"EvadingPoints={Utils.VectorListToStr(evadingPoints)}",
            };

            return string.Join("¬", parts);
        }
    }
}
