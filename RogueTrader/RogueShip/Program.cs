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
    /// Rogue ship
    /// </summary>
    partial class Program : MyGridProgram
    {
        const string Version = "1.0";

        #region Blocks
        readonly IMyBroadcastListener bl;

        readonly IMyTimerBlock timerPilot;
        readonly IMyTimerBlock timerWaiting;

        readonly IMyTimerBlock timerDock;
        readonly IMyTimerBlock timerUndock;

        readonly IMyTimerBlock timerLoad;
        readonly IMyTimerBlock timerUnload;
        readonly IMyTimerBlock timerFinalize;

        readonly IMyRemoteControl remotePilot;
        readonly IMyRemoteControl remoteDocking;
        readonly IMyRemoteControl remoteLanding;

        readonly IMyCameraBlock cameraPilot;
        readonly IMyRadioAntenna antenna;
        readonly IMyShipConnector connectorA;

        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        readonly List<IMyCargoContainer> shipCargos = new List<IMyCargoContainer>();
        readonly List<IMyBatteryBlock> shipBatteries = new List<IMyBatteryBlock>();
        readonly List<IMyGasTank> shipTanks = new List<IMyGasTank>();

        readonly List<IMyTextSurface> infoLCDs = new List<IMyTextSurface>();
        readonly List<TextPanelDesc> logLCDs = new List<TextPanelDesc>();
        #endregion

        readonly string shipId;
        readonly StringBuilder sbLog = new StringBuilder();

        bool paused = false;
        bool monitorizePropulsion = false;
        readonly double minStoredPower = 0;
        readonly double minStoredHydrogen = 0;
        ShipStatus shipStatus = ShipStatus.Idle;
        readonly Navigator navigator;

        internal readonly Config Config;

        readonly TimeSpan refreshLCDsInterval = TimeSpan.FromSeconds(5);
        DateTime lastRefreshLCDs = DateTime.MinValue;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData = Config.GetDefault();

                Echo("CustomData not set.");
                return;
            }

            shipId = Me.CubeGrid.CustomName;
            Config = new Config(Me.CustomData);
            if (!Config.IsValid())
            {
                Echo(Config.GetErrors());
                return;
            }

            minStoredPower = Config.MinPower;
            minStoredHydrogen = Config.MinHydrogen;

            navigator = new Navigator(this);

            timerPilot = GetBlockWithName<IMyTimerBlock>(Config.TimerPilot);
            if (timerPilot == null)
            {
                Echo($"Timer '{Config.TimerPilot}' not found.");
                return;
            }
            timerWaiting = GetBlockWithName<IMyTimerBlock>(Config.TimerWaiting);
            if (timerWaiting == null)
            {
                Echo($"Timer '{Config.TimerWaiting}' not found.");
                return;
            }

            timerDock = GetBlockWithName<IMyTimerBlock>(Config.TimerDock);
            if (timerDock == null)
            {
                Echo($"Timer '{Config.TimerDock}' not found.");
                return;
            }
            timerUndock = GetBlockWithName<IMyTimerBlock>(Config.TimerUndock);
            if (timerUndock == null)
            {
                Echo($"Timer '{Config.TimerUndock}' not found.");
                return;
            }

            timerLoad = GetBlockWithName<IMyTimerBlock>(Config.TimerLoad);
            if (timerLoad == null)
            {
                Echo($"Timer '{Config.TimerLoad}' not found.");
                return;
            }
            timerUnload = GetBlockWithName<IMyTimerBlock>(Config.TimerUnload);
            if (timerUnload == null)
            {
                Echo($"Timer '{Config.TimerUnload}' not found.");
                return;
            }
            timerFinalize = GetBlockWithName<IMyTimerBlock>(Config.TimerFinalizeCargo);
            if (timerFinalize == null)
            {
                Echo($"Timer '{Config.TimerFinalizeCargo}' not found.");
                return;
            }

            remotePilot = GetBlockWithName<IMyRemoteControl>(Config.RemoteControlPilot);
            if (remotePilot == null)
            {
                Echo($"Remote Control '{Config.RemoteControlPilot}' not found.");
                return;
            }
            remoteDocking = GetBlockWithName<IMyRemoteControl>(Config.RemoteControlDocking);
            if (remoteDocking == null)
            {
                Echo($"Remote Control '{Config.RemoteControlDocking}' not found.");
                return;
            }
            remoteLanding = GetBlockWithName<IMyRemoteControl>(Config.RemoteControlLanding);
            if (remoteLanding == null)
            {
                Echo($"Remote Control '{Config.RemoteControlLanding}' not found. This ship is not available for landing.");
            }

            antenna = GetBlockWithName<IMyRadioAntenna>(Config.Antenna);
            if (antenna == null)
            {
                Echo($"Antenna {Config.Antenna} not found.");
                return;
            }
            connectorA = GetBlockWithName<IMyShipConnector>(Config.Connector);
            if (connectorA == null)
            {
                Echo($"Connector '{Config.Connector}' not found.");
                return;
            }
            cameraPilot = GetBlockWithName<IMyCameraBlock>(Config.Camera);
            if (cameraPilot == null)
            {
                Echo($"Camera {Config.Camera} not found.");
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

            shipCargos = GetBlocksOfType<IMyCargoContainer>();
            shipBatteries = GetBlocksOfType<IMyBatteryBlock>();
            shipTanks = GetBlocksOfType<IMyGasTank>();

            RefreshLCDs();

            WriteLCDs(Config.WildcardShipId, shipId);

            bl = IGC.RegisterBroadcastListener(Config.Channel);

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            WriteInfoLCDs($"RogueShip v{Version}", false);
            WriteInfoLCDs($"{shipId} in channel {Config.Channel}");
            WriteInfoLCDs($"{CalculateCargoPercentage():P1} cargo.");
            if (minStoredPower > 0) WriteInfoLCDs($"Battery {CalculateBatteryPercentage():P1}.");
            if (minStoredHydrogen > 0) WriteInfoLCDs($"Hydrogen {CalculateHydrogenPercentage():P1}.");
            WriteInfoLCDs($"{shipStatus}");

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

            if (DoPause()) return;

            MonitorizeLoad();

            UpdateShipStatus();
        }

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "PAUSE") Pause();
            else if (argument == "RESUME") Resume();
            else if (argument == "ENABLE_LOGS") EnableLogs();

            else if (argument == "START_ROUTE") SendWaitingMessage(ExchangeTasks.StartLoad);
        }

        /// <summary>
        /// Ship reset
        /// </summary>
        void Reset()
        {
            Storage = "";

            remotePilot.SetAutoPilotEnabled(false);
            remotePilot.ClearWaypoints();
            remoteDocking.SetAutoPilotEnabled(false);
            remoteDocking.ClearWaypoints();
            remoteLanding.SetAutoPilotEnabled(false);
            remoteLanding.ClearWaypoints();
            ResetGyros();
            ResetThrust();

            shipStatus = ShipStatus.Idle;
            navigator.Clear();

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
            Config.EnableLogs = !Config.EnableLogs;
        }
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = Utils.ReadArgument(lines, "Command");

            if (command == "REQUEST_STATUS") ProcessRequestStatus(lines);

            if (!IsForMe(lines)) return;

            if (command == "COME_TO_LOAD") ProcessDocking(lines, ExchangeTasks.StartLoad);
            if (command == "COME_TO_UNLOAD") ProcessDocking(lines, ExchangeTasks.StartUnload);
            if (command == "UNDOCK_TO_LOAD") ProcessDocking(lines, ExchangeTasks.EndLoad);
            if (command == "UNDOCK_TO_UNLOAD") ProcessDocking(lines, ExchangeTasks.EndUnload);
        }

        /// <summary>
        /// The ship responds with its status
        /// </summary>
        /// <remarks>
        /// Request:  REQUEST_STATUS
        /// Execute:  RESPONSE_STATUS
        /// </remarks>
        void ProcessRequestStatus(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");

            List<string> parts = new List<string>()
            {
                $"Command=RESPONSE_STATUS",
                $"To={from}",
                $"From={shipId}",
                $"Status={(int)shipStatus}",
                $"StatusMessage={GetShipState()}",
                $"Cargo={CalculateCargoPercentage()}",
                $"Position={Utils.VectorToStr(remotePilot.GetPosition())}",
            };
            BroadcastMessage(parts);
        }

        /// <summary>
        /// SHIP begins navigation to/from the specified connector and docks/undocks.
        /// </summary>
        void ProcessDocking(string[] lines, ExchangeTasks task)
        {
            var landing = Utils.ReadInt(lines, "Landing") == 1;

            string exchange = Utils.ReadString(lines, "Exchange");
            var fw = Utils.ReadVector(lines, "Forward");
            var up = Utils.ReadVector(lines, "Up");
            var wpList = Utils.ReadVectorList(lines, "Waypoints");

            if (task == ExchangeTasks.StartLoad || task == ExchangeTasks.StartUnload)
            {
                navigator.ApproachToDock(landing, exchange, fw, up, wpList, "ON_APPROACHING_COMPLETED", task);
                shipStatus = ShipStatus.Docking;
            }
            else if (task == ExchangeTasks.EndLoad || task == ExchangeTasks.EndUnload)
            {
                navigator.SeparateFromDock(landing, exchange, fw, up, wpList, "ON_SEPARATION_COMPLETED", task);
                shipStatus = ShipStatus.Undocking;
            }
        }
        #endregion

        #region PAUSE
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

        #region MONITORS
        void MonitorizeLoad()
        {
            if (shipStatus == ShipStatus.Loading)
            {
                double capacity = CalculateCargoPercentage();
                WriteInfoLCDs($"Progress {capacity / Config.MaxLoad:P1}...");
                if (capacity >= Config.MaxLoad)
                {
                    if (monitorizePropulsion && IsPropulsionFilled())
                    {
                        SendWaitingMessage(ExchangeTasks.EndLoad);
                        monitorizePropulsion = false;
                    }

                    timerFinalize?.StartCountdown();
                    monitorizePropulsion = true;
                }

                return;
            }

            if (shipStatus == ShipStatus.Unloading)
            {
                double capacity = CalculateCargoPercentage();
                WriteInfoLCDs($"Progress {1.0 - (capacity / Config.MaxLoad):P1}...");
                if (capacity <= Config.MinLoad)
                {
                    if (monitorizePropulsion && IsPropulsionFilled())
                    {
                        SendWaitingMessage(ExchangeTasks.EndUnload);
                        monitorizePropulsion = false;
                    }

                    timerFinalize?.StartCountdown();
                    monitorizePropulsion = true;
                }

                return;
            }
        }
        #endregion

        #region UPDATE SHIP STATUS
        void UpdateShipStatus()
        {
            navigator.Update();

            //Refresh LCDs every 60 seconds
            if (DateTime.Now - lastRefreshLCDs > refreshLCDsInterval)
            {
                RefreshLCDs();
            }
        }
        void RefreshLCDs()
        {
            infoLCDs.Clear();
            var info = GetBlocksOfType<IMyTextPanel>(Config.WildcardShipInfo);
            var infoCps = GetBlocksOfType<IMyCockpit>(Config.WildcardShipInfo).Where(c => Config.WildcardShipInfo.Match(c.CustomName).Groups[1].Success);
            infoLCDs.AddRange(info);
            infoLCDs.AddRange(infoCps.Select(c => c.GetSurface(int.Parse(Config.WildcardShipInfo.Match(c.CustomName).Groups[1].Value))));

            logLCDs.Clear();
            var log = GetBlocksOfType<IMyTextPanel>(Config.WildcardLogLCDs);
            var logCps = GetBlocksOfType<IMyCockpit>(Config.WildcardLogLCDs).Where(c => Config.WildcardLogLCDs.Match(c.CustomName).Groups[1].Success);
            logLCDs.AddRange(log.Select(l => new TextPanelDesc(l, l)));
            logLCDs.AddRange(logCps.Select(c => new TextPanelDesc(c, c.GetSurface(int.Parse(Config.WildcardLogLCDs.Match(c.CustomName).Groups[1].Value)))));

            lastRefreshLCDs = DateTime.Now;
        }
        #endregion

        #region CALLBACKS
        internal void ExecuteCallback(string name, ExchangeTasks task)
        {
            if (name == "ON_APPROACHING_COMPLETED") OnApproachingCompleted(task);
            else if (name == "ON_SEPARATION_COMPLETED") OnSeparationCompleted(task);
            else if (name == "ON_NAVIGATION_COMPLETED") OnNavigationCompleted(task);
        }
        void OnApproachingCompleted(ExchangeTasks task)
        {
            Dock();

            if (task == ExchangeTasks.StartLoad)
            {
                Load();
                shipStatus = ShipStatus.Loading;
            }
            else if (task == ExchangeTasks.StartUnload)
            {
                Unload();
                shipStatus = ShipStatus.Unloading;
            }
        }
        void OnSeparationCompleted(ExchangeTasks task)
        {
            shipStatus = ShipStatus.OnRoute;

            if (task == ExchangeTasks.EndLoad)
            {
                navigator.NavigateTo(Config.Route.UnloadBaseOnPlanet, Config.Route.ToUnloadBaseWaypoints, "ON_NAVIGATION_COMPLETED", task);
            }
            else if (task == ExchangeTasks.EndUnload)
            {
                navigator.NavigateTo(Config.Route.LoadBaseOnPlanet, Config.Route.ToLoadBaseWaypoints, "ON_NAVIGATION_COMPLETED", task);
            }
        }
        void OnNavigationCompleted(ExchangeTasks task)
        {
            EnableSystems();

            if (task == ExchangeTasks.EndLoad)
            {
                SendWaitingMessage(ExchangeTasks.StartUnload);
            }
            else if (task == ExchangeTasks.EndUnload)
            {
                SendWaitingMessage(ExchangeTasks.StartLoad);
            }
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
        List<T> GetBlocksOfType<T>(System.Text.RegularExpressions.Regex regEx) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && regEx.IsMatch(b.CustomName));
            return blocks;
        }

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == shipId;
        }

        internal void WriteLCDs(string wildcard, string text)
        {
            List<IMyTextPanel> lcds = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(lcds, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(wildcard));
            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, false);
            }
        }
        internal void WriteInfoLCDs(string text, bool append = true)
        {
            Echo(text);

            foreach (var lcd in infoLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text + Environment.NewLine, append);
            }
        }
        internal void WriteLogLCDs(string text)
        {
            if (!Config.EnableLogs)
            {
                return;
            }

            sbLog.Insert(0, text + Environment.NewLine);

            var log = sbLog.ToString();
            var logLines = log.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            foreach (var lcd in logLCDs)
            {
                lcd.Write(log, logLines);
            }
        }
        internal void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            WriteLogLCDs($"BroadcastMessage: {message}");

            IGC.SendBroadcastMessage(Config.Channel, message);
        }

        internal void Pilot()
        {
            timerPilot?.StartCountdown();
        }
        internal void Waiting()
        {
            timerWaiting?.StartCountdown();
        }

        internal void Dock()
        {
            timerDock?.StartCountdown();
        }
        internal void Undock()
        {
            timerUndock?.StartCountdown();
        }

        internal void Load()
        {
            timerLoad?.StartCountdown();
        }
        internal void Unload()
        {
            timerUnload?.StartCountdown();
        }

        internal bool IsObstacleAhead(double collisionDetectRange, Vector3D velocity, out MyDetectedEntityInfo hit)
        {
            cameraPilot.EnableRaycast = true;

            var cameraMatrixInv = MatrixD.Invert(cameraPilot.WorldMatrix);
            var localDirection = Vector3D.TransformNormal(Vector3D.Normalize(velocity), cameraMatrixInv);
            if (cameraPilot.CanScan(collisionDetectRange, localDirection))
            {
                hit = cameraPilot.Raycast(collisionDetectRange, localDirection);
                return
                    !hit.IsEmpty() &&
                    hit.Type != MyDetectedEntityType.Planet &&
                    Vector3D.Distance(hit.HitPosition.Value, cameraPilot.GetPosition()) <= collisionDetectRange;
            }

            hit = new MyDetectedEntityInfo();
            return false;
        }

        internal void ThrustToTarget(bool landing, Vector3D toTarget, double maxSpeed)
        {
            var velocity = landing ? GetLandingLinearVelocity() : GetPilotLinearVelocity();
            double mass = GetMass();

            var force = Utils.CalculateThrustForce(toTarget, maxSpeed, velocity, mass);
            ApplyThrust(force);
        }
        internal void ApplyThrust(Vector3D force)
        {
            foreach (var t in thrusters)
            {
                var thrustDir = t.WorldMatrix.Backward;
                double alignment = thrustDir.Dot(force);

                t.Enabled = true;
                t.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / t.MaxEffectiveThrust, 1f) : 0f;
            }
        }
        internal void ResetThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = true;
                t.ThrustOverridePercentage = 0f;
            }
        }
        internal void StopThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = false;
                t.ThrustOverridePercentage = 0;
            }
        }

        internal bool AlignToDirection(bool landing, Vector3D toTarget, double thr)
        {
            var remote = landing ? remoteLanding : remotePilot;
            var direction = remote.WorldMatrix.Forward;

            double angle = Utils.AngleBetweenVectors(direction, toTarget);
            WriteInfoLCDs($"TGT angle: {angle:F3}");

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
        internal bool AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipMatrix = remoteDocking.WorldMatrix;
            var shipForward = shipMatrix.Forward;
            var shipUp = shipMatrix.Up;

            double angleFW = Utils.AngleBetweenVectors(shipForward, targetForward);
            double angleUP = Utils.AngleBetweenVectors(shipUp, targetUp);
            WriteInfoLCDs($"TGT angles: {angleFW:F3} | {angleUP:F3}");

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

        internal void ApplyGyroOverride(Vector3D axis)
        {
            foreach (var g in gyros)
            {
                var localAxis = Vector3D.TransformNormal(axis, MatrixD.Transpose(g.WorldMatrix));
                var gyroRot = localAxis * -Config.GyrosSpeed;
                g.GyroOverride = true;
                g.Pitch = (float)gyroRot.X;
                g.Yaw = (float)gyroRot.Y;
                g.Roll = (float)gyroRot.Z;
            }
        }
        internal void ResetGyros()
        {
            foreach (var g in gyros)
            {
                g.GyroOverride = false;
                g.Pitch = 0;
                g.Yaw = 0;
                g.Roll = 0;
            }
        }

        internal Vector3D GetPosition()
        {
            return remotePilot.GetPosition();
        }
        internal double GetSpeed()
        {
            return remotePilot.GetShipSpeed();
        }
        internal Vector3D GetGravity()
        {
            return remotePilot.GetNaturalGravity();
        }
        internal bool IsInGravity()
        {
            return GetGravity().Length() > 0.001;
        }
        internal double GetMass()
        {
            return remotePilot.CalculateShipMass().TotalMass;
        }

        internal Vector3D GetPilotLinearVelocity()
        {
            return remotePilot.GetShipVelocities().LinearVelocity;
        }
        internal Vector3D GetLandingLinearVelocity()
        {
            return remoteLanding.GetShipVelocities().LinearVelocity;
        }

        internal bool IsConnected()
        {
            return connectorA.Status == MyShipConnectorStatus.Connected;
        }
        internal bool IsNearConnector()
        {
            return
                connectorA.Status != MyShipConnectorStatus.Unconnected;
        }
        internal Vector3D GetDockingPosition()
        {
            return connectorA.GetPosition();
        }
        internal Vector3D GetDockingLinearVelocity()
        {
            return remoteDocking.GetShipVelocities().LinearVelocity;
        }
        internal string GetDockedGridName()
        {
            return connectorA.OtherConnector?.CubeGrid?.CustomName;
        }

        internal IMyCameraBlock GetCameraPilot()
        {
            return cameraPilot;
        }

        internal void EnableSystems()
        {
            if (antenna.Enabled)
            {
                return;
            }
            antenna.Enabled = true;
        }
        internal void DisableSystems()
        {
            if (!antenna.Enabled)
            {
                return;
            }
            antenna.Enabled = false;
        }

        bool IsPropulsionFilled()
        {
            if (!IsBatteryFilled())
            {
                WriteInfoLCDs($"Battery {CalculateBatteryPercentage():P1}.");
                return false;
            }
            if (!IsHydrogenFilled())
            {
                WriteInfoLCDs($"Hydrogen {CalculateHydrogenPercentage():P1}.");
                return false;
            }
            return true;
        }
        bool IsBatteryFilled()
        {
            if (minStoredPower <= 0)
            {
                return true;
            }

            double battery = CalculateBatteryPercentage();
            return battery >= minStoredPower;
        }
        bool IsHydrogenFilled()
        {
            if (minStoredHydrogen <= 0)
            {
                return true;
            }
            double hydrogen = CalculateHydrogenPercentage();
            return hydrogen >= minStoredHydrogen;
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
        double CalculateBatteryPercentage()
        {
            if (shipBatteries.Count == 0)
            {
                return 0;
            }

            double max = 0;
            double curr = 0;
            foreach (var battery in shipBatteries)
            {
                max += (double)battery.MaxStoredPower;
                curr += (double)battery.CurrentStoredPower;
            }

            return curr / max;
        }
        double CalculateHydrogenPercentage()
        {
            if (shipTanks.Count == 0)
            {
                return 0;
            }

            double max = 0;
            double curr = 0;
            foreach (var tank in shipTanks)
            {
                if (!tank.BlockDefinition.SubtypeId.Contains("Hydrogen"))
                {
                    continue;
                }

                max += tank.Capacity;
                curr += tank.Capacity * tank.FilledRatio;
            }

            return curr / max;
        }

        void SendWaitingMessage(ExchangeTasks task)
        {
            string message;
            string to;

            if (task == ExchangeTasks.StartLoad || task == ExchangeTasks.StartUnload)
            {
                message = task == ExchangeTasks.StartLoad ? "WAITING_LOAD" : "WAITING_UNLOAD";
                to = task == ExchangeTasks.StartLoad ? Config.Route.LoadBase : Config.Route.UnloadBase;

                shipStatus = ShipStatus.WaitingDock;
            }
            else if (task == ExchangeTasks.EndLoad || task == ExchangeTasks.EndUnload)
            {
                message = task == ExchangeTasks.EndLoad ? "WAITING_UNDOCK_LOAD" : "WAITING_UNDOCK_UNLOAD";
                to = task == ExchangeTasks.EndLoad ? Config.Route.LoadBase : Config.Route.UnloadBase;

                shipStatus = ShipStatus.WaitingUndock;
            }
            else
            {
                WriteLogLCDs($"Unknown task: {task}");
                return;
            }

            List<string> parts = new List<string>()
            {
                $"Command={message}",
                $"To={to}",
                $"From={shipId}",
            };
            BroadcastMessage(parts);

            Waiting();
        }

        string GetShipState()
        {
            if (shipStatus == ShipStatus.Idle)
            {
                string dockedGrid = GetDockedGridName();
                return string.IsNullOrWhiteSpace(dockedGrid) ? "" : dockedGrid;
            }
            else if (shipStatus == ShipStatus.Loading)
            {
                double capacity = CalculateCargoPercentage();
                return $"Loading from {GetDockedGridName()} {capacity / Config.MaxLoad:P1}...";
            }
            else if (shipStatus == ShipStatus.Unloading)
            {
                double capacity = CalculateCargoPercentage();
                return $"Unloading to {GetDockedGridName()} {1.0 - (capacity / Config.MaxLoad):P1}...";
            }
            else
            {
                return navigator.GetShortState();
            }
        }
        #endregion

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            paused = Utils.ReadInt(storageLines, "Paused", 0) == 1;
            monitorizePropulsion = Utils.ReadInt(storageLines, "MonitorizePropulsion", 0) == 1;
            shipStatus = (ShipStatus)Utils.ReadInt(storageLines, "ShipStatus", (int)ShipStatus.Idle);
            navigator.LoadFromStorage(Utils.ReadString(storageLines, "Navigator"));
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Paused={(paused ? 1 : 0)}",
                $"MonitorizePropulsion={(monitorizePropulsion ? 1 : 0)}",
                $"ShipStatus={(int)shipStatus}",
                $"Navigator={navigator.SaveToStorage()}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
