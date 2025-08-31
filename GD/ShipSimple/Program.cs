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
    /// Ship script for dock in bases.
    /// </summary>
    partial class Program : MyGridProgram
    {
        const string Version = "1.1";

        #region Blocks
        readonly IMyBroadcastListener bl;

        readonly IMyRemoteControl remoteDock;
        readonly IMyShipConnector connectorDock;
        readonly IMyTimerBlock timerDock;

        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();

        readonly List<IMyTextSurface> infoLCDs = new List<IMyTextSurface>();
        readonly List<TextPanelDesc> logLCDs = new List<TextPanelDesc>();
        #endregion

        readonly string shipId;
        readonly Config config;

        readonly StringBuilder sbLog = new StringBuilder();

        bool paused = false;
        ShipStatus shipStatus = ShipStatus.Idle;
        readonly AlignData alignData;

        DateTime lastDockRequest = DateTime.MinValue;

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
            config = new Config(Me.CustomData);
            if (!config.IsValid())
            {
                Echo(config.GetErrors());
                return;
            }

            alignData = new AlignData(config);

            remoteDock = GetBlockWithName<IMyRemoteControl>(config.ShipRemoteControlDock);
            if (remoteDock == null)
            {
                Echo($"Remote Control '{config.ShipRemoteControlDock}' not found.");
                return;
            }
            connectorDock = GetBlockWithName<IMyShipConnector>(config.ShipConnectorDock);
            if (connectorDock == null)
            {
                Echo($"Connector '{config.ShipConnectorDock}' not found.");
                return;
            }
            timerDock = GetBlockWithName<IMyTimerBlock>(config.ShipTimerDock);
            if (timerDock == null)
            {
                Echo($"Timer '{config.ShipTimerDock}' not found.");
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

            RefreshLCDs();

            bl = IGC.RegisterBroadcastListener(config.Channel);

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            WriteInfoLCDs($"SimpleShip v{Version}", false);
            WriteInfoLCDs($"{shipId} in channel {config.Channel}");
            WriteInfoLCDs($"{shipStatus}");
            if (shipStatus == ShipStatus.WaitingDock)
            {
                var waitTime = DateTime.Now - lastDockRequest;
                WriteInfoLCDs($"Waiting dock response... {waitTime.TotalSeconds:F0}s up to {config.DockRequestTimeout.TotalSeconds}s");
            }

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

            DoAlign();

            if (DateTime.Now - lastRefreshLCDs > refreshLCDsInterval)
            {
                RefreshLCDs();
            }

            if (shipStatus == ShipStatus.WaitingDock && (DateTime.Now - lastDockRequest) > config.DockRequestTimeout)
            {
                shipStatus = ShipStatus.Idle;
                lastDockRequest = DateTime.MinValue;
            }
        }

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "PAUSE") Pause();
            else if (argument == "RESUME") Resume();
            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "REQUEST_DOCK") RequestDock();
        }

        /// <summary>
        /// Ship reset
        /// </summary>
        void Reset()
        {
            Storage = "";

            remoteDock.SetAutoPilotEnabled(false);
            remoteDock.ClearWaypoints();
            ResetGyros();
            ResetThrust();

            shipStatus = ShipStatus.Idle;
            alignData.Clear();

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
        void RequestDock()
        {
            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_DOCK",
                $"From={shipId}",
                $"Position={Utils.VectorToStr(remoteDock.GetPosition())}",
            };
            BroadcastMessage(parts);

            shipStatus = ShipStatus.WaitingDock;
            lastDockRequest = DateTime.Now;
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
        }

        /// <summary>
        /// Replies the status of the ship
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
                $"Position={Utils.VectorToStr(remoteDock.GetPosition())}",
                $"StatusMessage={PrintShipStatus()}"
            };
            BroadcastMessage(parts);
        }

        /// <summary>
        /// Docks in a exchange
        /// </summary>
        void CmdDock(string[] lines)
        {
            alignData.Initialize(new ExchangeInfo(lines));

            shipStatus = ShipStatus.Docking;
        }
        #endregion

        #region ALIGN
        void DoAlign()
        {
            if (!alignData.HasTarget)
            {
                return;
            }

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
                alignData.Clear();
                ResetGyros();
                ResetThrust();
                timerDock.StartCountdown();
                shipStatus = ShipStatus.Idle;
                return;
            }

            if (connectorDock.Status == MyShipConnectorStatus.Connected)
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

            alignData.UpdatePosition(connectorDock.GetPosition());

            var currentVelocity = remoteDock.GetShipVelocities().LinearVelocity;
            double mass = remoteDock.CalculateShipMass().TotalMass;

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
                return;
            }

            double desiredSpeed = alignData.CalculateDesiredSpeed(distance);
            var neededForce = Utils.CalculateThrustForce(alignData.ToTarget, desiredSpeed, currentVelocity, mass);

            ApplyThrust(neededForce);
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

        void RefreshLCDs()
        {
            infoLCDs.Clear();
            var info = GetBlocksOfType<IMyTextPanel>(config.WildcardShipInfo);
            var infoCps = GetBlocksOfType<IMyCockpit>(config.WildcardShipInfo).Where(c => config.WildcardShipInfo.Match(c.CustomName).Groups[1].Success);
            infoLCDs.AddRange(info);
            infoLCDs.AddRange(infoCps.Select(c => c.GetSurface(int.Parse(config.WildcardShipInfo.Match(c.CustomName).Groups[1].Value))));

            logLCDs.Clear();
            var log = GetBlocksOfType<IMyTextPanel>(config.WildcardLogLCDs);
            var logCps = GetBlocksOfType<IMyCockpit>(config.WildcardLogLCDs).Where(c => config.WildcardLogLCDs.Match(c.CustomName).Groups[1].Success);
            logLCDs.AddRange(log.Select(l => new TextPanelDesc(l, l)));
            logLCDs.AddRange(logCps.Select(c => new TextPanelDesc(c, c.GetSurface(int.Parse(config.WildcardLogLCDs.Match(c.CustomName).Groups[1].Value)))));

            lastRefreshLCDs = DateTime.Now;
        }

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == shipId;
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

        bool AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipMatrix = remoteDock.WorldMatrix;
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

        string PrintShipStatus()
        {
            var sb = new StringBuilder();

            PrintAlignStatus(sb);

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
            var logLines = log.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            foreach (var lcd in logLCDs)
            {
                lcd.Write(log, logLines);
            }
        }
        void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            WriteLogLCDs($"BroadcastMessage: {message}");

            IGC.SendBroadcastMessage(config.Channel, message);
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
            alignData.LoadFromStorage(Utils.ReadString(storageLines, "AlignData"));
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Paused={(paused ? 1 : 0)}",
                $"ShipStatus={(int)shipStatus}",
                $"AlignData={alignData.SaveToStorage()}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
