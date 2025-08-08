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

        public bool InGravity = false;
        public Vector3D Parking;
        public string Exchange = null;
        public Vector3D TargetForward = new Vector3D(1, 0, 0);
        public Vector3D TargetUp = new Vector3D(0, 1, 0);
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public int CurrentTarget = 0;
        public string Callback = null;
        public ExchangeTasks ExchangeTask = ExchangeTasks.None;
        public NavigatorTasks Task = NavigatorTasks.None;

        public Config Config => ship.Config;
        public Vector3D CurrentPos => ship.GetPosition();
        public Vector3D TargetPos => Waypoints[CurrentTarget];
        public Vector3D ToTarget => TargetPos - CurrentPos;
        public double Distance => ToTarget.Length();

        public Navigator(Program ship)
        {
            this.ship = ship;
        }

        public void ApproachToDock(bool inGravity, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onAproximationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            InGravity = inGravity;
            Parking = parking;
            Exchange = exchange;
            TargetForward = -Vector3D.Normalize(fw);
            TargetUp = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentTarget = 0;
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
        public void SeparateFromDock(bool inGravity, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, string onSeparationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            InGravity = inGravity;
            Parking = parking;
            Exchange = exchange;
            TargetForward = -Vector3D.Normalize(fw);
            TargetUp = Vector3D.Normalize(up);
            Waypoints.Clear();
            Waypoints.AddRange(wpList);
            CurrentTarget = 0;
            Callback = onSeparationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Separate;

            //Start the undocking process.
            ship.Undock();
        }
        public void NavigateTo(List<Vector3D> waypoints, string onNavigationCompleted = null, ExchangeTasks exchangeTask = ExchangeTasks.None)
        {
            Callback = onNavigationCompleted;
            ExchangeTask = exchangeTask;

            Task = NavigatorTasks.Navigate;
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
            ExchangeTask = ExchangeTasks.None;

            Task = NavigatorTasks.None;

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
            if (CurrentTarget >= Waypoints.Count)
            {
                ship.ExecuteCallback(Callback, ExchangeTask);

                Clear();
                ship.ResetGyros();
                ship.ResetThrust();

                return;
            }

            bool corrected = ship.AlignToVectors(TargetForward, TargetUp, Config.GyrosThr);
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
            if (CurrentTarget >= Waypoints.Count)
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

            bool corrected = ship.AlignToVectors(TargetForward, TargetUp, Config.GyrosThr);
            if (corrected)
            {
                //Wait until aligned
                ship.ResetThrust();
                return;
            }

            Taxi();
        }
        void MonitorizeNavigate()
        {

        }
        void Taxi()
        {
            ship.WriteInfoLCDs(GetState());

            var distance = Distance;
            if (distance < Config.DockingDistanceThrWaypoints)
            {
                CurrentTarget++;
                ship.ResetThrust();
                return;
            }

            double desiredSpeed = CalculateDesiredSpeed(distance);
            var currentVelocity = ship.GetLinearVelocity();
            double mass = ship.GetPhysicalMass();
            var neededForce = Utils.CalculateThrustForce(ToTarget, desiredSpeed, currentVelocity, mass);

            ship.ApplyThrust(neededForce);
        }
        double CalculateDesiredSpeed(double distance)
        {
            //Calculates desired speed based on distance, when we are moving towards the last waypoint.
            double approachSpeed;
            if (CurrentTarget == 0) approachSpeed = Config.DockingSpeedWaypointFirst; //Speed ​​to the first approach point.
            else if (CurrentTarget == Waypoints.Count - 1) approachSpeed = Config.DockingSpeedWaypointLast; //Speed ​​from the last approach point.
            else approachSpeed = Config.DockingSpeedWaypoints; //Speed ​​between approach points.

            double desiredSpeed = approachSpeed;
            if (distance < Config.DockingSlowdownDistance && (CurrentTarget == 0 || CurrentTarget == Waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / Config.DockingSlowdownDistance * approachSpeed, 0.5);
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
            Parking = Utils.ReadVector(parts, "Parking");
            Exchange = Utils.ReadString(parts, "Exchange");
            TargetForward = Utils.ReadVector(parts, "TargetForward");
            TargetUp = Utils.ReadVector(parts, "TargetUp");
            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(parts, "Waypoints"));
            CurrentTarget = Utils.ReadInt(parts, "CurrentTarget");
            Callback = Utils.ReadString(parts, "Callback");
            ExchangeTask = (ExchangeTasks)Utils.ReadInt(parts, "ExchangeTask");

            Task = (NavigatorTasks)Utils.ReadInt(parts, "Task");

            monitorize = Utils.ReadInt(parts, "Monitorize") == 1;
            monitorizePosition = Utils.ReadVector(parts, "MonitorizePosition");
            monitorizeDistance = Utils.ReadDouble(parts, "MonitorizeDistance");
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"InGravity={(InGravity ? 1 : 0)}",
                $"Parking={Utils.VectorToStr(Parking)}",
                $"Exchange={Exchange}",
                $"TargetForward={Utils.VectorToStr(TargetForward)}",
                $"TargetUp={Utils.VectorToStr(TargetUp)}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"CurrentTarget={CurrentTarget}",
                $"Callback={Callback}",
                $"ExchangeTask={(int)ExchangeTask}",

                $"Task={(int)Task}",

                $"Monitorize={(monitorize ? 1 : 0)}",
                $"MonitorizePosition={Utils.VectorToStr(monitorizePosition)}",
                $"MonitorizeDistance={monitorizeDistance:F2}",
            };

            return string.Join("¬", parts);
        }
    }
}
