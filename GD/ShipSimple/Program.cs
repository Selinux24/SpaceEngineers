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
        const string Version = "1.2";

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
        internal readonly Config Config;

        readonly StringBuilder sbLog = new StringBuilder();

        bool paused = false;
        ShipStatus shipStatus = ShipStatus.Idle;
        readonly AlignData alignData;

        DateTime lastDockRequest = DateTime.MinValue;
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

            alignData = new AlignData(this);

            remoteDock = GetBlockWithName<IMyRemoteControl>(Config.ShipRemoteControlDock);
            if (remoteDock == null)
            {
                Echo($"Remote Control '{Config.ShipRemoteControlDock}' not found.");
                return;
            }
            connectorDock = GetBlockWithName<IMyShipConnector>(Config.ShipConnectorDock);
            if (connectorDock == null)
            {
                Echo($"Connector '{Config.ShipConnectorDock}' not found.");
                return;
            }
            timerDock = GetBlockWithName<IMyTimerBlock>(Config.ShipTimerDock);
            if (timerDock == null)
            {
                Echo($"Timer '{Config.ShipTimerDock}' not found.");
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
            WriteInfoLCDs($"SimpleShip v{Version}. {DateTime.Now:HH:mm:ss}", false);
            WriteInfoLCDs($"{shipId} in channel {Config.Channel}");
            WriteInfoLCDs($"{shipStatus}");
            if (shipStatus == ShipStatus.WaitingDock)
            {
                var waitTime = DateTime.Now - lastDockRequest;
                WriteInfoLCDs($"Waiting dock response... {waitTime.TotalSeconds:F0}s up to {Config.DockRequestTimeout.TotalSeconds}s");
            }
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
            Config.EnableLogs = !Config.EnableLogs;
        }
        /// <summary>
        /// Changes the state of the variable that controls the refresh of LCDs
        /// </summary>
        void EnableRefreshLCDs()
        {
            Config.EnableRefreshLCDs = !Config.EnableRefreshLCDs;
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

        #region UPDATE SHIP STATUS
        void UpdateShipStatus()
        {
            alignData.Update();

            if (shipStatus == ShipStatus.WaitingDock && (DateTime.Now - lastDockRequest) > Config.DockRequestTimeout)
            {
                shipStatus = ShipStatus.Idle;
                lastDockRequest = DateTime.MinValue;
            }

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

        internal void ApplyGyroOverride(Vector3D axis)
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
        internal void ResetGyros()
        {
            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = false;
                gyro.Pitch = 0;
                gyro.Yaw = 0;
                gyro.Roll = 0;
            }
        }

        internal Vector3D GetVelocity()
        {
            return remoteDock.GetShipVelocities().LinearVelocity;
        }
        internal double GetMass()
        {
            return remoteDock.CalculateShipMass().TotalMass;
        }
        internal Vector3D GetDockPosition()
        {
            return connectorDock.GetPosition();
        }
        internal Vector3D GetDockingForwardDirection()
        {
            return remoteDock.WorldMatrix.Forward;
        }
        internal Vector3D GetDockingUpDirection()
        {
            return remoteDock.WorldMatrix.Up;
        }
        internal bool IsDocked()
        {
            return connectorDock.Status == MyShipConnectorStatus.Connected;
        }
        internal void Dock()
        {
            timerDock.StartCountdown();
            shipStatus = ShipStatus.Idle;
        }

        TimeSpan GetNextLCDsRefresh()
        {
            return lastRefreshLCDs + Config.RefreshLCDsInterval - DateTime.Now;
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
        void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            WriteLogLCDs($"BroadcastMessage: {message}");

            IGC.SendBroadcastMessage(Config.Channel, message);
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
