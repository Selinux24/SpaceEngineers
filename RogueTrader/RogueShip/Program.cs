﻿using Sandbox.ModAPI.Ingame;
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

        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        readonly List<IMyTextPanel> infoLCDs = new List<IMyTextPanel>();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly List<IMyCargoContainer> shipCargos = new List<IMyCargoContainer>();
        #endregion

        readonly string shipId;
        readonly Config config;

        readonly StringBuilder sbLog = new StringBuilder();

        readonly AlignData alignData;
        readonly ArrivalData arrivalData;
        readonly CruisingData cruisingData;
        readonly NavigationData navigationData;

        ShipStatus shipStatus = ShipStatus.Idle;
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

            alignData = new AlignData(config);
            arrivalData = new ArrivalData(config);
            cruisingData = new CruisingData(config);
            navigationData = new NavigationData(config);

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

        public void Main(string argument)
        {
            WriteInfoLCDs($"{shipId} in channel {config.Channel}");
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

            DoArrival();
            DoAlign();
            DoCruising();
            DoAtmNavigation();

            UpdateShipStatus();
        }

        #region ALIGN
        void DoAlign()
        {
            if (!alignData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(alignData.StateMsg)) WriteInfoLCDs(alignData.StateMsg);

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

            alignData.UpdatePosition(connectorA.GetPosition());

            var currentVelocity = remoteAlign.GetShipVelocities().LinearVelocity;
            double mass = remoteAlign.CalculateShipMass().PhysicalMass;

            NavigateWaypoints(currentVelocity, mass);
        }
        void NavigateWaypoints(Vector3D currentVelocity, double mass)
        {
            WriteInfoLCDs(alignData.GetAlignState());

            var distance = alignData.Distance;
            if (distance < config.AlignDistanceThrWaypoints)
            {
                alignData.Next();
                ResetThrust();
                alignData.StateMsg = "Waypoint reached. Moving to the next.";
                return;
            }

            double desiredSpeed = alignData.CalculateDesiredSpeed(distance);
            var neededForce = Utils.CalculateThrustForce(alignData.ToTarget, desiredSpeed, currentVelocity, mass);

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

            if (!string.IsNullOrWhiteSpace(arrivalData.StateMsg)) WriteInfoLCDs(arrivalData.StateMsg);

            if (!arrivalData.Tick())
            {
                return;
            }

            MonitorizeArrival();
        }
        void MonitorizeArrival()
        {
            if (arrivalData.Arrived(remotePilot.GetPosition()))
            {
                ParseTerminalMessage(arrivalData.Command);
                arrivalData.Clear();
            }
        }
        #endregion

        #region CRUISING
        void DoCruising()
        {
            if (!cruisingData.HasTarget)
            {
                return;
            }

            if (!cruisingData.Tick())
            {
                return;
            }

            MonitorizeCruising();
        }
        void MonitorizeCruising()
        {
            cruisingData.UpdatePositionAndVelocity(cameraPilot.GetPosition(), remotePilot.GetShipSpeed());

            WriteInfoLCDs($"{cruisingData.CurrentState}");

            if (DoPause()) return;

            switch (cruisingData.CurrentState)
            {
                case CruisingStatus.Locating:
                    CruisingLocate();
                    break;
                case CruisingStatus.Accelerating:
                    CruisingAccelerate();
                    break;
                case CruisingStatus.Cruising:
                    CruisingCruise();
                    break;
                case CruisingStatus.Decelerating:
                    CruisingDecelerate();
                    break;

                case CruisingStatus.Avoiding:
                    CruisingAvoid();
                    break;
            }
        }
        void CruisingLocate()
        {
            if (!AlignToDirection(remotePilot.WorldMatrix.Forward, cruisingData.DirectionToTarget, config.CruisingLocateAlignThr))
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
                BroadcastStatus("Destination reached. Decelerating.");
                cruisingData.CurrentState = CruisingStatus.Decelerating;

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
            WriteInfoLCDs(cruisingData.GetTripState());

            var maxSpeed = config.CruisingMaxSpeed;
            if (Vector3D.Distance(cruisingData.NextWaypoint, cameraPilot.GetPosition()) <= config.CruisingToBasesDistanceThr)
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
                BroadcastStatus("Destination reached. Decelerating.");
                cruisingData.CurrentState = CruisingStatus.Decelerating;

                return;
            }

            //Maintain speed
            WriteInfoLCDs(cruisingData.GetTripState());

            bool inGravity = IsShipInGravity();
            if (inGravity || AlignToDirection(remotePilot.WorldMatrix.Forward, cruisingData.DirectionToTarget, config.CruisingCruiseAlignThr))
            {
                WriteInfoLCDs("Not aligned");

                //Thrust until the velocity vector is aligned again with the vector to the target
                ThrustToTarget(remotePilot, cruisingData.DirectionToTarget, config.CruisingMaxSpeed);
                cruisingData.AlignThrustStart = DateTime.Now;
                cruisingData.Thrusting = true;

                return;
            }

            if (cruisingData.Thrusting)
            {
                WriteInfoLCDs("Thrusters started to regain alignment");

                //Thrusters started to regain alignment
                if (!inGravity && (DateTime.Now - cruisingData.AlignThrustStart).TotalSeconds > config.CruisingThrustAlignSeconds)
                {
                    //Out of gravity and alignment time consumed. Deactivate thrusters.
                    CruisingEnterCruise();
                    cruisingData.Thrusting = false;
                }

                return;
            }

            var shipVelocity = remotePilot.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity > config.CruisingMaxSpeed)
            {
                WriteInfoLCDs("Maximum speed exceeded");

                //Maximum speed exceeded. Engage thrusters in neutral to brake.
                ResetThrust();
                ResetGyros();

                return;
            }

            if (shipVelocity < config.CruisingMaxSpeed * config.CruisingMaxSpeedThr)
            {
                WriteInfoLCDs("Below the desired speed");

                //Below the desired speed. Accelerate until reaching it.
                ThrustToTarget(remotePilot, cruisingData.DirectionToTarget, config.CruisingMaxSpeed);

                return;
            }

            CruisingEnterCruise();
        }
        void CruisingDecelerate()
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
                ParseTerminalMessage(cruisingData.TerminalMessage);
                cruisingData.Clear();
            }
        }
        void CruisingAvoid()
        {
            WriteInfoLCDs(cruisingData.PrintObstacle());

            if (cruisingData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Decelerating.");
                cruisingData.CurrentState = CruisingStatus.Decelerating;

                return;
            }

            //Calculate evading points
            if (!cruisingData.CalculateEvadingWaypoints(cameraPilot, config.CruisingCollisionDetectRange * 0.5))
            {
                //Cannot calculate evading point
                cruisingData.CurrentState = CruisingStatus.Decelerating;

                return;
            }

            //Navigate between evading points
            if (cruisingData.EvadingPoints.Count > 0)
            {
                CruisingEvadingTo(cruisingData.EvadingPoints[0], config.CruisingEvadingMaxSpeed);

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
        void CruisingEvadingTo(Vector3D wayPoint, double maxSpeed)
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
        void CruisingEnterCruise()
        {
            antenna.Enabled = false;

            ResetThrust();
            StopThrust();
            ResetGyros();
        }
        #endregion

        #region ATMOSPHERIC NAVIGATION
        /// <summary>
        /// Starts a route to the configured loading base.
        /// </summary>
        void AtmNavigationStartRoute()
        {
            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_DOCK",
                $"To={config.AtmNavigationRoute.LoadBase}",
                $"From={shipId}",
                $"Task={(int)ExchangeTasks.Load}",
            };
            BroadcastMessage(parts);
        }
        void DoAtmNavigation()
        {
            if (!navigationData.HasTarget)
            {
                return;
            }

            if (!navigationData.Tick())
            {
                return;
            }

            MonitorizeAtmNavigation();
        }
        void MonitorizeAtmNavigation()
        {
            var remote = (navigationData.Landing ? remoteLanding : remotePilot) ?? remotePilot;

            navigationData.UpdatePositionAndVelocity(remote.GetPosition(), remote.GetShipSpeed());

            WriteInfoLCDs($"{navigationData.CurrentState}");

            if (DoPause()) return;

            switch (navigationData.CurrentState)
            {
                case NavigationStatus.Undocking:
                    AtmNavigationUndock();
                    break;
                case NavigationStatus.Separating:
                    AtmNavigationSeparate();
                    break;
                case NavigationStatus.Accelerating:
                    AtmNavigationAccelerate(remote);
                    break;
                case NavigationStatus.Decelerating:
                    AtmNavigationDecelerate(remote);
                    break;
                case NavigationStatus.Docking:
                    AtmNavigationDock();
                    break;
                case NavigationStatus.Exchanging:
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
            navigationData.CurrentState = NavigationStatus.Separating;
            navigationData.StartSeparation();
        }
        void AtmNavigationSeparate()
        {
            if (!navigationData.IsSeparationTimeReached())
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
            navigationData.CurrentState = NavigationStatus.Accelerating;
        }
        void AtmNavigationAccelerate(IMyRemoteControl remote)
        {
            AlignToDirection(remote.WorldMatrix.Forward, navigationData.DirectionToTarget, config.AtmNavigationAlignThr);

            if (navigationData.DistanceToTarget < config.AtmNavigationToTargetDistanceThr)
            {
                WriteInfoLCDs("Destination reached. Decelerating.");
                navigationData.CurrentState = NavigationStatus.Decelerating;
                return;
            }

            //Accelerate
            WriteInfoLCDs(navigationData.GetTripState());

            ThrustToTarget(remote, navigationData.DirectionToTarget, config.AtmNavigationMaxSpeed);
        }
        void AtmNavigationDecelerate(IMyRemoteControl remote)
        {
            ResetThrust();
            ResetGyros();
            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity <= 0.1)
            {
                WriteInfoLCDs("Parking reached. Aproaching to dock...");
                navigationData.CurrentState = NavigationStatus.Docking;

                alignData.Initialize(navigationData.Exchange, navigationData.Command);
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
            navigationData.CurrentState = NavigationStatus.Exchanging;
        }
        void AtmNavigationExchange()
        {
            //Monitorize the cargo capacity of the ship
            var capacity = CalculateCargoPercentage();
            if (navigationData.ExchangeTask == ExchangeTasks.Load)
            {
                WriteInfoLCDs($"Loading to {config.AtmNavigationMaxLoad:P1}");

                if (capacity >= config.AtmNavigationMaxLoad)
                {
                    List<string> parts = new List<string>()
                    {
                        $"Command=REQUEST_DOCK",
                        $"To={config.AtmNavigationRoute.UnloadBase}",
                        $"From={shipId}",
                        $"Task={(int)ExchangeTasks.Unload}",
                    };
                    BroadcastMessage(parts);

                    navigationData.Clear();
                }
            }
            else if (navigationData.ExchangeTask == ExchangeTasks.Unload)
            {
                WriteInfoLCDs($"Unloading to {config.AtmNavigationMinLoad:P1}");

                if (capacity <= config.AtmNavigationMinLoad)
                {
                    List<string> parts = new List<string>()
                    {
                        $"Command=REQUEST_DOCK",
                        $"To={config.AtmNavigationRoute.LoadBase}",
                        $"From={shipId}",
                        $"Task={(int)ExchangeTasks.Load}",
                    };
                    BroadcastMessage(parts);

                    navigationData.Clear();
                }
            }
            else
            {
                navigationData.Clear();
            }
        }
        void AtmNavigationTriggerInit(bool landing, List<Vector3D> waypoints, ExchangeInfo info, ExchangeTasks task, string command)
        {
            navigationData.Initialize(landing, waypoints, command);

            navigationData.SetExchange(info, task);

            timerUnlock?.StartCountdown();
        }
        #endregion

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "PAUSE") Pause();
            else if (argument == "RESUME") Resume();
            else if (argument == "ENABLE_LOGS") EnableLogs();

            else if (argument.StartsWith("REQUEST_DOCK")) RequestDock(argument);

            else if (argument == "START_ROUTE") AtmNavigationStartRoute();
        }

        /// <summary>
        /// Ship reset
        /// </summary>
        void Reset()
        {
            Storage = "";

            alignData.Clear();
            arrivalData.Clear();
            cruisingData.Clear();
            navigationData.Clear();

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
        /// Changes the state of the variable that controls the display of logs
        /// </summary>
        void EnableLogs()
        {
            config.EnableLogs = !config.EnableLogs;
        }

        /// <summary>
        /// Requests docking at a base defined in the argument.
        /// </summary>
        void RequestDock(string argument)
        {
            string[] lines = argument.Split('|');
            var bse = Utils.ReadString(lines, "Base");
            var task = Utils.ReadInt(lines, "Task");

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_DOCK",
                $"To={bse}",
                $"From={shipId}",
                $"Task={task}",
            };
            BroadcastMessage(parts);
        }
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = Utils.ReadArgument(lines, "Command");

            if (command == "REQUEST_STATUS") CmdRequestStatus(lines);

            if (!IsForMe(lines)) return;

            if (command == "DOCK") CmdDock(lines);
            else if (command == "REQUEST_LOAD") CmdRequestLoad(lines);
            else if (command == "REQUEST_UNLOAD") CmdRequestUnload(lines);
        }

        /// <summary>
        /// Seq_A_2 - The ship responds with its status
        /// Request:  REQUEST_STATUS
        /// Execute:  RESPONSE_STATUS
        /// </summary>
        void CmdRequestStatus(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");

            List<string> parts = new List<string>()
            {
                $"Command=RESPONSE_STATUS",
                $"To={from}",
                $"From={shipId}",
                $"Status={(int)shipStatus}",
                $"Cargo={CalculateCargoPercentage()}",
                $"Position={Utils.VectorToStr(remotePilot.GetPosition())}",
                $"StatusMessage={PrintShipStatus()}"
            };
            BroadcastMessage(parts);
        }

        /// <summary>
        /// Seq_xxxx / Seq_D_2a - SHIPX begins navigation to the specified connector and docks in LOADING or UNLOADING MODE.
        /// Execute:  START_LOADING / START_UNLOADING
        /// </summary>
        void CmdDock(string[] lines)
        {
            var task = (ExchangeTasks)Utils.ReadInt(lines, "Task");

            if (task == ExchangeTasks.Load)
            {
                AtmNavigationTriggerInit(
                    Utils.ReadInt(lines, "Landing") == 1,
                    config.AtmNavigationRoute.GetLoadWaypoints(remotePilot.GetPosition()),
                    new ExchangeInfo(lines),
                    task,
                    "START_LOADING");
            }
            else if (task == ExchangeTasks.Unload)
            {
                AtmNavigationTriggerInit(
                    Utils.ReadInt(lines, "Landing") == 1,
                    config.AtmNavigationRoute.GetUnLoadWaypoints(remotePilot.GetPosition()),
                    new ExchangeInfo(lines),
                    task,
                    "START_UNLOADING");
            }
        }

        /// <summary>
        /// A base calls the ship to load cargo.
        /// </summary>
        void CmdRequestLoad(string[] lines)
        {
            var bse = Utils.ReadString(lines, "From");
            var route = Utils.ReadVectorList(lines, "Route");

            StartTrip(bse, route, ExchangeTasks.Load);
        }
        /// <summary>
        /// A base calls the ship to unload cargo.
        /// </summary>
        void CmdRequestUnload(string[] lines)
        {
            var bse = Utils.ReadString(lines, "From");
            var route = Utils.ReadVectorList(lines, "Route");

            StartTrip(bse, route, ExchangeTasks.Unload);
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

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == shipId;
        }

        void UpdateShipStatus()
        {
            shipStatus = ShipStatus.Idle;

            if (alignData.HasTarget)
            {
                shipStatus = ShipStatus.Docking;
                return;
            }

            if (cruisingData.HasTarget)
            {
                shipStatus = ShipStatus.OnRoute;
                return;
            }

            if (navigationData.HasTarget)
            {
                if (navigationData.CurrentState == NavigationStatus.Accelerating ||
                    navigationData.CurrentState == NavigationStatus.Decelerating)
                {
                    shipStatus = ShipStatus.OnRoute;
                }
                else
                {
                    shipStatus = ShipStatus.Docking;
                }

                return;
            }
        }
        string PrintShipStatus()
        {
            var sb = new StringBuilder();

            PrintAlignStatus(sb);
            PrintArrivalStatus(sb);
            PrintCruiseStatus(sb);
            PrintAtmNavigationStatus(sb);

            return sb.ToString();
        }

        void PrintAlignStatus(StringBuilder sb)
        {
            if (!alignData.HasTarget)
            {
                return;
            }

            sb.AppendLine(alignData.GetAlignState());
        }
        void PrintArrivalStatus(StringBuilder sb)
        {
            if (!arrivalData.HasPosition)
            {
                return;
            }

            sb.AppendLine(arrivalData.GetArrivalState());
        }
        void PrintCruiseStatus(StringBuilder sb)
        {
            if (!cruisingData.HasTarget)
            {
                return;
            }

            sb.AppendLine(cruisingData.GetTripState());
        }
        void PrintAtmNavigationStatus(StringBuilder sb)
        {
            if (!navigationData.HasTarget)
            {
                return;
            }

            sb.AppendLine(navigationData.GetTripState());
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

        /// <summary>
        /// Set up the long trip
        /// </summary>
        void StartCruising(Vector3D position, List<Vector3D> waypoints, string onArrivalMessage)
        {
            cruisingData.Initialize(position, waypoints, onArrivalMessage);
        }

        void StartTrip(string baseName, List<Vector3D> waypoints, ExchangeTasks task)
        {
            //If the ship is docked, start departure from dock, and then start the cruise

            List<string> parts2 = new List<string>()
            {
                $"REQUEST_DEPARTURE",
                $"Ship={shipId}",
            };
            BroadcastMessage(parts2);

            List<string> parts = new List<string>()
            {
                $"REQUEST_DOCK",
                $"Base={baseName}",
                $"Task={(int)task}",
            };
            string message = string.Join("|", parts);

            StartCruising(remotePilot.GetPosition(), waypoints, message);
        }
        #endregion

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            alignData.LoadFromStorage(Utils.ReadString(storageLines, "AlignData"));
            arrivalData.LoadFromStorage(Utils.ReadString(storageLines, "ArrivalData"));
            cruisingData.LoadFromStorage(Utils.ReadString(storageLines, "NavigationData"));
            navigationData.LoadFromStorage(Utils.ReadString(storageLines, "AtmNavigationData"));
            paused = Utils.ReadInt(storageLines, "Paused", 0) == 1;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"AlignData={alignData.SaveToStorage()}",
                $"ArrivalData={arrivalData.SaveToStorage()}",
                $"NavigationData={cruisingData.SaveToStorage()}",
                $"AtmNavigationData={navigationData.SaveToStorage()}",
                $"Paused={(paused ? 1 : 0)}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
