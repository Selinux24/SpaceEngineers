using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
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

        public double Speed => ship.GetSpeed();
        public Vector3D CurrentWaypoint => GetCurrentWaypoint();
        public Vector3D ToWaypoint => CurrentWaypoint - ship.GetPosition();
        public double DistanceToNextWaypoint => ToWaypoint.Length();
        public Vector3D DirectionToWaypoint => Vector3D.Normalize(ToWaypoint);
        public double DistanceToDestination => GetRemainingDistance();
        public double TotalDistance => GetTotalDistance();

        public TimeSpan DockingETA => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToNextDockWaypoint / Speed) : TimeSpan.Zero;
        public Vector3D ConnectorPosition => ship.GetDockingPosition();
        public Vector3D ToDockWaypoint => CurrentWaypoint - ConnectorPosition;
        public double DistanceToNextDockWaypoint => ToDockWaypoint.Length();

        public TimeSpan NavigationETA => Speed > 0.01 ? TimeSpan.FromSeconds(DistanceToDestination / Speed) : TimeSpan.Zero;
        public double Progress => DistanceToDestination > 0 ? 1 - (DistanceToDestination / TotalDistance) : 1;

        public Navigator(Program ship)
        {
            this.ship = ship;
        }

        public void ApproachToDock(bool landing, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onAproximationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            ship.WriteLogLCDs($"Approaching to dock {exchange} with {wpList.Count} waypoints.");

            Landing = landing;

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

            ship.Pilot();
        }
        public void SeparateFromDock(bool landing, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onSeparationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Landing = landing;

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

            //Start the undocking process.
            ship.Pilot();
            ship.Undock();
        }
        public void NavigateTo(bool landing, List<Vector3D> wpList, string onNavigationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Landing = landing;

            Exchange = null;
            Forward = Vector3D.Zero;
            Up = Vector3D.Zero;
            Waypoints.Clear();
            Waypoints.Add(ship.GetPosition());
            Waypoints.AddRange(wpList);
            CurrentWpIdx = 0;
            Callback = onNavigationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Navigate;
            AtmStatus = NavigatorAtmStatus.Starting;
            CrsStatus = NavigatorCrsStatus.Starting;

            ship.Pilot();
            ship.WriteLogLCDs($"Navigating to {wpList.Count} waypoints. Landing: {landing}. {exchangeTask}");
        }
        public void Clear()
        {
            Landing = false;

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
            if (Task == NavigatorTasks.None) return;

            if (!Tick()) return;

            if (Task == NavigatorTasks.Approach) { MonitorizeApproach(); }
            else if (Task == NavigatorTasks.Separate) { MonitorizeSeparate(); }
            else if (Task == NavigatorTasks.Navigate) { MonitorizeNavigate(); }
        }
        bool Tick()
        {
            if (++tickCount < Config.NavigationTicks) return false;

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
                    var callBack = Callback;
                    var exchangeTask = ExchangeTask;
                    Clear();
                    ship.ExecuteCallback(callBack, exchangeTask);
                }

                ship.ResetGyros();
                ship.ResetThrust();

                return;
            }

            if (ship.IsConnected())
            {
                return;
            }

            bool corrected = AlignToVectors(Forward, Up, Config.GyrosThr);
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
                ship.ResetGyros();
                ship.ResetThrust();

                var callBack = Callback;
                var exchangeTask = ExchangeTask;
                Clear();
                ship.ExecuteCallback(callBack, exchangeTask);

                return;
            }

            if (ship.IsConnected())
            {
                return;
            }

            bool corrected = AlignToVectors(Forward, Up, Config.GyrosThr);
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
            double desiredSpeed = CalculateDesiredSpeed(Task == NavigatorTasks.Approach, distance);
            var currentVelocity = ship.GetDockingLinearVelocity();
            double mass = ship.GetMass();
            var neededForce = Utils.CalculateThrustForce(ToDockWaypoint, desiredSpeed, currentVelocity, mass);
            ship.ApplyThrust(neededForce);
        }
        #endregion

        #region Navigation
        void MonitorizeNavigate()
        {
            if (Waypoints.Count == 0)
            {
                ship.WriteLogLCDs("No waypoints to navigate");
                return;
            }

            //Determine if the ship is in gravity.
            bool inGravity = ship.IsInAtmosphere();
            if (inGravity)
            {
                var preStatus = AtmStatus;

                //If the ship is in gravity, do the trip in atmospheric mode.
                switch (AtmStatus)
                {
                    case NavigatorAtmStatus.Starting:
                        AtmStatus = NavigatorAtmStatus.Accelerating;
                        break;
                    case NavigatorAtmStatus.Accelerating:
                        AtmNavigationAccelerate();
                        break;
                    case NavigatorAtmStatus.Decelerating:
                        AtmNavigationDecelerate();
                        break;
                    case NavigatorAtmStatus.Ending:
                        AtmNavigationEnding();
                        break;
                }

                if (preStatus != AtmStatus) ship.WriteLogLCDs($"Transition from {preStatus} to {AtmStatus}");
            }
            else
            {
                var preStatus = CrsStatus;

                //If the ship is not in gravity, do the trip in cruise mode.
                switch (CrsStatus)
                {
                    case NavigatorCrsStatus.Starting:
                        CrsStatus = NavigatorCrsStatus.Locating;
                        break;
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
                    case NavigatorCrsStatus.Ending:
                        CrsNavigationEnding();
                        break;
                }

                if (preStatus != CrsStatus) ship.WriteLogLCDs($"Transition from {preStatus} to {CrsStatus}");
            }
        }

        void AtmNavigationAccelerate()
        {
            AlignToDirection(Landing, DirectionToWaypoint, Config.AtmNavigationAlignThr);

            if (DistanceToDestination < Config.AtmNavigationDestinationThr)
            {
                //Destination reached.
                AtmStatus = NavigatorAtmStatus.Decelerating;

                return;
            }

            if (DistanceToNextWaypoint < Config.AtmNavigationWaypointThr)
            {
                //Waypoint reached.
                ship.WriteLogLCDs($"Next Waypoint: {CurrentWpIdx}/{Waypoints.Count}");
                CurrentWpIdx++;

                return;
            }

            //Accelerate
            ship.WriteInfoLCDs(GetState());

            ThrustToTarget(Landing, DirectionToWaypoint, Config.AtmNavigationMaxSpeed);
        }
        void AtmNavigationDecelerate()
        {
            ship.ResetThrust();
            ship.ResetGyros();
            ship.EnableSystems();

            var speed = ship.GetSpeed();
            if (speed <= 0.1)
            {
                AtmStatus = NavigatorAtmStatus.Ending;
            }
        }
        void AtmNavigationEnding()
        {
            //Reached the waypoint.
            ship.ExecuteCallback(Callback, ExchangeTask);
            Clear();

            AtmStatus = NavigatorAtmStatus.None;
        }

        void CrsNavigationLocate()
        {
            if (DistanceToNextWaypoint < Config.CrsNavigationWaypointThr)
            {
                //Waypoint reached.
                ship.WriteLogLCDs($"Next Waypoint: {CurrentWpIdx}/{Waypoints.Count}");
                CurrentWpIdx++;

                return;
            }

            if (!AlignToDirection(false, DirectionToWaypoint, Config.CrsNavigationAlignThr))
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

            if (DistanceToDestination < Config.CrsNavigationDestinationThr)
            {
                //Destination reached.
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            if (DistanceToNextWaypoint < Config.CrsNavigationWaypointThr)
            {
                //Waypoint reached.
                ship.WriteLogLCDs($"Next Waypoint: {CurrentWpIdx}/{Waypoints.Count}");
                CurrentWpIdx++;

                return;
            }

            bool inGravity = ship.IsInAtmosphere();
            var speed = ship.GetSpeed();
            if (!inGravity && speed >= Config.CrsNavigationMaxCruiseSpeed * Config.CrsNavigationMaxSpeedThr)
            {
                CrsStatus = NavigatorCrsStatus.Cruising;

                return;
            }

            //Accelerate
            var maxSpeed = Config.CrsNavigationMaxCruiseSpeed;
            if (DistanceToDestination <= Config.CrsNavigationDestinationThr)
            {
                maxSpeed = Config.CrsNavigationMaxAccelerationSpeed;
            }
            ThrustToTarget(false, DirectionToWaypoint, maxSpeed);
        }
        void CrsNavigationCruise()
        {
            if (ship.IsObstacleAhead(Config.CrsNavigationCollisionDetectRange, ship.GetPilotLinearVelocity(), out lastHit))
            {
                CrsStatus = NavigatorCrsStatus.Avoiding;

                return;
            }

            if (DistanceToDestination < Config.CrsNavigationDestinationThr)
            {
                //Destination reached.
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            if (DistanceToNextWaypoint < Config.CrsNavigationWaypointThr)
            {
                //Waypoint reached.
                ship.WriteLogLCDs($"Next Waypoint: {CurrentWpIdx}/{Waypoints.Count}");
                CurrentWpIdx++;

                return;
            }

            //Maintain speed
            ship.WriteInfoLCDs(GetState());

            bool inGravity = ship.IsInAtmosphere();
            if (inGravity || AlignToDirection(false, DirectionToWaypoint, Config.CrsNavigationAlignThr))
            {
                ship.WriteInfoLCDs("Not aligned");

                //Thrust until the velocity vector is aligned again with the vector to the target
                ship.ResetThrust();
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

            var speed = ship.GetSpeed();
            if (speed > Config.CrsNavigationMaxCruiseSpeed)
            {
                ship.WriteInfoLCDs("Maximum speed exceeded");

                //Maximum speed exceeded. Engage thrusters in neutral to brake.
                ship.ResetThrust();
                ship.ResetGyros();

                return;
            }

            if (speed < Config.CrsNavigationMaxCruiseSpeed * Config.CrsNavigationMaxSpeedThr)
            {
                ship.WriteInfoLCDs("Below the desired speed");

                //Below the desired speed. Accelerate until reaching it.
                ThrustToTarget(false, DirectionToWaypoint, Config.CrsNavigationMaxCruiseSpeed);

                return;
            }

            CrsNavigationEnterCruise();
        }
        void CrsNavigationDecelerate()
        {
            ship.ResetThrust();
            ship.ResetGyros();
            ship.EnableSystems();

            var speed = ship.GetSpeed();
            if (speed <= 0.1)
            {
                CrsStatus = NavigatorCrsStatus.Ending;
            }
        }
        void CrsNavigationAvoid()
        {
            ship.WriteInfoLCDs(PrintObstacle());

            if (DistanceToDestination < Config.CrsNavigationDestinationThr)
            {
                //Destination reached.
                CrsStatus = NavigatorCrsStatus.Decelerating;

                return;
            }

            if (DistanceToNextWaypoint < Config.CrsNavigationWaypointThr)
            {
                //Waypoint reached.
                ship.WriteLogLCDs($"Next Waypoint: {CurrentWpIdx}/{Waypoints.Count}");
                CurrentWpIdx++;

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
            if (d <= Config.CrsNavigationEvadingWaypointThr)
            {
                //Waypoint reached
                evadingPoints.RemoveAt(0);

                return;
            }

            ship.WriteInfoLCDs($"Following evading route...");
            ship.WriteInfoLCDs($"Distance to waypoint {Utils.DistanceToStr(d)}");

            ThrustToTarget(false, Vector3D.Normalize(toTarget), maxSpeed);
        }
        void CrsNavigationEnding()
        {
            ship.ExecuteCallback(Callback, ExchangeTask);
            Clear();
            CrsStatus = NavigatorCrsStatus.None;
        }
        void CrsNavigationEnterCruise()
        {
            ship.DisableSystems();
            ship.ResetThrust();
            ship.StopThrust();
            ship.ResetGyros();
        }
        #endregion

        void ThrustToTarget(bool landing, Vector3D toTarget, double maxSpeed)
        {
            var velocity = landing ? ship.GetLandingLinearVelocity() : ship.GetPilotLinearVelocity();
            double mass = ship.GetMass();

            var force = Utils.CalculateThrustForce(toTarget, maxSpeed, velocity, mass);
            ship.ApplyThrust(force);
        }

        bool AlignToDirection(bool landing, Vector3D toTarget, double thr)
        {
            var direction = landing ? ship.GetLandingForwardDirection() : ship.GetPilotForwardDirection();

            double angle = Utils.AngleBetweenVectors(direction, toTarget);
            ship.WriteInfoLCDs($"TGT angle: {angle:F3}");

            if (angle <= thr)
            {
                ship.ResetGyros();
                ship.WriteInfoLCDs("Aligned.");
                return false;
            }
            ship.WriteInfoLCDs("Aligning...");

            var rotationAxis = Vector3D.Cross(direction, toTarget);
            if (rotationAxis.Length() <= 0.001) rotationAxis = new Vector3D(1, 0, 0);
            ship.ApplyGyroOverride(rotationAxis);

            return true;
        }
        bool AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipForward = ship.GetDockingForwardDirection();
            var shipUp = ship.GetDockingUpDirection();

            double angleFW = Utils.AngleBetweenVectors(shipForward, targetForward);
            double angleUP = Utils.AngleBetweenVectors(shipUp, targetUp);
            ship.WriteInfoLCDs($"TGT angles: {angleFW:F3} | {angleUP:F3}");

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

        Vector3D GetCurrentWaypoint()
        {
            if (Waypoints.Count == 0)
            {
                return ship.GetPosition();
            }

            return Waypoints[Math.Min(CurrentWpIdx, Waypoints.Count - 1)];
        }
        double GetTotalDistance()
        {
            if (Waypoints.Count < 2)
            {
                return GetRemainingDistance();
            }

            double d = 0;
            for (int i = 1; i < Waypoints.Count; i++)
            {
                d += Vector3D.Distance(Waypoints[i - 1], Waypoints[i]);
            }
            return d;
        }
        double GetRemainingDistance()
        {
            if (Waypoints.Count == 0)
            {
                return 0;
            }

            var position = ship.GetPosition();
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
        double CalculateDesiredSpeed(bool approaching, double distance)
        {
            double[] speeds = approaching ?
                new double[] { Config.DockingSpeedWaypointFirst, Config.DockingSpeedWaypoints, Config.DockingSpeedWaypointLast } :
                new double[] { Config.DockingSpeedWaypointLast, Config.DockingSpeedWaypoints, Config.DockingSpeedWaypointFirst };

            //Calculates desired speed based on distance, when we are moving towards the last waypoint.
            double speed;
            if (CurrentWpIdx == 0) speed = speeds[0]; //Speed ​​to the first approach point.
            else if (CurrentWpIdx == Waypoints.Count - 1) speed = speeds[2]; //Speed ​​from the last approach point.
            else speed = speeds[1]; //Speed ​​between approach points.

            if (distance < Config.DockingSlowdownDistance && (CurrentWpIdx == 0 || CurrentWpIdx == Waypoints.Count - 1))
            {
                speed = Math.Max(distance / Config.DockingSlowdownDistance * speed, 1.0);
            }

            return speed;
        }

        string GetState()
        {
            if (Task == NavigatorTasks.None)
            {
                return null;
            }
            else if (Task == NavigatorTasks.Navigate)
            {
                return
                    $"Trip: {Utils.DistanceToStr(TotalDistance)}" + Environment.NewLine +
                    $"To DST: {Utils.DistanceToStr(DistanceToDestination)}" + Environment.NewLine +
                    $"To TGT: {Utils.DistanceToStr(DistanceToNextWaypoint)}" + Environment.NewLine +
                    $"Speed: {Speed:F2}" + Environment.NewLine +
                    $"ETA: {NavigationETA:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                    $"Progress {Progress:P1}.  {CurrentWpIdx}/{Waypoints.Count}" + Environment.NewLine;
            }
            else
            {
                return
                    $"Trip: {Utils.DistanceToStr(TotalDistance)}" + Environment.NewLine +
                    $"To TGT: {Utils.DistanceToStr(DistanceToNextDockWaypoint)}" + Environment.NewLine +
                    $"Speed: {Speed:F2}" + Environment.NewLine +
                    $"ETA: {DockingETA:dd\\:hh\\:mm\\:ss}" + Environment.NewLine +
                    $"Progress: {CurrentWpIdx}/{Waypoints.Count}." + Environment.NewLine;
            }
        }
        public string GetShortState()
        {
            if (Task == NavigatorTasks.None)
            {
                return null;
            }
            else if (Task == NavigatorTasks.Navigate)
            {
                return $"{Task} - ETA: {NavigationETA:dd\\:hh\\:mm\\:ss}";
            }
            else
            {
                return $"{Task} - ETA: {DockingETA:dd\\:hh\\:mm\\:ss}";
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

        public string GetPlan(bool formatted)
        {
            if (Waypoints.Count <= 0) return "No waypoints";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Waypoints.Count; i++)
            {
                if (formatted)
                {
                    sb.AppendLine($"GPS:WP_{i + 1}:{Utils.VectorToStr(Waypoints[i])}:#FFAAE9B3:");
                }
                else
                {
                    sb.Append($"GPS:WP_{i + 1}:{Utils.VectorToStr(Waypoints[i])}:#FFAAE9B3:");
                    if (i < Waypoints.Count - 1) sb.Append(";");
                }
            }
            return sb.ToString();
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            Landing = Utils.ReadInt(parts, "Landing") == 1;

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
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"Landing={(Landing ? 1 : 0)}",

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
