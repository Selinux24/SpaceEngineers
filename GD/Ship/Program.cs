using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Ship script for delivery and distress signals.
    /// </summary>
    /// <remarks>
    /// TODO: When you get to a certain percentage of battery, stop and wait for it to recharge.
    /// TODO: When waiting to charge, when a certain battery percentage is reached, continue the trip
    /// </remarks>
    partial class Program : MyGridProgram
    {
        #region Blocks
        readonly IMyBroadcastListener bl;

        readonly IMyTimerBlock timerPilot;
        readonly IMyTimerBlock timerLock;
        readonly IMyTimerBlock timerUnlock;
        readonly IMyTimerBlock timerLoad;
        readonly IMyTimerBlock timerUnload;
        readonly IMyTimerBlock timerWaiting;

        readonly IMyRemoteControl remotePilot;
        readonly IMyCameraBlock cameraPilot;

        readonly IMyRemoteControl remoteAlign;
        readonly IMyShipConnector connectorA;

        readonly IMyRemoteControl remoteLanding;

        readonly IMyRadioAntenna antenna;
        readonly IMyBeacon beacon;

        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        readonly List<IMyTextPanel> infoLCDs = new List<IMyTextPanel>();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly List<IMyCargoContainer> shipCargos = new List<IMyCargoContainer>();
        #endregion

        readonly string shipId;
        readonly Config config;

        readonly StringBuilder sbLog = new StringBuilder();

        readonly DeliveryData deliveryData;
        readonly AlignData alignData;
        readonly ArrivalData arrivalData;
        readonly CruisingData cruisingData;
        readonly AtmNavigationData atmNavigationData;

        ShipStatus status = ShipStatus.Idle;
        bool paused = false;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData = Config.GetDefault();

                Echo("CustomData not set.");
                return;
            }

            shipId = Me.CubeGrid.CustomName;
            config = new Config(Me.CustomData);
            if (!config.IsValid())
            {
                Echo(config.GetErrors());
                return;
            }

            deliveryData = new DeliveryData();
            alignData = new AlignData(config);
            arrivalData = new ArrivalData(config);
            cruisingData = new CruisingData(config);
            atmNavigationData = new AtmNavigationData(config);

            timerPilot = GetBlockWithName<IMyTimerBlock>(config.ShipTimerPilot);
            if (timerPilot == null)
            {
                Echo($"Timer '{config.ShipTimerPilot}' not found.");
                return;
            }
            timerLock = GetBlockWithName<IMyTimerBlock>(config.ShipTimerLock);
            if (timerLock == null)
            {
                Echo($"Timer '{config.ShipTimerLock}' not found.");
                return;
            }
            timerUnlock = GetBlockWithName<IMyTimerBlock>(config.ShipTimerUnlock);
            if (timerUnlock == null)
            {
                Echo($"Timer '{config.ShipTimerUnlock}' not found.");
                return;
            }
            timerLoad = GetBlockWithName<IMyTimerBlock>(config.ShipTimerLoad);
            if (timerLoad == null)
            {
                Echo($"Timer '{config.ShipTimerLoad}' not found.");
                return;
            }
            timerUnload = GetBlockWithName<IMyTimerBlock>(config.ShipTimerUnload);
            if (timerUnload == null)
            {
                Echo($"Timer '{config.ShipTimerUnload}' not found.");
                return;
            }
            timerWaiting = GetBlockWithName<IMyTimerBlock>(config.ShipTimerWaiting);
            if (timerWaiting == null)
            {
                Echo($"Timer '{config.ShipTimerWaiting}' not found.");
                return;
            }

            remotePilot = GetBlockWithName<IMyRemoteControl>(config.ShipRemoteControlPilot);
            if (remotePilot == null)
            {
                Echo($"Remote Control '{config.ShipRemoteControlPilot}' not found.");
                return;
            }
            cameraPilot = GetBlockWithName<IMyCameraBlock>(config.ShipCameraPilot);
            if (cameraPilot == null)
            {
                Echo($"Camera {config.ShipCameraPilot} not found.");
                return;
            }

            remoteAlign = GetBlockWithName<IMyRemoteControl>(config.ShipRemoteControlAlign);
            if (remoteAlign == null)
            {
                Echo($"Remote Control '{config.ShipRemoteControlAlign}' not found.");
                return;
            }
            connectorA = GetBlockWithName<IMyShipConnector>(config.ShipConnectorA);
            if (connectorA == null)
            {
                Echo($"Connector '{config.ShipConnectorA}' not found.");
                return;
            }

            remoteLanding = GetBlockWithName<IMyRemoteControl>(config.ShipRemoteControlLanding);
            if (remoteLanding == null)
            {
                Echo($"Remote Control '{config.ShipRemoteControlLanding}' not found. This ship is not available for landing.");
            }

            antenna = GetBlockWithName<IMyRadioAntenna>(config.ShipAntennaName);
            if (antenna == null)
            {
                Echo($"Antenna {config.ShipAntennaName} not found.");
                return;
            }
            beacon = GetBlockWithName<IMyBeacon>(config.ShipBeaconName);
            if (beacon == null)
            {
                Echo($"Beacon {config.ShipBeaconName} not found.");
                return;
            }

            gyros = GetBlocksOfType<IMyGyro>();
            if (gyros.Count == 0)
            {
                Echo("Grid without gyroscopes.");
                return;
            }
            thrusters = GetBlocksOfType<IMyThrust>();
            if (thrusters.Count == 0)
            {
                Echo("Grid without thrusters.");
                return;
            }

            logLCDs = GetBlocksOfType<IMyTextPanel>(config.WildcardLogLCDs);
            infoLCDs = GetBlocksOfType<IMyTextPanel>(config.WildcardShipInfo);
            shipCargos = GetBlocksOfType<IMyCargoContainer>();

            WriteLCDs(config.WildcardShipId, shipId);

            bl = IGC.RegisterBroadcastListener(config.Channel);
            Echo($"Listening in channel {config.Channel}");

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            Echo("Working!");
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            WriteInfoLCDs($"{shipId} in channel {config.Channel}", false);
            WriteInfoLCDs($"{CalculateCargoPercentage():P1} cargo.");
            if (!string.IsNullOrEmpty(argument))
            {
                ParseTerminalMessage(argument);
                return;
            }

            while (bl.HasPendingMessage)
            {
                var message = bl.AcceptMessage();
                ParseMessage(message.Data.ToString());
            }

            WriteInfoLCDs($"Status: {status}");
            if (deliveryData.OrderId > 0)
            {
                WriteInfoLCDs($"Order: {deliveryData.OrderId}");
                WriteInfoLCDs($"Load: {deliveryData.OrderWarehouse}");
                WriteInfoLCDs($"Unload: {deliveryData.OrderCustomer}");
            }

            DoArrival();
            DoAlign();
            DoCruising();
            DoAtmNavigation();
        }

        #region ALIGN
        void DoAlign()
        {
            if (!alignData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(alignData.StateMsg)) Echo(alignData.StateMsg);

            if (!alignData.Tick())
            {
                return;
            }

            MonitorizeAlign();
        }
        void MonitorizeAlign()
        {
            if (alignData.CurrentTarget >= alignData.Waypoints.Count)
            {
                alignData.StateMsg = "Destination reached.";
                ParseTerminalMessage(alignData.Command);
                alignData.Clear();
                ResetGyros();
                ResetThrust();
                return;
            }

            if (connectorA.Status == MyShipConnectorStatus.Connected)
            {
                return;
            }

            if (DoPause()) return;

            bool corrected = AlignToVectors(alignData.TargetForward, alignData.TargetUp, config.GyrosThr);
            if (corrected)
            {
                //Wait until aligned
                ResetThrust();
                return;
            }

            var currentPos = connectorA.GetPosition();
            var currentVelocity = remoteAlign.GetShipVelocities().LinearVelocity;
            double mass = remoteAlign.CalculateShipMass().PhysicalMass;

            NavigateWaypoints(currentPos, currentVelocity, mass);
        }
        void NavigateWaypoints(Vector3D currentPos, Vector3D currentVelocity, double mass)
        {
            var targetPos = alignData.Waypoints[alignData.CurrentTarget];
            var toTarget = targetPos - currentPos;
            double distance = toTarget.Length();

            WriteInfoLCDs($"Distance to destination: {Utils.DistanceToStr(distance)}");
            WriteInfoLCDs($"Progress: {alignData.CurrentTarget + 1}/{alignData.Waypoints.Count}.");
            WriteInfoLCDs($"Has command? {!string.IsNullOrWhiteSpace(alignData.Command)}");

            if (distance < config.AlignDistanceThrWaypoints)
            {
                alignData.Next();
                ResetThrust();
                alignData.StateMsg = "Waypoint reached. Moving to the next.";
                return;
            }

            double desiredSpeed = alignData.CalculateDesiredSpeed(distance);
            var neededForce = Utils.CalculateThrustForce(toTarget, desiredSpeed, currentVelocity, mass);

            ApplyThrust(neededForce);
        }
        #endregion

        #region ARRIVAL
        void DoArrival()
        {
            if (!arrivalData.HasPosition)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(arrivalData.StateMsg)) Echo(arrivalData.StateMsg);

            if (!arrivalData.Tick())
            {
                return;
            }

            MonitorizeArrival();
        }
        void MonitorizeArrival()
        {
            double distance;
            if (arrivalData.Arrived(remotePilot.GetPosition(), out distance))
            {
                arrivalData.StateMsg = "Destination reached.";
                ParseTerminalMessage(arrivalData.Command);
                arrivalData.Clear();
                ResetGyros();
                ResetThrust();
                return;
            }

            arrivalData.StateMsg = $"Distance to destination: {Utils.DistanceToStr(distance)}";
        }
        #endregion

        #region CRUISING
        void DoCruising()
        {
            if (!cruisingData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(cruisingData.StateMsg)) Echo(cruisingData.StateMsg);

            if (!cruisingData.Tick())
            {
                return;
            }

            MonitorizeCruising();
        }
        void MonitorizeCruising()
        {
            cruisingData.UpdatePositionAndVelocity(cameraPilot.GetPosition(), remotePilot.GetShipSpeed());

            cruisingData.StateMsg = $"Cruising state {cruisingData.CurrentState}";

            WriteInfoLCDs($"Trip: {Utils.DistanceToStr(cruisingData.TotalDistance)}");
            WriteInfoLCDs($"To target: {Utils.DistanceToStr(cruisingData.DistanceToTarget)}");
            WriteInfoLCDs($"ETC: {cruisingData.EstimatedArrival:hh\\:mm\\:ss}");
            WriteInfoLCDs($"Speed: {cruisingData.Speed:F2}");
            WriteInfoLCDs($"Progress {cruisingData.Progress:P1}");
            WriteInfoLCDs(cruisingData.PrintObstacle());

            if (DoPause()) return;

            switch (cruisingData.CurrentState)
            {
                case CruisingStatus.Distress:
                    CruisingDistress();
                    break;

                case CruisingStatus.Locating:
                    CruisingLocate();
                    break;
                case CruisingStatus.Accelerating:
                    CruisingAccelerate();
                    break;
                case CruisingStatus.Cruising:
                    CruisingCruise();
                    break;
                case CruisingStatus.Braking:
                    CruisingBrake();
                    break;

                case CruisingStatus.Avoiding:
                    CruisingAvoid();
                    break;
            }
        }
        void CruisingDistress()
        {
            if (beacon != null) beacon.Enabled = true;
            BroadcastStatus($"DISTRESS: Engines damaged!, waiting in position {Utils.VectorToStr(remotePilot.GetPosition())}");
            ResetThrust();
            ResetGyros();
        }
        void CruisingLocate()
        {
            if (AlignToDirection(remotePilot.WorldMatrix.Forward, cruisingData.DirectionToTarget, config.CruisingLocateAlignThr))
            {
                BroadcastStatus("Destination located. Initializing acceleration.");
                cruisingData.CurrentState = CruisingStatus.Accelerating;

                return;
            }

            ResetThrust();
        }
        void CruisingAccelerate()
        {
            if (cruisingData.IsObstacleAhead(cameraPilot, config.CruisingCollisionDetectRange, remotePilot.GetShipVelocities().LinearVelocity))
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                cruisingData.CurrentState = CruisingStatus.Avoiding;
                return;
            }

            if (cruisingData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Braking.");
                cruisingData.CurrentState = CruisingStatus.Braking;

                return;
            }

            bool inGravity = IsShipInGravity();
            var shipVelocity = remotePilot.GetShipVelocities().LinearVelocity.Length();
            if (!inGravity && shipVelocity >= config.CruisingMaxSpeed * config.CruisingMaxSpeedThr)
            {
                BroadcastStatus("Reached cruise speed. Deactivating thrusters.");
                cruisingData.CurrentState = CruisingStatus.Cruising;
                return;
            }

            //Accelerate
            var maxSpeed = config.CruisingMaxSpeed;
            if (Vector3D.Distance(cruisingData.Origin, cameraPilot.GetPosition()) <= config.CruisingToBasesDistanceThr)
            {
                maxSpeed = config.CruisingMaxAccelerationSpeed;
            }
            ThrustToTarget(remotePilot, cruisingData.DirectionToTarget, maxSpeed);
        }
        void CruisingCruise()
        {
            if (cruisingData.IsObstacleAhead(cameraPilot, config.CruisingCollisionDetectRange, remotePilot.GetShipVelocities().LinearVelocity))
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                cruisingData.CurrentState = CruisingStatus.Avoiding;

                return;
            }

            if (cruisingData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Braking.");
                cruisingData.CurrentState = CruisingStatus.Braking;

                return;
            }

            //Maintain speed
            bool inGravity = IsShipInGravity();
            if (inGravity || !AlignToDirection(remotePilot.WorldMatrix.Forward, cruisingData.DirectionToTarget, config.CruisingCruiseAlignThr))
            {
                //Thrust until the velocity vector is aligned again with the vector to the target
                ThrustToTarget(remotePilot, cruisingData.DirectionToTarget, config.CruisingMaxSpeed);
                cruisingData.AlignThrustStart = DateTime.Now;
                cruisingData.Thrusting = true;

                return;
            }

            if (cruisingData.Thrusting)
            {
                //Thrusters started to regain alignment
                if (!inGravity && (DateTime.Now - cruisingData.AlignThrustStart).TotalSeconds > config.CruisingThrustAlignSeconds)
                {
                    //Out of gravity and alignment time consumed. Deactivate thrusters.
                    EnterCruising();
                    cruisingData.Thrusting = false;
                }

                return;
            }

            var shipVelocity = remotePilot.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity > config.CruisingMaxSpeed)
            {
                //Maximum speed exceeded. Engage thrusters in neutral to brake.
                ResetThrust();
                ResetGyros();

                return;
            }

            if (shipVelocity < config.CruisingMaxSpeed * config.CruisingMaxSpeedThr)
            {
                //Below the desired speed. Accelerate until reaching it.
                ThrustToTarget(remotePilot, cruisingData.DirectionToTarget, config.CruisingMaxSpeed);

                return;
            }

            EnterCruising();
        }
        void CruisingBrake()
        {
            ResetThrust();
            ResetGyros();

            if (!antenna.Enabled)
            {
                antenna.Enabled = true;
            }

            var shipVelocity = remotePilot.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity <= 0.1)
            {
                BroadcastStatus("Destination reached.");
                ParseTerminalMessage(cruisingData.Command);
                cruisingData.Clear();
            }
        }
        void CruisingAvoid()
        {
            if (cruisingData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Braking.");
                cruisingData.CurrentState = CruisingStatus.Braking;

                return;
            }

            //Calculate evading points
            if (!cruisingData.CalculateEvadingWaypoints(cameraPilot, config.CruisingCollisionDetectRange * 0.5))
            {
                //Cannot calculate evading point
                cruisingData.CurrentState = CruisingStatus.Braking;

                return;
            }

            //Navigate between evading points
            if (cruisingData.EvadingPoints.Count > 0)
            {
                EvadingTo(cruisingData.EvadingPoints[0], config.CruisingEvadingMaxSpeed);

                if (cruisingData.EvadingPoints.Count == 0)
                {
                    //Clear obstacle information
                    cruisingData.ClearObstacle();

                    ResetThrust();

                    //Return to navigation when the last navigation point is reached
                    cruisingData.CurrentState = CruisingStatus.Locating;
                }

                return;
            }
        }
        void EvadingTo(Vector3D wayPoint, double maxSpeed)
        {
            var toTarget = wayPoint - cameraPilot.GetPosition();
            var d = toTarget.Length();
            if (d <= config.CruisingEvadingWaypointDistance)
            {
                //Waypoint reached
                cruisingData.EvadingPoints.RemoveAt(0);

                return;
            }

            WriteInfoLCDs($"Following evading route...");
            WriteInfoLCDs($"Distance to waypoint {Utils.DistanceToStr(d)}");

            ThrustToTarget(remotePilot, Vector3D.Normalize(toTarget), maxSpeed);
        }
        void EnterCruising()
        {
            antenna.Enabled = false;

            ResetThrust();
            StopThrust();
            ResetGyros();
        }
        #endregion

        #region ATMOSPHERIC NAVIGATION
        void DoAtmNavigation()
        {
            if (!atmNavigationData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(atmNavigationData.StateMsg)) Echo(atmNavigationData.StateMsg);

            if (!atmNavigationData.Tick())
            {
                return;
            }

            MonitorizeAtmNavigation();
        }
        void MonitorizeAtmNavigation()
        {
            var remote = (atmNavigationData.Landing ? remoteLanding : remotePilot) ?? remotePilot;

            atmNavigationData.UpdatePositionAndVelocity(remote.GetPosition(), remote.GetShipSpeed());

            atmNavigationData.StateMsg = $"Navigation state {atmNavigationData.CurrentState}";

            if (DoPause()) return;

            switch (atmNavigationData.CurrentState)
            {
                case AtmNavigationStatus.Undocking:
                    AtmNavigationUndock();
                    break;
                case AtmNavigationStatus.Separating:
                    AtmNavigationSeparate();
                    break;
                case AtmNavigationStatus.Accelerating:
                    AtmNavigationAccelerate(remote);
                    break;
                case AtmNavigationStatus.Decelerating:
                    AtmNavigationDecelerate(remote);
                    break;
                case AtmNavigationStatus.Docking:
                    AtmNavigationDock();
                    break;
                case AtmNavigationStatus.Exchanging:
                    AtmNavigationExchange();
                    break;
            }
        }
        void AtmNavigationUndock()
        {
            if (connectorA.Status == MyShipConnectorStatus.Connected)
            {
                WriteInfoLCDs("Waiting for connector to unlock...");
                return;
            }

            WriteInfoLCDs("Connector unlocked. Separating.");
            atmNavigationData.CurrentState = AtmNavigationStatus.Separating;
            atmNavigationData.StartSeparation();
        }
        void AtmNavigationSeparate()
        {
            if (!atmNavigationData.IsSeparationTimeReached())
            {
                var gravity = remoteAlign.GetNaturalGravity();
                var mass = remoteAlign.CalculateShipMass().PhysicalMass;

                var force = Utils.CalculateThrustForce(
                    remoteAlign.WorldMatrix.Backward,
                    config.AtmNavigationMaxSpeed,
                    Vector3D.Zero,
                    mass);

                ApplyThrust(force, gravity, mass);

                return;
            }

            WriteInfoLCDs("Separated. Accelerating...");
            atmNavigationData.CurrentState = AtmNavigationStatus.Accelerating;
        }
        void AtmNavigationAccelerate(IMyRemoteControl remote)
        {
            AlignToDirection(remote.WorldMatrix.Forward, atmNavigationData.DirectionToTarget, config.AtmNavigationAlignThr);

            if (atmNavigationData.DistanceToTarget < config.AtmNavigationToTargetDistanceThr)
            {
                WriteInfoLCDs("Destination reached. Decelerating.");
                atmNavigationData.CurrentState = AtmNavigationStatus.Decelerating;
                return;
            }

            //Accelerate
            WriteInfoLCDs($"Trip: {Utils.DistanceToStr(atmNavigationData.TotalDistance)}");
            WriteInfoLCDs($"To target: {Utils.DistanceToStr(atmNavigationData.DistanceToTarget)}");
            WriteInfoLCDs($"Speed: {atmNavigationData.Speed:F2}");
            WriteInfoLCDs($"ETC: {atmNavigationData.EstimatedArrival:hh\\:mm\\:ss}");
            WriteInfoLCDs($"Progress {atmNavigationData.Progress:P1}");

            ThrustToTarget(remote, atmNavigationData.DirectionToTarget, config.AtmNavigationMaxSpeed);
        }
        void AtmNavigationDecelerate(IMyRemoteControl remote)
        {
            ResetThrust();
            ResetGyros();
            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity <= 0.1)
            {
                WriteInfoLCDs("Parking reached. Aproaching to dock...");
                atmNavigationData.CurrentState = AtmNavigationStatus.Docking;

                alignData.Initialize(
                    atmNavigationData.ExchangeForward,
                    atmNavigationData.ExchangeUp,
                    atmNavigationData.ExchangeApproachingWaypoints,
                    atmNavigationData.Command);
            }
        }
        void AtmNavigationDock()
        {
            if (connectorA.Status != MyShipConnectorStatus.Connected)
            {
                WriteInfoLCDs("Waiting for connector to lock.");
                return;
            }

            WriteInfoLCDs("Connector locked. Navigation finished.");
            atmNavigationData.CurrentState = AtmNavigationStatus.Exchanging;
        }
        void AtmNavigationExchange()
        {
            //Monitorize the cargo capacity of the ship
            var capacity = CalculateCargoPercentage();
            if (atmNavigationData.ExchangeTask == ExchangeTasks.RocketLoad)
            {
                WriteInfoLCDs($"Loading to {config.AtmNavigationMaxLoad:P1}");

                if (capacity >= config.AtmNavigationMaxLoad)
                {
                    List<string> parts = new List<string>()
                    {
                        $"Command=REQUEST_DOCK",
                        $"To={config.AtmNavigationUnloadBase}",
                        $"From={shipId}",
                        $"Task={(int)ExchangeTasks.RocketUnload}",
                    };
                    BroadcastMessage(parts);

                    atmNavigationData.Clear();
                    status = ShipStatus.Idle;
                }
            }
            else if (atmNavigationData.ExchangeTask == ExchangeTasks.RocketUnload)
            {
                WriteInfoLCDs($"Unloading to {config.AtmNavigationMinLoad:P1}");

                if (capacity <= config.AtmNavigationMinLoad)
                {
                    List<string> parts = new List<string>()
                    {
                        $"Command=REQUEST_DOCK",
                        $"To={config.AtmNavigationLoadBase}",
                        $"From={shipId}",
                        $"Task={(int)ExchangeTasks.RocketLoad}",
                    };
                    BroadcastMessage(parts);

                    atmNavigationData.Clear();
                    status = ShipStatus.Idle;
                }
            }
            else
            {
                atmNavigationData.Clear();
                status = ShipStatus.Idle;
            }
        }
        #endregion

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "PAUSE") Pause();
            else if (argument == "RESUME") Resume();

            else if (argument.StartsWith("REQUEST_DOCK")) RequestDock(argument);
            else if (argument == "START_ROUTE") StartRoute();

            else if (argument.StartsWith("GOTO")) Goto(argument);
            else if (argument == "APPROACH_TO_PARKING") ApproachToParking();

            else if (argument == "REQUEST_LOAD_TO_WAREHOUSE") RequestLoadToWarehouse();
            else if (argument == "START_LOADING") StartLoading();

            else if (argument == "REQUEST_UNLOAD_TO_CUSTOMER") RequestUnloadToCustomer();
            else if (argument == "START_UNLOADING") StartUnloading();
            else if (argument == "UNLOAD_FINISHED") UnloadFinished();

            else if (argument == "WAITING") Waiting();

            else if (argument == "START_APPROACH") StartApproach();
            else if (argument == "ENABLE_LOGS") EnableLogs();
        }
        /// <summary>
        /// Ship reset
        /// </summary>
        void Reset()
        {
            Storage = "";

            status = ShipStatus.Idle;

            deliveryData.Clear();
            alignData.Clear();
            arrivalData.Clear();
            cruisingData.Clear();
            atmNavigationData.Clear();

            remotePilot.SetAutoPilotEnabled(false);
            remotePilot.ClearWaypoints();
            remoteAlign.SetAutoPilotEnabled(false);
            remoteAlign.ClearWaypoints();
            ResetGyros();
            ResetThrust();

            WriteInfoLCDs("Stopped.");
        }
        /// <summary>
        /// Pause all tasks
        /// </summary>
        void Pause()
        {
            paused = true;
        }
        /// <summary>
        /// Resume all tasks
        /// </summary>
        void Resume()
        {
            paused = false;
        }

        /// <summary>
        /// Requests docking at a base defined in the argument.
        /// </summary>
        void RequestDock(string argument)
        {
            string[] lines = argument.Split('|');
            var bse = Utils.ReadString(lines, "Base");
            var task = Utils.ReadInt(lines, "Task");

            status = ShipStatus.Idle;

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_DOCK",
                $"To={bse}",
                $"From={shipId}",
                $"Task={task}",
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// Starts a route to the configured loading base.
        /// </summary>
        void StartRoute()
        {
            status = ShipStatus.Idle;

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_DOCK",
                $"To={config.AtmNavigationLoadBase}",
                $"From={shipId}",
                $"Task={(int)ExchangeTasks.RocketLoad}",
            };
            BroadcastMessage(parts);
        }

        /// <summary>
        /// Goes to a position defined in the argument.
        /// </summary>
        void Goto(string argument)
        {
            string[] lines = argument.Split('|');
            var position = Utils.ReadVector(lines, "Position");

            StartCruising(position, "WAITING");
        }
        /// <summary>
        /// Make the approach to the destination parking position
        /// </summary>
        void ApproachToParking()
        {
            var destination = deliveryData.DestinationPosition;
            var destinationName = deliveryData.DestinationName;
            var onArrival = deliveryData.OnDestinationArrival;

            var shipPosition = remotePilot.GetPosition();
            var distance = Vector3D.Distance(shipPosition, destination);
            if (distance < 5000)
            {
                //Load on autopilot
                StartAutoPilot(destination, destinationName, config.AlignExchangeApproachingSpeed, onArrival);
            }
            else
            {
                //Load on cruise mode
                StartCruising(destination, onArrival);
            }
        }

        /// <summary>
        /// Seq_xxx - SHIPX arrives at Parking_WH and requests permission to load
        /// Request:  REQUEST_LOAD_TO_WAREHOUSE
        /// Execute:  REQUEST_LOAD
        /// </summary>
        void RequestLoadToWarehouse()
        {
            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_LOAD",
                $"To={deliveryData.OrderWarehouse}",
                $"From={shipId}",
                $"Order={deliveryData.OrderId}"
            };
            BroadcastMessage(parts);

            timerWaiting.StartCountdown();

            status = ShipStatus.WaitingForLoad;
        }
        /// <summary>
        /// Seq_C_2b - When the ship reaches the connector, it informs the base to begin loading.
        /// Request  : START_LOADING
        /// Execute  : LOADING
        /// </summary>
        void StartLoading()
        {
            List<string> parts = new List<string>()
            {
                $"Command=LOADING",
                $"To={deliveryData.OrderWarehouse}",
                $"From={shipId}",
                $"Order={deliveryData.OrderId}",
                $"Exchange={deliveryData.ExchangeName}"
            };
            BroadcastMessage(parts);

            //Dock
            timerLock.StartCountdown();

            //Load mode
            timerLoad.StartCountdown();

            status = ShipStatus.Loading;
        }

        /// <summary>
        /// Seq_C_4c - SHIPX arrives at Parking_BASEX and requests permission to unload
        /// Request:  REQUEST_UNLOAD_TO_CUSTOMER
        /// Execute:  REQUEST_UNLOAD
        /// </summary>
        void RequestUnloadToCustomer()
        {
            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_UNLOAD",
                $"To={deliveryData.OrderCustomer}",
                $"From={shipId}",
                $"Order={deliveryData.OrderId}"
            };
            BroadcastMessage(parts);

            timerWaiting.StartCountdown();

            status = ShipStatus.WaitingForUnload;
        }
        /// <summary>
        /// Seq_D_2b - SHIPX notifies BASEX that it has arrived to unload the ORDER_ID to the connector. It launches [UNLOADING] to BASEX.
        /// Request:  START_UNLOADING
        /// Execute:  UNLOADING
        /// </summary>
        void StartUnloading()
        {

            List<string> parts = new List<string>()
            {
                $"Command=UNLOADING",
                $"To={deliveryData.OrderCustomer}",
                $"From={shipId}",
                $"Order={deliveryData.OrderId}",
                $"Exchange={deliveryData.ExchangeName}"
            };
            BroadcastMessage(parts);

            //Dock
            timerLock.StartCountdown();

            //Unload mode
            timerUnload.StartCountdown();

            status = ShipStatus.Unloading;
        }
        /// <summary>
        /// Seq_D_2d - SHIPX informs BASEX of the end of the unload, and begins the return journey to Parking_WH.
        /// Execute:  UNLOADED - to BASEX
        /// Execute:  APPROACH_TO_PARKING - When the route of the exit waypoints ends, the trip to Parking_WH begins
        /// Execute:  WAITING - When it arrives at Parking_WH, it is put on hold.
        /// </summary>
        void UnloadFinished()
        {
            if (!deliveryData.Active)
            {
                return;
            }

            //Notice of download completed to base
            List<string> parts = new List<string>()
            {
                $"Command=UNLOADED",
                $"To={deliveryData.OrderCustomer}",
                $"From={shipId}",
                $"Order={deliveryData.OrderId}",
                $"Warehouse={deliveryData.OrderWarehouse}"
            };
            BroadcastMessage(parts);

            //Load the return trip to the Warehouse
            //It will monitor the journey to the Warehouse standby position, execute WAITING, and wait for instructions from the base.
            deliveryData.PrepareNavigationToWarehouse("WAITING");

            //Load the exit route and when you reach the last waypoint of the connector
            //It will execute APPROACH_TO_PARKING, which will activate navigation to the previously loaded destination
            deliveryData.PrepareNavigationFromExchange("APPROACH_TO_PARKING");

            Depart();

            status = ShipStatus.RouteToLoad;
        }

        /// <summary>
        /// Seq_D_2f - SHIPX is on hold
        /// </summary>
        void Waiting()
        {
            //Puts the ship on hold
            timerWaiting.StartCountdown();

            //It occurs when the ship reaches the last waypoint of the delivery route
            deliveryData.Clear();

            status = ShipStatus.Idle;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of logs
        /// </summary>
        void EnableLogs()
        {
            config.EnableLogs = !config.EnableLogs;
        }
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');

            string command = Utils.ReadArgument(lines, "Command");
            if (command == "REQUEST_STATUS") CmdRequestStatus(lines);
            else if (command == "START_DELIVERY") CmdStartDelivery(lines);
            else if (command == "DOCK") CmdDock(lines);
            else if (command == "LOADED") CmdLoaded(lines);
        }
        /// <summary>
        /// Seq_A_2 - The ship responds with its status
        /// Request:  REQUEST_STATUS
        /// Execute:  RESPONSE_STATUS
        /// </summary>
        void CmdRequestStatus(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            Vector3D position = remotePilot.GetPosition();
            double speed = remotePilot.GetShipVelocities().LinearVelocity.Length();

            List<string> parts = new List<string>()
            {
                $"Command=RESPONSE_STATUS",
                $"To={from}",
                $"From={shipId}",
                $"Status={(int)status}",
                $"Warehouse={deliveryData.OrderWarehouse}",
                $"WarehousePosition={Utils.VectorToStr(deliveryData.OrderWarehouseParking)}",
                $"Customer={deliveryData.OrderCustomer}",
                $"CustomerPosition={Utils.VectorToStr(deliveryData.OrderCustomerParking)}",
                $"Position={Utils.VectorToStr(position)}",
                $"Capacity={CalculateCargoPercentage():F2}",
                $"Speed={speed:F2}",
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// Seq_C_2a - SHIPX registers the request, begins navigation to the specified parking position, and requests an exchange to dock.
        /// Request:  START_DELIVERY
        /// Execute:  REQUEST_LOAD_TO_WAREHOUSE when the ship reaches the WH parking lot
        /// </summary>
        void CmdStartDelivery(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.RouteToLoad;

            deliveryData.SetOrder(
                Utils.ReadInt(lines, "Order"),
                Utils.ReadString(lines, "Warehouse"),
                Utils.ReadVector(lines, "WarehouseParking"),
                Utils.ReadString(lines, "Customer"),
                Utils.ReadVector(lines, "CustomerParking"));

            deliveryData.PrepareNavigationToWarehouse("REQUEST_LOAD_TO_WAREHOUSE");

            ApproachToParking();
        }

        /// <summary>
        /// Seq_xxxx / Seq_D_2a - SHIPX begins navigation to the specified connector and docks in LOADING or UNLOADING MODE.
        /// Execute:  START_LOADING / START_UNLOADING
        /// </summary>
        void CmdDock(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != shipId)
            {
                return;
            }

            ExchangeTasks task = (ExchangeTasks)Utils.ReadInt(lines, "Task");

            if (task == ExchangeTasks.DeliveryLoad)
            {
                status = ShipStatus.ApproachingLoad;

                deliveryData.SetExchange(
                    Utils.ReadString(lines, "Exchange"),
                    Utils.ReadVector(lines, "Forward"),
                    Utils.ReadVector(lines, "Up"),
                    Utils.ReadVectorList(lines, "WayPoints"));

                deliveryData.PrepareNavigationToExchange("START_LOADING");

                ApproachToExchange();
            }
            else if (task == ExchangeTasks.DeliveryUnload)
            {
                status = ShipStatus.ApproachingUnload;

                deliveryData.SetExchange(
                    Utils.ReadString(lines, "Exchange"),
                    Utils.ReadVector(lines, "Forward"),
                    Utils.ReadVector(lines, "Up"),
                    Utils.ReadVectorList(lines, "WayPoints"));

                deliveryData.PrepareNavigationToExchange("START_UNLOADING");

                ApproachToExchange();
            }
            else if (task == ExchangeTasks.RocketLoad)
            {
                status = ShipStatus.ApproachingLoad;

                atmNavigationData.Initialize(
                    Utils.ReadInt(lines, "Landing") == 1,
                    remotePilot.GetPosition(),
                    Utils.ReadVector(lines, "Parking"),
                    "START_LOADING");

                atmNavigationData.SetExchange(
                    Utils.ReadString(lines, "Exchange"),
                    Utils.ReadVector(lines, "Forward"),
                    Utils.ReadVector(lines, "Up"),
                    Utils.ReadVectorList(lines, "WayPoints"),
                    task);

                timerUnlock?.StartCountdown();
            }
            else if (task == ExchangeTasks.RocketUnload)
            {
                status = ShipStatus.ApproachingUnload;

                atmNavigationData.Initialize(
                    Utils.ReadInt(lines, "Landing") == 1,
                    remotePilot.GetPosition(),
                    Utils.ReadVector(lines, "Parking"),
                    "START_UNLOADING");

                atmNavigationData.SetExchange(
                    Utils.ReadString(lines, "Exchange"),
                    Utils.ReadVector(lines, "Forward"),
                    Utils.ReadVector(lines, "Up"),
                    Utils.ReadVectorList(lines, "WayPoints"),
                    task);

                timerUnlock?.StartCountdown();
            }
        }

        /// <summary>
        /// Seq_C_4a - SHIPX loads the route to Parking_BASEX and begins the exit maneuver from the WH connector
        /// Request:  LOADED
        /// Execute:  APPROACH_TO_PARKING when SHIPX reaches the last waypoint of the connector's exit route
        /// Execute:  REQUEST_UNLOAD_TO_CUSTOMER when SHIPX arrives at Parking_BASEX
        /// </summary>
        void CmdLoaded(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.RouteToUnload;

            //Load the exit route and upon reaching the last waypoint of the connector, it will execute APPROACH_TO_PARKING, which will activate navigation to the Customer
            deliveryData.PrepareNavigationFromExchange("APPROACH_TO_PARKING");

            //It will monitor the trip to the Customer's waiting position, execute REQUEST_UNLOAD_TO_CUSTOMER, and wait for instructions from the base.
            deliveryData.PrepareNavigationToCustomer("REQUEST_UNLOAD_TO_CUSTOMER");

            Depart();
        }

        /// <summary>
        /// Perform the approach maneuver from any position
        /// </summary>
        void ApproachToExchange()
        {
            //Get the distance to the first approach point
            var shipPosition = remoteAlign.GetPosition();
            var wp = deliveryData.Waypoints[0];
            double distance = Vector3D.Distance(shipPosition, wp);
            if (distance > 500)
            {
                //Load the autopilot to the position of the first waypoint
                StartAutoPilot(wp, "Path to Connector", config.AlignExchangeApproachingSpeed, "START_APPROACH");
            }
            else
            {
                StartApproach();
            }
        }
        /// <summary>
        /// Perform the uncoupling maneuver and travel to the destination
        /// </summary>
        void Depart()
        {
            timerUnlock?.StartCountdown();

            //The exit maneuver begins
            alignData.Initialize(deliveryData.AlignFwd, deliveryData.AlignUp, deliveryData.Waypoints, deliveryData.OnLastWaypoint);
        }
        /// <summary>
        /// Navigate the waypoints
        /// </summary>
        void StartApproach()
        {
            remotePilot.SetAutoPilotEnabled(false);
            remotePilot.ClearWaypoints();

            alignData.Initialize(deliveryData.AlignFwd, deliveryData.AlignUp, deliveryData.Waypoints, deliveryData.OnLastWaypoint);
        }
        /// <summary>
        /// Set the autopilot
        /// </summary>
        void StartAutoPilot(Vector3D destination, string destinationName, double velocity, string onArrival)
        {
            arrivalData.Initialize(destination, config.AlignExchangeDistanceThr, onArrival);

            timerPilot.Trigger();

            remoteAlign.ClearWaypoints();
            remoteAlign.AddWaypoint(destination, destinationName);
            remoteAlign.SetCollisionAvoidance(true);
            remoteAlign.WaitForFreeWay = false;
            remoteAlign.FlightMode = FlightMode.OneWay;
            remoteAlign.SpeedLimit = (float)velocity;
            remoteAlign.SetAutoPilotEnabled(true);
        }
        /// <summary>
        /// Set up the long trip
        /// </summary>
        void StartCruising(Vector3D destination, string onArrival)
        {
            cruisingData.Initialize(cameraPilot.GetPosition(), destination, onArrival);
        }
        #endregion

        #region UTILITY
        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks.FirstOrDefault();
        }
        List<T> GetBlocksOfType<T>() where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);
            return blocks;
        }
        List<T> GetBlocksOfType<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks;
        }

        void WriteLCDs(string wildcard, string text)
        {
            List<IMyTextPanel> lcds = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(lcds, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(wildcard));
            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, false);
            }
        }
        void WriteInfoLCDs(string text, bool append = true)
        {
            Echo(text);

            foreach (var lcd in infoLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text + Environment.NewLine, append);
            }
        }
        void WriteLogLCDs(string text)
        {
            if (!config.EnableLogs)
            {
                return;
            }

            sbLog.Insert(0, text + Environment.NewLine);

            var log = sbLog.ToString();
            string[] logLines = log.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            foreach (var lcd in logLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;

                string customData = lcd.CustomData;
                var blackList = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (blackList.Length > 0)
                {
                    string[] lines = logLines.Where(l => !blackList.Any(b => l.Contains(b))).ToArray();
                    lcd.WriteText(string.Join(Environment.NewLine, lines));
                }
                else
                {
                    lcd.WriteText(log, false);
                }
            }
        }
        void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            WriteLogLCDs($"BroadcastMessage: {message}");

            IGC.SendBroadcastMessage(config.Channel, message);
        }
        void BroadcastStatus(string message)
        {
            WriteLogLCDs($"BroadcastStatus: {message}");

            IGC.SendBroadcastMessage(config.Channel, message);
        }

        void ThrustToTarget(IMyRemoteControl remote, Vector3D toTarget, double maxSpeed)
        {
            var currentVelocity = remote.GetShipVelocities().LinearVelocity;
            double mass = remote.CalculateShipMass().PhysicalMass;

            var force = Utils.CalculateThrustForce(toTarget, maxSpeed, currentVelocity, mass);
            ApplyThrust(force);
        }

        void ApplyThrust(Vector3D force)
        {
            foreach (var t in thrusters)
            {
                var thrustDir = t.WorldMatrix.Backward;
                double alignment = thrustDir.Dot(force);

                t.Enabled = true;
                t.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / t.MaxEffectiveThrust, 1f) : 0f;
            }
        }
        void ApplyThrust(Vector3D force, Vector3D gravity, double mass)
        {
            if (gravity.Length() < 0.001)
            {
                ApplyThrust(force);
                return;
            }

            //Find opposing thrusters to counteract gravity, to obtain total effective thrust
            var opposingThrusters = new List<IMyThrust>();
            double totalEffectiveThrust = 0;
            foreach (var t in thrusters)
            {
                var thrustDir = t.WorldMatrix.Backward;
                double alignment = thrustDir.Dot(-gravity);

                if (alignment >= 0.9)
                {
                    opposingThrusters.Add(t);
                    totalEffectiveThrust += t.MaxEffectiveThrust;

                    continue;
                }

                //Apply force to thrusters, no opposing thrusters
                alignment = thrustDir.Dot(force);

                t.Enabled = true;
                t.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / t.MaxEffectiveThrust, 1f) : 0f;
            }

            //Apply force to opposing thrusters, distributing the gravity force over them
            if (totalEffectiveThrust <= 0.01) return;

            var gravForce = -Vector3D.Normalize(gravity) * (mass * gravity.Length());

            foreach (var t in opposingThrusters)
            {
                double share = t.MaxEffectiveThrust / totalEffectiveThrust;
                var targetForce = gravForce * share;

                var thrustDir = t.WorldMatrix.Backward;
                double alignment = thrustDir.Dot(targetForce);

                t.Enabled = true;
                t.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / t.MaxEffectiveThrust, 1f) : 0f;
            }
        }
        void ResetThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = true;
                t.ThrustOverridePercentage = 0f;
            }
        }
        void StopThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = false;
                t.ThrustOverridePercentage = 0;
            }
        }

        bool AlignToDirection(Vector3D direction, Vector3D toTarget, double thr)
        {
            double angle = Utils.AngleBetweenVectors(direction, toTarget);
            WriteInfoLCDs($"Target angle: {angle:F4}");

            if (angle <= thr)
            {
                ResetGyros();
                WriteInfoLCDs("Aligned.");
                return false;
            }
            WriteInfoLCDs("Aligning...");

            var rotationAxis = Vector3D.Cross(direction, toTarget);
            if (rotationAxis.Length() <= 0.001) rotationAxis = new Vector3D(1, 0, 0);
            ApplyGyroOverride(rotationAxis);

            return true;
        }
        bool AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipMatrix = remoteAlign.WorldMatrix;
            var shipForward = shipMatrix.Forward;
            var shipUp = shipMatrix.Up;

            double angleFW = Utils.AngleBetweenVectors(shipForward, targetForward);
            double angleUP = Utils.AngleBetweenVectors(shipUp, targetUp);
            WriteInfoLCDs($"Target angles: {angleFW:F2} | {angleUP:F2}");

            if (angleFW <= thr && angleUP <= thr)
            {
                ResetGyros();
                WriteInfoLCDs("Aligned.");
                return false;
            }
            WriteInfoLCDs("Aligning...");

            bool corrected = false;
            if (angleFW > thr)
            {
                var rotationAxisFW = Vector3D.Cross(shipForward, targetForward);
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ApplyGyroOverride(rotationAxisFW);
                corrected = true;
            }

            if (angleUP > thr)
            {
                var rotationAxisUP = Vector3D.Cross(shipUp, targetUp);
                if (rotationAxisUP.Length() <= 0.001) rotationAxisUP = new Vector3D(1, 0, 0);
                ApplyGyroOverride(rotationAxisUP);
                corrected = true;
            }

            return corrected;
        }
        void ApplyGyroOverride(Vector3D axis)
        {
            foreach (var gyro in gyros)
            {
                var localAxis = Vector3D.TransformNormal(axis, MatrixD.Transpose(gyro.WorldMatrix));
                var gyroRot = localAxis * -config.GyrosSpeed;
                gyro.GyroOverride = true;
                gyro.Pitch = (float)gyroRot.X;
                gyro.Yaw = (float)gyroRot.Y;
                gyro.Roll = (float)gyroRot.Z;
            }
        }
        void ResetGyros()
        {
            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = false;
                gyro.Pitch = 0;
                gyro.Yaw = 0;
                gyro.Roll = 0;
            }
        }

        bool IsShipInGravity()
        {
            var gravitry = remotePilot.GetNaturalGravity();
            return gravitry.Length() >= 0.001;
        }

        double CalculateCargoPercentage()
        {
            if (shipCargos.Count == 0)
            {
                return 0;
            }

            double max = 0;
            double curr = 0;
            foreach (var cargo in shipCargos)
            {
                var inv = cargo.GetInventory();
                max += (double)inv.MaxVolume;
                curr += (double)inv.CurrentVolume;
            }

            return curr / max;
        }

        bool DoPause()
        {
            if (!paused)
            {
                return false;
            }

            WriteInfoLCDs("Paused...");

            ResetThrust();
            ResetGyros();
            return true;
        }
        #endregion

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            status = (ShipStatus)Utils.ReadInt(storageLines, "Status");
            deliveryData.LoadFromStorage(Utils.ReadString(storageLines, "DeliveryData"));
            alignData.LoadFromStorage(Utils.ReadString(storageLines, "AlignData"));
            arrivalData.LoadFromStorage(Utils.ReadString(storageLines, "ArrivalData"));
            cruisingData.LoadFromStorage(Utils.ReadString(storageLines, "NavigationData"));
            atmNavigationData.LoadFromStorage(Utils.ReadString(storageLines, "AtmNavigationData"));
            paused = Utils.ReadInt(storageLines, "Paused", 0) == 1;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Status={(int)status}{Environment.NewLine}",
                $"DeliveryData={deliveryData.SaveToStorage()}",
                $"AlignData={alignData.SaveToStorage()}",
                $"ArrivalData={arrivalData.SaveToStorage()}",
                $"NavigationData={cruisingData.SaveToStorage()}",
                $"AtmNavigationData={atmNavigationData.SaveToStorage()}",
                $"Paused={(paused ? 1 : 0)}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
