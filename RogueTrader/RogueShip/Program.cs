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
        #region Blocks
        readonly IMyBroadcastListener bl;

        readonly IMyTimerBlock timerPilot;
        readonly IMyTimerBlock timerWaiting;

        readonly IMyTimerBlock timerDock;
        readonly IMyTimerBlock timerUndock;

        readonly IMyTimerBlock timerLoad;
        readonly IMyTimerBlock timerUnload;

        readonly IMyRemoteControl remotePilot;
        readonly IMyRemoteControl remoteDocking;
        readonly IMyRemoteControl remoteLanding;

        readonly IMyCameraBlock cameraPilot;
        readonly IMyRadioAntenna antenna;
        readonly IMyShipConnector connectorA;

        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        readonly List<IMyTextPanel> infoLCDs = new List<IMyTextPanel>();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly List<IMyCargoContainer> shipCargos = new List<IMyCargoContainer>();
        #endregion

        readonly string shipId;
        readonly StringBuilder sbLog = new StringBuilder();
        bool paused = false;
        ShipStatus shipStatus = ShipStatus.Idle;
        readonly Navigator navigator;

        internal readonly Config Config;

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

            logLCDs = GetBlocksOfType<IMyTextPanel>(Config.WildcardLogLCDs);
            infoLCDs = GetBlocksOfType<IMyTextPanel>(Config.WildcardShipInfo);
            shipCargos = GetBlocksOfType<IMyCargoContainer>();

            WriteLCDs(Config.WildcardShipId, shipId);

            bl = IGC.RegisterBroadcastListener(Config.Channel);
            Echo($"Listening in channel {Config.Channel}");

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
            WriteInfoLCDs($"{shipId} in channel {Config.Channel}");
            WriteInfoLCDs($"{CalculateCargoPercentage():P1} cargo.");

            if (DoPause()) return;

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

            MonitorizeLoad();

            navigator.Update();
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
            var parking = Utils.ReadVector(lines, "Parking");

            string exchange = Utils.ReadString(lines, "Exchange");
            var fw = Utils.ReadVector(lines, "Forward");
            var up = Utils.ReadVector(lines, "Up");
            var wpList = Utils.ReadVectorList(lines, "Waypoints");

            if (task == ExchangeTasks.StartLoad || task == ExchangeTasks.StartUnload)
            {
                navigator.ApproachToDock(parking, exchange, fw, up, wpList, "ON_APPROACHING_COMPLETED", task);
                shipStatus = ShipStatus.Docking;
            }
            else if (task == ExchangeTasks.EndLoad || task == ExchangeTasks.EndUnload)
            {
                navigator.SeparateFromDock(parking, exchange, fw, up, wpList, "ON_SEPARATION_COMPLETED", task);
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

        #region LOAD/UNLOAD Monitors
        void MonitorizeLoad()
        {
            if (shipStatus == ShipStatus.Loading)
            {
                WriteInfoLCDs("Loading cargo...");

                double capacity = CalculateCargoPercentage();
                if (capacity >= Config.MaxLoad)
                {
                    SendWaitingUndockMessage(ExchangeTasks.EndLoad);
                }

                return;
            }

            if (shipStatus == ShipStatus.Unloading)
            {
                WriteInfoLCDs("Unloading cargo...");

                double capacity = CalculateCargoPercentage();
                if (capacity <= Config.MinLoad)
                {
                    SendWaitingUndockMessage(ExchangeTasks.EndUnload);
                }

                return;
            }
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

        public void WriteLCDs(string wildcard, string text)
        {
            List<IMyTextPanel> lcds = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(lcds, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(wildcard));
            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, false);
            }
        }
        public void WriteInfoLCDs(string text, bool append = true)
        {
            Echo(text);

            foreach (var lcd in infoLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text + Environment.NewLine, append);
            }
        }
        public void WriteLogLCDs(string text)
        {
            if (!Config.EnableLogs)
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
        public void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            WriteLogLCDs($"BroadcastMessage: {message}");

            IGC.SendBroadcastMessage(Config.Channel, message);
        }

        public void Pilot()
        {
            timerPilot?.StartCountdown();
        }
        public void Waiting()
        {
            timerWaiting?.StartCountdown();
        }

        public void Dock()
        {
            timerDock?.StartCountdown();
        }
        public void Undock()
        {
            timerUndock?.StartCountdown();
        }

        public void Load()
        {
            timerLoad?.StartCountdown();
        }
        public void Unload()
        {
            timerUnload?.StartCountdown();
        }

        public void ConfigureRemotePilot(Vector3D position, string positionName, double velocity, bool activateNow)
        {
            ConfigureRemote(remotePilot, position, positionName, velocity, activateNow);
        }
        void ConfigureRemote(IMyRemoteControl remote, Vector3D position, string positionName, double velocity, bool activateNow)
        {
            Pilot();

            DisableRemote(remote);

            remote.AddWaypoint(position, positionName);
            remote.SetCollisionAvoidance(true);
            remote.WaitForFreeWay = false;
            remote.FlightMode = FlightMode.OneWay;
            remote.SpeedLimit = (float)velocity;

            if (activateNow) remote.SetAutoPilotEnabled(true);
        }

        public void DisableRemotePilot()
        {
            DisableRemote(remotePilot);
        }
        void DisableRemote(IMyRemoteControl remote)
        {
            remote.ClearWaypoints();
            remote.SetAutoPilotEnabled(false);
        }

        public bool IsObstacleAhead(double collisionDetectRange, Vector3D velocity, out MyDetectedEntityInfo hit)
        {
            cameraPilot.EnableRaycast = true;

            MatrixD cameraMatrixInv = MatrixD.Invert(cameraPilot.WorldMatrix);
            Vector3D localDirection = Vector3D.TransformNormal(Vector3D.Normalize(velocity), cameraMatrixInv);
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

        public void ThrustToTarget(bool landing, Vector3D toTarget, double maxSpeed)
        {
            var remote = landing ? remoteLanding : remotePilot;

            var currentVelocity = remote.GetShipVelocities().LinearVelocity;
            double mass = remote.CalculateShipMass().PhysicalMass;

            var force = Utils.CalculateThrustForce(toTarget, maxSpeed, currentVelocity, mass);
            ApplyThrust(force);
        }

        public void ApplyThrust(Vector3D force, Vector3D gravity, double mass)
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
        public void ResetThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = true;
                t.ThrustOverridePercentage = 0f;
            }
        }
        public void StopThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = false;
                t.ThrustOverridePercentage = 0;
            }
        }

        public bool AlignToDirection(bool landing, Vector3D toTarget, double thr)
        {
            var remote = landing ? remoteLanding : remotePilot;
            var direction = remote.WorldMatrix.Forward;

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
        public bool AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipMatrix = remoteDocking.WorldMatrix;
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
        public void ApplyGyroOverride(Vector3D axis)
        {
            foreach (var gyro in gyros)
            {
                var localAxis = Vector3D.TransformNormal(axis, MatrixD.Transpose(gyro.WorldMatrix));
                var gyroRot = localAxis * -Config.GyrosSpeed;
                gyro.GyroOverride = true;
                gyro.Pitch = (float)gyroRot.X;
                gyro.Yaw = (float)gyroRot.Y;
                gyro.Roll = (float)gyroRot.Z;
            }
        }
        public void ResetGyros()
        {
            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = false;
                gyro.Pitch = 0;
                gyro.Yaw = 0;
                gyro.Roll = 0;
            }
        }

        public double GetSpeed(bool landing)
        {
            var remote = landing ? remoteLanding : remotePilot;

            return remote.GetShipSpeed();
        }

        public Vector3D GetPosition()
        {
            return antenna.GetPosition();
        }

        public double GetPilotSpeed()
        {
            return remotePilot.GetShipSpeed();
        }
        public Vector3D GetPilotLinearVelocity()
        {
            return remotePilot.GetShipVelocities().LinearVelocity;
        }
        public Vector3D GetPilotNaturalGravity()
        {
            return remotePilot.GetNaturalGravity();
        }

        public double GetLandingSpeed()
        {
            return remoteLanding.GetShipSpeed();
        }

        public bool IsConnected()
        {
            return connectorA.Status == MyShipConnectorStatus.Connected;
        }
        public Vector3D GetDockingPosition()
        {
            return connectorA.GetPosition();
        }
        public double GetDockingPhysicalMass()
        {
            return remoteDocking.CalculateShipMass().PhysicalMass;
        }
        public Vector3D GetDockingLinearVelocity()
        {
            return remoteDocking.GetShipVelocities().LinearVelocity;
        }
        public Vector3D GetDockingNaturalGravity()
        {
            return remoteDocking.GetNaturalGravity();
        }

        public IMyCameraBlock GetCameraPilot()
        {
            return cameraPilot;
        }

        public void EnableSystems()
        {
            antenna.Enabled = true;
        }
        public void DisableSystems()
        {
            antenna.Enabled = false;
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

        void SendWaitingMessage(ExchangeTasks task)
        {
            string message = task == ExchangeTasks.StartLoad ? "WAITING_LOAD" : "WAITING_UNLOAD";
            string to = task == ExchangeTasks.StartLoad ? Config.Route.LoadBase : Config.Route.UnloadBase;

            List<string> parts = new List<string>()
            {
                $"Command={message}",
                $"To={to}",
                $"From={shipId}",
            };
            BroadcastMessage(parts);

            Waiting();

            shipStatus = ShipStatus.WaitingDock;
        }
        void SendWaitingUndockMessage(ExchangeTasks task)
        {
            //Send a message to the base to undock the ship from the exchange connector
            string message = task == ExchangeTasks.EndLoad ? "WAITING_UNDOCK_LOAD" : "WAITING_UNDOCK_UNLOAD";
            string to = task == ExchangeTasks.EndLoad ? Config.Route.LoadBase : Config.Route.UnloadBase;

            List<string> parts = new List<string>()
            {
                $"Command={message}",
                $"To={to}",
                $"From={shipId}",
            };
            BroadcastMessage(parts);

            Waiting();

            shipStatus = ShipStatus.WaitingUndock;
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
            shipStatus = (ShipStatus)Utils.ReadInt(storageLines, "ShipStatus", (int)ShipStatus.Idle);
            navigator.LoadFromStorage(Utils.ReadString(storageLines, "Navigator"));
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Paused={(paused ? 1 : 0)}",
                $"ShipStatus={(int)shipStatus}",
                $"Navigator={navigator.SaveToStorage()}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
