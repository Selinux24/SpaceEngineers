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
    /// TODO: Cuando se quede a un porcentaje de batería, parar y esperar a que se recargue
    /// TODO: Cuando esté en espera de cargarse, al llegar a un porcentaje de batería, continuar el viaje
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
        readonly NavigationData navigationData;
        readonly AtmNavigationData atmNavigationData;

        ShipStatus status = ShipStatus.Idle;
        bool paused = false;
        bool enableLogs = true;

        int alignTickCount = 0;
        string alignStateMsg;

        int arrivalTickCount = 0;
        string arrivalStateMsg;

        int navigationTickCount = 0;
        string navigationStateMsg;

        int atmNavigationTickCount = 0;
        string atmNavigationStateMsg;

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
            arrivalData = new ArrivalData();
            navigationData = new NavigationData();
            atmNavigationData = new AtmNavigationData();

            timerPilot = GetBlockWithName<IMyTimerBlock>(config.ShipTimerPilot);
            if (timerPilot == null)
            {
                Echo($"Timer '{config.ShipTimerPilot}' no locallizado.");
                return;
            }
            timerLock = GetBlockWithName<IMyTimerBlock>(config.ShipTimerLock);
            if (timerLock == null)
            {
                Echo($"Timer de atraque '{config.ShipTimerLock}' no locallizado.");
                return;
            }
            timerUnlock = GetBlockWithName<IMyTimerBlock>(config.ShipTimerUnlock);
            if (timerUnlock == null)
            {
                Echo($"Timer de separación '{config.ShipTimerUnlock}' no locallizado.");
                return;
            }
            timerLoad = GetBlockWithName<IMyTimerBlock>(config.ShipTimerLoad);
            if (timerLoad == null)
            {
                Echo($"Timer de carga '{config.ShipTimerLoad}' no locallizado.");
                return;
            }
            timerUnload = GetBlockWithName<IMyTimerBlock>(config.ShipTimerUnload);
            if (timerUnload == null)
            {
                Echo($"Timer de descarga '{config.ShipTimerUnload}' no locallizado.");
                return;
            }
            timerWaiting = GetBlockWithName<IMyTimerBlock>(config.ShipTimerWaiting);
            if (timerWaiting == null)
            {
                Echo($"Timer de espera '{config.ShipTimerWaiting}' no locallizado.");
                return;
            }

            remotePilot = GetBlockWithName<IMyRemoteControl>(config.ShipRemoteControlPilot);
            if (remotePilot == null)
            {
                Echo($"Control remoto de pilotaje '{config.ShipRemoteControlPilot}' no locallizado.");
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
            DoNavigation();
            DoAtmNavigation();
        }

        #region ALIGN
        void DoAlign()
        {
            if (!alignData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(alignStateMsg)) Echo(alignStateMsg);

            if (++alignTickCount < config.AlignTicks)
            {
                return;
            }
            alignTickCount = 0;

            MonitorizeAlign();
        }
        void MonitorizeAlign()
        {
            if (alignData.CurrentTarget >= alignData.Waypoints.Count)
            {
                alignStateMsg = "Destination reached.";
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

            if (distance < config.ExchangeWaypointDistanceThr)
            {
                alignData.Next();
                ResetThrust();
                alignStateMsg = "Waypoint reached. Moving to the next.";
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

            if (!string.IsNullOrWhiteSpace(arrivalStateMsg)) Echo(arrivalStateMsg);

            if (++arrivalTickCount < config.ArrivalTicks)
            {
                return;
            }
            arrivalTickCount = 0;

            MonitorizeArrival();
        }
        void MonitorizeArrival()
        {
            double distance;
            if (arrivalData.Arrived(remotePilot.GetPosition(), out distance))
            {
                arrivalStateMsg = "Destination reached.";
                ParseTerminalMessage(arrivalData.Command);
                arrivalData.Clear();
                ResetGyros();
                ResetThrust();
                return;
            }

            arrivalStateMsg = $"Distance to destination: {Utils.DistanceToStr(distance)}";
        }
        #endregion

        #region NAVIGATOR
        void DoNavigation()
        {
            if (!navigationData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(navigationStateMsg)) Echo(navigationStateMsg);

            if (++navigationTickCount < config.NavigationTicks)
            {
                return;
            }
            navigationTickCount = 0;

            MonitorizeNavigation();
        }
        void MonitorizeNavigation()
        {
            navigationData.UpdatePositionAndVelocity(cameraPilot.GetPosition(), remotePilot.GetShipSpeed());

            navigationStateMsg = $"Navigation state {navigationData.CurrentState}";

            WriteInfoLCDs($"Trip: {Utils.DistanceToStr(navigationData.TotalDistance)}");
            WriteInfoLCDs($"To target: {Utils.DistanceToStr(navigationData.DistanceToTarget)}");
            WriteInfoLCDs($"ETC: {navigationData.EstimatedArrival:hh\\:mm\\:ss}");
            WriteInfoLCDs($"Speed: {navigationData.Speed:F2}");
            WriteInfoLCDs($"Progress {navigationData.Progress:P1}");
            WriteInfoLCDs(navigationData.PrintObstacle());

            if (DoPause()) return;

            switch (navigationData.CurrentState)
            {
                case NavigationStatus.Distress:
                    Distress();
                    break;

                case NavigationStatus.Locating:
                    Locate();
                    break;
                case NavigationStatus.Accelerating:
                    Accelerate();
                    break;
                case NavigationStatus.Cruising:
                    Cruise();
                    break;
                case NavigationStatus.Braking:
                    Brake();
                    break;

                case NavigationStatus.Avoiding:
                    Avoid();
                    break;
            }
        }
        void Distress()
        {
            if (beacon != null) beacon.Enabled = true;
            BroadcastStatus($"DISTRESS: Engines damaged!, waiting in position {Utils.VectorToStr(remotePilot.GetPosition())}");
            ResetThrust();
            ResetGyros();
        }
        void Locate()
        {
            if (AlignToDirection(remotePilot.WorldMatrix.Forward, navigationData.DirectionToTarget, config.CruisingLocateAlignThr))
            {
                BroadcastStatus("Destination located. Initializing acceleration.");
                navigationData.CurrentState = NavigationStatus.Accelerating;

                return;
            }

            ResetThrust();
        }
        void Accelerate()
        {
            if (navigationData.IsObstacleAhead(cameraPilot, config.CrusingCollisionDetectRange, remotePilot.GetShipVelocities().LinearVelocity))
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                navigationData.CurrentState = NavigationStatus.Avoiding;
                return;
            }

            if (navigationData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Braking.");
                navigationData.CurrentState = NavigationStatus.Braking;

                return;
            }

            bool inGravity = IsShipInGravity();
            var shipVelocity = remotePilot.GetShipVelocities().LinearVelocity.Length();
            if (!inGravity && shipVelocity >= config.CruisingMaxSpeed * config.CruisingMaxSpeedThr)
            {
                BroadcastStatus("Reached cruise speed. Deactivating thrusters.");
                navigationData.CurrentState = NavigationStatus.Cruising;
                return;
            }

            // Acelerar
            var maxSpeed = config.CruisingMaxSpeed;
            if (Vector3D.Distance(navigationData.Origin, cameraPilot.GetPosition()) <= config.CruisingToBasesDistanceThr)
            {
                maxSpeed = config.CruisingMaxAccelerationSpeed;
            }
            ThrustToTarget(remotePilot, navigationData.DirectionToTarget, maxSpeed);
        }
        void Cruise()
        {
            if (navigationData.IsObstacleAhead(cameraPilot, config.CrusingCollisionDetectRange, remotePilot.GetShipVelocities().LinearVelocity))
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                navigationData.CurrentState = NavigationStatus.Avoiding;

                return;
            }

            if (navigationData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Braking.");
                navigationData.CurrentState = NavigationStatus.Braking;

                return;
            }

            // Mantener velocidad
            bool inGravity = IsShipInGravity();
            if (inGravity || !AlignToDirection(remotePilot.WorldMatrix.Forward, navigationData.DirectionToTarget, config.CruisingCruiseAlignThr))
            {
                // Encender los propulsores hasta alinear de nuevo el vector velocidad con el vector hasta el objetivo
                ThrustToTarget(remotePilot, navigationData.DirectionToTarget, config.CruisingMaxSpeed);
                navigationData.AlignThrustStart = DateTime.Now;
                navigationData.Thrusting = true;

                return;
            }

            if (navigationData.Thrusting)
            {
                // Propulsores encendidos para recuperar la alineación
                if (!inGravity && (DateTime.Now - navigationData.AlignThrustStart).TotalSeconds > config.CruisingThrustAlignSeconds)
                {
                    // Fuera de la gravedad y tiempo de alineación consumido. Desactivar propulsores
                    EnterCruising();
                    navigationData.Thrusting = false;
                }

                return;
            }

            var shipVelocity = remotePilot.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity > config.CruisingMaxSpeed)
            {
                // Velocidad máxima superada. Encender propulsores en neutro para frenar
                ResetThrust();
                ResetGyros();

                return;
            }

            if (shipVelocity < config.CruisingMaxSpeed * config.CruisingMaxSpeedThr)
            {
                // Por debajo de la velocidad deseada. Acelerar hasta alcanzarla
                ThrustToTarget(remotePilot, navigationData.DirectionToTarget, config.CruisingMaxSpeed);

                return;
            }

            EnterCruising();
        }
        void Brake()
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
                ParseTerminalMessage(navigationData.Command);
                navigationData.Clear();
            }
        }
        void Avoid()
        {
            if (navigationData.DistanceToTarget < config.CruisingToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Braking.");
                navigationData.CurrentState = NavigationStatus.Braking;

                return;
            }

            // Calcular los puntos de evasión
            if (!navigationData.CalculateEvadingWaypoints(cameraPilot, config.CrusingCollisionDetectRange * 0.5))
            {
                //No se puede calcular un punto de evasión
                navigationData.CurrentState = NavigationStatus.Braking;

                return;
            }

            // Navegar entre los puntos de evasión
            if (navigationData.EvadingPoints.Count > 0)
            {
                EvadingTo(navigationData.EvadingPoints[0], config.CrusingEvadingMaxSpeed);

                if (navigationData.EvadingPoints.Count == 0)
                {
                    // Limpiar la información del obstáculo
                    navigationData.ClearObstacle();

                    ResetThrust();

                    // Volver a navegación cuando se alcance el último punto de navegación
                    navigationData.CurrentState = NavigationStatus.Locating;
                }

                return;
            }
        }
        void EvadingTo(Vector3D wayPoint, double maxSpeed)
        {
            var toTarget = wayPoint - cameraPilot.GetPosition();
            var d = toTarget.Length();
            if (d <= config.CrusingEvadingWaypointDistance)
            {
                // Waypoint alcanzado
                navigationData.EvadingPoints.RemoveAt(0);

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
        bool IsShipInGravity()
        {
            var gravitry = remotePilot.GetNaturalGravity();
            return gravitry.Length() >= 0.001;
        }
        #endregion

        #region ATMOSPHERIC NAVIGATION
        void DoAtmNavigation()
        {
            if (!atmNavigationData.HasTarget)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(atmNavigationStateMsg)) Echo(atmNavigationStateMsg);

            if (++atmNavigationTickCount < config.AtmNavigationTicks)
            {
                return;
            }
            atmNavigationTickCount = 0;

            MonitorizeAtmNavigation();
        }
        void MonitorizeAtmNavigation()
        {
            var remote = (atmNavigationData.Landing ? remoteLanding : remotePilot) ?? remotePilot;

            atmNavigationData.UpdatePositionAndVelocity(remote.GetPosition(), remote.GetShipSpeed());

            WriteInfoLCDs($"Navigation state {atmNavigationData.CurrentState}");
            WriteInfoLCDs($"Trip: {Utils.DistanceToStr(atmNavigationData.TotalDistance)}");
            WriteInfoLCDs($"To target: {Utils.DistanceToStr(atmNavigationData.DistanceToTarget)}");
            WriteInfoLCDs($"Speed: {atmNavigationData.Speed:F2}");
            WriteInfoLCDs($"ETC: {atmNavigationData.EstimatedArrival:hh\\:mm\\:ss}");
            WriteInfoLCDs($"Progress {atmNavigationData.Progress:P1}");
            WriteInfoLCDs(errDebug);

            if (DoPause()) return;

            switch (atmNavigationData.CurrentState)
            {
                case AtmNavigationStatus.Undocking:
                    AtmNavigationUndock();
                    break;
                case AtmNavigationStatus.Separating:
                    AtmNavigationSeparate(remote);
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
            }
        }
        string errDebug = null;
        void AtmNavigationUndock()
        {
            if (connectorA.Status == MyShipConnectorStatus.Connected)
            {
                atmNavigationStateMsg = "Waiting for connector to unlock.";
                return;
            }

            try
            {
                //Find a postion backward the remote control, separated from the connector
                var toTarget = remoteAlign.WorldMatrix.Backward;
                double mass = remoteAlign.CalculateShipMass().PhysicalMass;
                var force = Utils.CalculateThrustForce(toTarget, 100, Vector3D.Zero, mass);
                ApplyThrust(force);
            }
            catch (Exception ex)
            {
                errDebug = $"Error undocking: {ex.Message}";
                paused = true;
                return;
            }

            atmNavigationStateMsg = "Connector unlocked. Separating.";
            atmNavigationData.CurrentState = AtmNavigationStatus.Separating;
        }
        void AtmNavigationSeparate(IMyRemoteControl remote)
        {
            ResetThrust();
            ResetGyros();
            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity > 0.1)
            {
                atmNavigationStateMsg = "Separating.";
                return;
            }

            BroadcastStatus("Separated. Accelerating...");
            atmNavigationData.CurrentState = AtmNavigationStatus.Accelerating;
        }
        void AtmNavigationAccelerate(IMyRemoteControl remote)
        {
            AlignToDirection(remote.WorldMatrix.Forward, atmNavigationData.DirectionToTarget, config.AtmNavigationAlignThr);

            if (atmNavigationData.DistanceToTarget < config.AtmNavigationToTargetDistanceThr)
            {
                BroadcastStatus("Destination reached. Decelerating.");
                atmNavigationData.CurrentState = AtmNavigationStatus.Decelerating;
                return;
            }

            // Acelerar
            ThrustToTarget(remote, atmNavigationData.DirectionToTarget, config.AtmNavigationMaxSpeed);
        }
        void AtmNavigationDecelerate(IMyRemoteControl remote)
        {
            ResetThrust();
            ResetGyros();
            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity <= 0.1)
            {
                BroadcastStatus("Parking reached. Aproaching to dock...");
                atmNavigationData.CurrentState = AtmNavigationStatus.Docking;

                alignData.Initialize(
                    atmNavigationData.ExchangeForward,
                    atmNavigationData.ExchangeUp,
                    atmNavigationData.ExchangeApproachingWaypoints,
                    atmNavigationData.Command);

                atmNavigationData.Clear();
            }
        }
        void AtmNavigationDock()
        {
            if (connectorA.Status != MyShipConnectorStatus.Connected)
            {
                atmNavigationStateMsg = "Waiting for connector to lock.";
                return;
            }

            atmNavigationStateMsg = "Connector locked. Navigation finished.";
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
        /// Reset de la nave
        /// </summary>
        void Reset()
        {
            Storage = "";

            status = ShipStatus.Idle;

            deliveryData.Clear();
            alignData.Clear();
            arrivalData.Clear();
            navigationData.Clear();
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
        /// Pausa todas las tareas de navegación
        /// </summary>
        void Pause()
        {
            paused = true;
        }
        /// <summary>
        /// Continua todas las tareas de navegación
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
        /// Goes to a position defined in the argument.
        /// </summary>
        void Goto(string argument)
        {
            string[] lines = argument.Split('|');
            var position = Utils.ReadVector(lines, "Position");

            StartCruising(position, "WAITING");
        }
        /// <summary>
        /// Realiza la aproximación al parking destino cargado en delivery data
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
                //Carga en el piloto automático
                StartAutoPilot(destination, destinationName, config.ExchangeMaxApproachingSpeed, onArrival);
            }
            else
            {
                //Carga en el modo crucero
                StartCruising(destination, onArrival);
            }
        }

        /// <summary>
        /// Sec_xxxx - NAVEX llega a Parking_WH y solicita permiso para cargar
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

            timerWaiting.ApplyAction("Start");

            status = ShipStatus.WaitingForLoad;
        }
        /// <summary>
        /// Sec_C_2b - Cuando la nave llega al conector de carga, informa a la base para comenzar la carga
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

            //Atraque
            timerLock.ApplyAction("Start");

            //Activar modo carga
            timerLoad.ApplyAction("Start");

            status = ShipStatus.Loading;
        }

        /// <summary>
        /// Sec_C_4c - NAVEX llega a Parking_BASEX y solicita permiso para descargar
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

            timerWaiting.ApplyAction("Start");

            status = ShipStatus.WaitingForUnload;
        }
        /// <summary>
        /// Sec_D_2b - NAVEX avisa a BASEX que ha llegado para descargar el ID_PEDIDO en el conector. Lanza [UNLOADING] a BASEX
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

            //Atraque
            timerLock.ApplyAction("Start");

            //Activar modo descarga
            timerUnload.ApplyAction("Start");

            status = ShipStatus.Unloading;
        }
        /// <summary>
        /// Sec_D_2d - NAVEX informa del fin de la descarga a BASEX, empieza el camino de vuelta a Parking_WH
        /// Execute:  UNLOADED - Informa a BASEX
        /// Execute:  APPROACH_TO_PARKING - Cuando termina el recorrido de los waypoints de salida, comienza el viaje a Parking_WH
        /// Execute:  WAITING - Cuando llega a Parking_WH, se queda en espera.
        /// </summary>
        void UnloadFinished()
        {
            if (!deliveryData.Active)
            {
                return;
            }

            //Aviso de descarga finalizada a base
            List<string> parts = new List<string>()
            {
                $"Command=UNLOADED",
                $"To={deliveryData.OrderCustomer}",
                $"From={shipId}",
                $"Order={deliveryData.OrderId}",
                $"Warehouse={deliveryData.OrderWarehouse}"
            };
            BroadcastMessage(parts);

            //Carga el viaje de vuelta al Warehouse
            //Monitorizará el viaje hasta la posición de espera del Warehouse, ejecutará WAITING, y esperará instrucciones de la base
            deliveryData.PrepareNavigationToWarehouse("WAITING");

            //Carga la ruta de salida y al llegar al último waypoint del conector
            //Ejecutará APPROACH_TO_PARKING, que activará la navegación al destino cargado previamente
            deliveryData.PrepareNavigationFromExchange("APPROACH_TO_PARKING");

            Depart();

            status = ShipStatus.RouteToLoad;
        }

        /// <summary>
        /// Sec_D_2f - NAVEX se queda en espera
        /// </summary>
        void Waiting()
        {
            //Pone la nave en espera
            timerWaiting.ApplyAction("Start");

            //Se produce cuando la nave llega al último waypoint de la ruta de entrega
            deliveryData.Clear();

            status = ShipStatus.Idle;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de los logs
        /// </summary>
        void EnableLogs()
        {
            enableLogs = !enableLogs;
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
        /// Sec_A_2 - La nave responde con su estado
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
                $"Speed={speed:F2}",
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// Sec_C_2a - NAVEX registra el pedido, comienza la navegación al parking especificado y pide un exchange para atracar.
        /// Request:  START_DELIVERY
        /// Execute:  REQUEST_LOAD_TO_WAREHOUSE cuando la nave alcance el parking del WH
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
        /// Sec_xxxx / Sec_D_2a - NAVEX comienza la navegación al conector especificado y atraca en MODO CARGA o DESCARGA.
        /// Execute:  START_LOADING o START_UNLOADING
        /// </summary>
        /// <param name="lines"></param>
        void CmdDock(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != shipId)
            {
                return;
            }

            ExchangeTasks task = (ExchangeTasks)Utils.ReadInt(lines, "Task");

            if (task == ExchangeTasks.Load)
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
            else if (task == ExchangeTasks.Unload)
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
                    Utils.ReadVectorList(lines, "WayPoints"));

                timerUnlock?.ApplyAction("Start");
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
                    Utils.ReadVectorList(lines, "WayPoints"));

                timerUnlock?.ApplyAction("Start");
            }
        }

        /// <summary>
        /// Sec_C_4a - NAVEX carga la ruta hasta Parking_BASEX y comienza la maniobra de salida desde el conector de WH
        /// Request:  LOADED
        /// Execute:  APPROACH_TO_PARKING cuando NAVEX llegue al último waypoint de la ruta de salida del conector
        /// Execute:  REQUEST_UNLOAD_TO_CUSTOMER cuando NAVEX llegue a Parking_BASEX
        /// </summary>
        void CmdLoaded(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.RouteToUnload;

            //Carga la ruta de salida y al llegar al último waypoint del conector, ejecutará APPROACH_TO_PARKING, que activará la navegación al Customer
            deliveryData.PrepareNavigationFromExchange("APPROACH_TO_PARKING");

            //Monitorizará el viaje hasta la posición de espera del Customer, ejecutará REQUEST_UNLOAD_TO_CUSTOMER, y esperará instrucciones de la base
            deliveryData.PrepareNavigationToCustomer("REQUEST_UNLOAD_TO_CUSTOMER");

            Depart();
        }

        /// <summary>
        /// Realiza la maniobra de aproximación desde cualquier posición
        /// </summary>
        void ApproachToExchange()
        {
            //Obtener la distancia al primer punto de aproximación
            var shipPosition = remoteAlign.GetPosition();
            var wp = deliveryData.Waypoints[0];
            double distance = Vector3D.Distance(shipPosition, wp);
            if (distance > 500)
            {
                //Carga en el piloto automático hasta la posición del primer waypoint
                StartAutoPilot(wp, "Path to Connector", config.ExchangeMaxApproachingSpeed, "START_APPROACH");
            }
            else
            {
                StartApproach();
            }
        }
        /// <summary>
        /// Realiza la maniobra de desacople y viaja hasta el destino
        /// </summary>
        void Depart()
        {
            timerUnlock?.ApplyAction("Start");

            //Comienza la maniobra de salida
            alignData.Initialize(deliveryData.AlignFwd, deliveryData.AlignUp, deliveryData.Waypoints, deliveryData.OnLastWaypoint);
        }
        /// <summary>
        /// Recorre los waypoints
        /// </summary>
        void StartApproach()
        {
            remotePilot.SetAutoPilotEnabled(false);
            remotePilot.ClearWaypoints();

            alignData.Initialize(deliveryData.AlignFwd, deliveryData.AlignUp, deliveryData.Waypoints, deliveryData.OnLastWaypoint);
        }
        /// <summary>
        /// Configura el piloto automático
        /// </summary>
        void StartAutoPilot(Vector3D destination, string destinationName, double velocity, string onArrival)
        {
            arrivalData.Initialize(destination, config.ExchangeDistanceThr, onArrival);

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
        /// Configura el viaje largo
        /// </summary>
        void StartCruising(Vector3D destination, string onArrival)
        {
            navigationData.Initialize(cameraPilot.GetPosition(), destination, onArrival);
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
            if (!enableLogs)
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
            navigationData.LoadFromStorage(Utils.ReadString(storageLines, "NavigationData"));
            atmNavigationData.LoadFromStorage(Utils.ReadString(storageLines, "AtmNavigationData"));
            enableLogs = Utils.ReadInt(storageLines, "EnableLogs") == 1;
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
                $"NavigationData={navigationData.SaveToStorage()}",
                $"AtmNavigationData={atmNavigationData.SaveToStorage()}",
                $"EnableLogs={(enableLogs ? 1 : 0)}",
                $"Paused={(paused ? 1 : 0)}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
