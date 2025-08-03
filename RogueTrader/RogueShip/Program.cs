using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        }

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "PAUSE") Pause();
            else if (argument == "RESUME") Resume();
            else if (argument == "ENABLE_LOGS") EnableLogs();

            else if (argument == "START_ROUTE") StartRoute();
        }

        /// <summary>
        /// Ship reset
        /// </summary>
        void Reset()
        {
            Storage = "";

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
        /// Starts a route to the configured load base.
        /// </summary>
        void StartRoute()
        {
            List<string> parts = new List<string>()
            {
                $"Command=WAITING_LOAD",
                $"To={config.Route.LoadBase}",
                $"From={shipId}",
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

            if (command == "COME_TO_LOAD") CmdDock(lines, ExchangeTasks.Load);
            if (command == "COME_TO_UNLOAD") CmdDock(lines, ExchangeTasks.Unload);
        }

        /// <summary>
        /// The ship responds with its status
        /// </summary>
        /// <remarks>
        /// Request:  REQUEST_STATUS
        /// Execute:  RESPONSE_STATUS
        /// </remarks>
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
            };
            BroadcastMessage(parts);
        }

        /// <summary>
        /// SHIP begins navigation to the specified connector and docks in LOADING or UNLOADING MODE.
        /// </summary>
        void CmdDock(string[] lines, ExchangeTasks task)
        {
            if (task == ExchangeTasks.Load)
            {

            }
            else if (task == ExchangeTasks.Unload)
            {

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

        void ResetThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = true;
                t.ThrustOverridePercentage = 0f;
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
        #endregion

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            paused = Utils.ReadInt(storageLines, "Paused", 0) == 1;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Paused={(paused ? 1 : 0)}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
