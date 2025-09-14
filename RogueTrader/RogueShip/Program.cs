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
        const string Version = "1.8";

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
        internal readonly Config Config;

        readonly StringBuilder sbLog = new StringBuilder();

        bool paused = false;
        bool monitorizeCapacity = false;
        bool monitorizePropulsion = false;
        ShipStatus shipStatus = ShipStatus.Idle;
        readonly Navigator navigator;

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

            bl = IGC.RegisterBroadcastListener(Config.Channel);

            LoadFromStorage();

            lastRefreshLCDs = DateTime.MinValue + TimeSpan.FromTicks(Me.EntityId);

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            WriteInfoLCDs($"RogueShip v{Version}. {DateTime.Now:HH:mm:ss}", false);
            WriteInfoLCDs($"{shipId} in channel {Config.Channel}");
            WriteInfoLCDs($"{CalculateCargoPercentage():P1} cargo.");
            if (Config.MinPowerOnLoad > 0 || Config.MinPowerOnUnload > 0) WriteInfoLCDs($"Battery {CalculateBatteryPercentage():P1}.");
            if (Config.MinHydrogenOnLoad > 0 || Config.MinHydrogenOnUnload > 0) WriteInfoLCDs($"Hydrogen {CalculateHydrogenPercentage():P1}.");
            WriteInfoLCDs($"{shipStatus}");
            if (Config.EnableRefreshLCDs) WriteInfoLCDs($"Next LCDs Refresh {GetNextLCDsRefresh():hh\\:mm\\:ss}");

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

            UpdateShipStatus();

            MonitorizeLoad();
        }

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "PAUSE") Pause();
            else if (argument == "RESUME") Resume();

            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "ENABLE_REFRESH_LCDS") EnableRefreshLCDs();

            else if (argument == "START_ROUTE") SendWaitingMessage(ExchangeTasks.StartLoad);
            else if (argument == "START_LOAD") SendWaitingMessage(ExchangeTasks.StartLoad);
            else if (argument == "START_UNLOAD") SendWaitingMessage(ExchangeTasks.StartUnload);

            else if (argument == "NEXT") Next();
            else if (argument == "UNDOCK") StopAndUndocK();
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
        /// <summary>
        /// Changes the state of the variable that controls the refresh of LCDs
        /// </summary>
        void EnableRefreshLCDs()
        {
            Config.EnableRefreshLCDs = !Config.EnableRefreshLCDs;
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
            string msg = null;

            if (shipStatus == ShipStatus.Loading)
            {
                double capacity = CalculateCargoPercentage();
                WriteInfoLCDs($"Progress {capacity / Config.MaxLoad:P1}...");

                if (monitorizeCapacity && capacity < Config.MaxLoad)
                {
                    return;
                }
                monitorizeCapacity = false;

                if (!monitorizePropulsion)
                {
                    timerFinalize?.StartCountdown();
                    monitorizePropulsion = true;
                    return;
                }

                if (IsPropulsionFilled(shipStatus, out msg))
                {
                    SendWaitingMessage(ExchangeTasks.EndLoad);
                    monitorizePropulsion = false;
                }
            }
            else if (shipStatus == ShipStatus.Unloading)
            {
                double capacity = CalculateCargoPercentage();
                WriteInfoLCDs($"Progress {1.0 - (capacity / Config.MaxLoad):P1}...");

                if (monitorizeCapacity && capacity > Config.MinLoad)
                {
                    return;
                }
                monitorizeCapacity = false;

                if (!monitorizePropulsion)
                {
                    timerFinalize?.StartCountdown();
                    monitorizePropulsion = true;
                    return;
                }

                if (IsPropulsionFilled(shipStatus, out msg))
                {
                    SendWaitingMessage(ExchangeTasks.EndUnload);
                    monitorizePropulsion = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(msg)) WriteInfoLCDs(msg);
        }
        #endregion

        #region UPDATE SHIP STATUS
        void UpdateShipStatus()
        {
            navigator.Update();

            DoRefreshLCDs();
        }
        void DoRefreshLCDs()
        {
            if (!Config.EnableRefreshLCDs)
            {
                return;
            }

            if (DateTime.Now - lastRefreshLCDs > Config.RefreshLCDsInterval)
            {
                RefreshLCDs();
            }
        }
        void RefreshLCDs()
        {
            infoLCDs.Clear();
            var info = GetBlocksOfType<IMyTextPanel>(Config.WildcardShipInfo);
            var infoCps = GetBlocksImplementType<IMyTextSurfaceProvider>(Config.WildcardShipInfo).Where(c => Config.WildcardShipInfo.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Success);
            infoLCDs.AddRange(info);
            infoLCDs.AddRange(infoCps.Select(c => c.GetSurface(int.Parse(Config.WildcardShipInfo.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Value))));

            logLCDs.Clear();
            var log = GetBlocksOfType<IMyTextPanel>(Config.WildcardLogLCDs);
            var logCps = GetBlocksImplementType<IMyTextSurfaceProvider>(Config.WildcardLogLCDs).Where(c => Config.WildcardLogLCDs.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Success);
            logLCDs.AddRange(log.Select(l => new TextPanelDesc(l, l)));
            logLCDs.AddRange(logCps.Select(c => new TextPanelDesc((IMyTerminalBlock)c, c.GetSurface(int.Parse(Config.WildcardLogLCDs.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Value)))));

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
        List<T> GetBlocksImplementType<T>(System.Text.RegularExpressions.Regex regEx) where T : class
        {
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && regEx.IsMatch(b.CustomName) && b is T);
            return blocks.Cast<T>().ToList();
        }

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == shipId;
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

        TimeSpan GetNextLCDsRefresh()
        {
            return lastRefreshLCDs + Config.RefreshLCDsInterval - DateTime.Now;
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
            monitorizeCapacity = true;
            timerLoad?.StartCountdown();
        }
        internal void Unload()
        {
            monitorizeCapacity = true;
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
                    hit.EntityId != Me.CubeGrid.EntityId &&
                    Vector3D.Distance(hit.HitPosition.Value, cameraPilot.GetPosition()) <= collisionDetectRange;
            }

            hit = new MyDetectedEntityInfo();
            return false;
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
        internal bool IsInAtmosphere()
        {
            return GetGravity().Length() / 9.8 > Config.AtmNavigationGravityThr;
        }
        internal double GetMass()
        {
            return remotePilot.CalculateShipMass().TotalMass;
        }

        internal Vector3D GetPilotLinearVelocity()
        {
            return remotePilot.GetShipVelocities().LinearVelocity;
        }
        internal Vector3D GetPilotForwardDirection()
        {
            return remotePilot.WorldMatrix.Forward;
        }

        internal Vector3D GetLandingLinearVelocity()
        {
            return remoteLanding.GetShipVelocities().LinearVelocity;
        }
        internal Vector3D GetLandingForwardDirection()
        {
            return remoteLanding.WorldMatrix.Forward;
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
        internal Vector3D GetDockingForwardDirection()
        {
            return remoteDocking.WorldMatrix.Forward;
        }
        internal Vector3D GetDockingUpDirection()
        {
            return remoteDocking.WorldMatrix.Up;
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

        bool IsPropulsionFilled(ShipStatus shipStatus, out string msg)
        {
            if (!IsBatteryFilled(shipStatus, out msg))
            {
                return false;
            }
            if (!IsHydrogenFilled(shipStatus, out msg))
            {
                return false;
            }
            msg = null;
            return true;
        }
        bool IsBatteryFilled(ShipStatus shipStatus, out string msg)
        {
            var minStoredPower = shipStatus == ShipStatus.Loading ? Config.MinPowerOnLoad : Config.MinPowerOnUnload;
            if (minStoredPower <= 0)
            {
                msg = null;
                return true;
            }

            double battery = CalculateBatteryPercentage();
            if (battery >= minStoredPower)
            {
                msg = null;
                return true;
            }

            msg = $"Batteries at {battery:P1}, minimum required {minStoredPower:P1}.";
            return false;
        }
        bool IsHydrogenFilled(ShipStatus shipStatus, out string msg)
        {
            var minStoredHydrogen = shipStatus == ShipStatus.Loading ? Config.MinHydrogenOnLoad : Config.MinHydrogenOnUnload;
            if (minStoredHydrogen <= 0)
            {
                msg = null;
                return true;
            }

            double hydrogen = CalculateHydrogenPercentage();
            if (hydrogen >= minStoredHydrogen)
            {
                msg = null;
                return true;
            }

            msg = $"Hydrogen at {hydrogen:P1}, minimum required {minStoredHydrogen:P1}.";
            return false;
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

        void Next()
        {
            if (shipStatus == ShipStatus.Loading)
            {
                monitorizeCapacity = false;
            }
            else if (shipStatus == ShipStatus.Unloading)
            {
                monitorizeCapacity = false;
            }
            else
            {
                WriteLogLCDs($"Ship is not docked: {shipStatus}");
            }
        }
        void StopAndUndocK()
        {
            if (shipStatus == ShipStatus.Loading)
            {
                timerFinalize?.StartCountdown();
                SendWaitingMessage(ExchangeTasks.EndLoad);
                monitorizePropulsion = false;
            }
            else if (shipStatus == ShipStatus.Unloading)
            {
                timerFinalize?.StartCountdown();
                SendWaitingMessage(ExchangeTasks.EndUnload);
                monitorizePropulsion = false;
            }
            else
            {
                WriteLogLCDs($"Ship is not docked: {shipStatus}");
            }
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
                double pct = capacity / Config.MaxLoad;
                string msg = null;
                if (pct >= 1.0) IsPropulsionFilled(shipStatus, out msg);
                return $"Loading from {GetDockedGridName()} {pct:P1}. {msg}";
            }
            else if (shipStatus == ShipStatus.Unloading)
            {
                double capacity = CalculateCargoPercentage();
                double pct = 1.0 - (capacity / Config.MaxLoad);
                string msg = null;
                if (pct >= 1.0) IsPropulsionFilled(shipStatus, out msg);
                return $"Unloading to {GetDockedGridName()} {pct:P1}. {msg}";
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
            monitorizeCapacity = Utils.ReadInt(storageLines, "MonitorizeCapacity", 0) == 1;
            monitorizePropulsion = Utils.ReadInt(storageLines, "MonitorizePropulsion", 0) == 1;
            shipStatus = (ShipStatus)Utils.ReadInt(storageLines, "ShipStatus", (int)ShipStatus.Idle);
            navigator.LoadFromStorage(Utils.ReadString(storageLines, "Navigator"));
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Paused={(paused ? 1 : 0)}",
                $"MonitorizeCapacity={(monitorizeCapacity ? 1 : 0)}",
                $"MonitorizePropulsion={(monitorizePropulsion ? 1 : 0)}",
                $"ShipStatus={(int)shipStatus}",
                $"Navigator={navigator.SaveToStorage()}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
