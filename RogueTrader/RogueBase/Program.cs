using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    /// <summary>
    /// RogueBase script
    /// </summary>
    partial class Program : MyGridProgram
    {
        const string Separate = "-";

        #region Blocks
        readonly IMyBroadcastListener bl;
        readonly List<IMyTextPanel> dataLCDs = new List<IMyTextPanel>();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        #endregion

        readonly string baseId;
        readonly Config config;

        readonly StringBuilder sbData = new StringBuilder();
        readonly StringBuilder sbLog = new StringBuilder();

        readonly List<Ship> ships = new List<Ship>();
        readonly List<ExchangeGroup> exchanges = new List<ExchangeGroup>();
        readonly List<ExchangeRequest> exchangeRequests = new List<ExchangeRequest>();

        bool requestStatus = true;
        DateTime lastRequestStatus = DateTime.MinValue;

        bool requestExchange = true;
        DateTime lastExchangeRequest = DateTime.MinValue;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData = Config.GetDefault();

                Echo("CustomData not set.");
                return;
            }

            baseId = Me.CubeGrid.CustomName;
            config = new Config(Me.CustomData);
            if (!config.IsValid())
            {
                Echo(config.GetErrors());
                return;
            }

            InitializeExchangeGroups();

            LoadFromStorage();

            GridTerminalSystem.GetBlocksOfType(dataLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(config.DataLCDs));
            GridTerminalSystem.GetBlocksOfType(logLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(config.LogLCDs));

            WriteLCDs("[baseId]", baseId);

            bl = IGC.RegisterBroadcastListener(config.Channel);
            Echo($"{baseId} in channel {config.Channel}");

            Runtime.UpdateFrequency = UpdateFrequency.Update100; // Ejecuta cada ~1.6s
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            DoRequestStatus();
            DoExchangeRequest();

            if (!string.IsNullOrEmpty(argument))
            {
                ParseTerminalMessage(argument);
            }

            while (bl.HasPendingMessage)
            {
                var message = bl.AcceptMessage();
                ParseMessage(message.Data.ToString());
            }

            UpdateBaseState();

            sbData.Clear();
            sbData.AppendLine($"{baseId} in channel {config.Channel}");
            PrintExchanges();
            PrintShipStatus();
            PrintExchangeRequests();
            WriteDataLCDs();
            WriteLogLCDs();
        }

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLog($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "LIST_SHIPS") ListShips();
            else if (argument == "LIST_EXCHANGES") ListExchanges();
            else if (argument == "LIST_EXCHANGE_REQUESTS") ListExchangeRequests();
            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "ENABLE_STATUS_REQUEST") EnableStatusRequest();
            else if (argument == "ENABLE_EXCHANGE_REQUEST") EnableExchangeRequest();
        }

        /// <summary>
        /// Resets the state
        /// </summary>
        void Reset()
        {
            Storage = "";

            sbData.Clear();
            sbLog.Clear();

            exchanges.Clear();
            ships.Clear();
            exchangeRequests.Clear();

            InitializeExchangeGroups();
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of ships
        /// </summary>
        void ListShips()
        {
            config.ShowShips = !config.ShowShips;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of exchanges
        /// </summary>
        void ListExchanges()
        {
            config.ShowExchanges = !config.ShowExchanges;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of exchange requests
        /// </summary>
        void ListExchangeRequests()
        {
            config.ShowExchangeRequests = !config.ShowExchangeRequests;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of logs
        /// </summary>
        void EnableLogs()
        {
            config.EnableLogs = !config.EnableLogs;
        }
        /// <summary>
        /// Changes the state of the variable that controls the status request to the ships
        /// </summary>
        void EnableStatusRequest()
        {
            requestStatus = !requestStatus;
        }
        /// <summary>
        /// Changes the state of the variable that controls the exchange requests
        /// </summary>
        void EnableExchangeRequest()
        {
            requestExchange = !requestExchange;
        }
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLog($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = Utils.ReadArgument(lines, "Command");

            if (!IsForMe(lines)) return;

            if (command == "RESPONSE_STATUS") ProcessResponseStatus(lines);

            else if (command == "WAITING_LOAD") ProcessWaiting(lines, ExchangeTasks.StartLoad);
            else if (command == "WAITING_UNLOAD") ProcessWaiting(lines, ExchangeTasks.StartUnload);
            else if (command == "WAITING_UNDOCK_LOAD") ProcessWaiting(lines, ExchangeTasks.EndLoad);
            else if (command == "WAITING_UNDOCK_UNLOAD") ProcessWaiting(lines, ExchangeTasks.EndUnload);
        }

        /// <summary>
        /// BASE updates the SHIP status
        /// </summary>
        /// <remarks>
        /// Request: RESPONSE_STATUS
        /// </remarks>
        void ProcessResponseStatus(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            ShipStatus status = (ShipStatus)Utils.ReadInt(lines, "Status");
            string statusMessage = Utils.ReadString(lines, "StatusMessage");
            var position = Utils.ReadVector(lines, "Position");
            var cargo = Utils.ReadDouble(lines, "Cargo");

            var ship = ships.Find(s => s.Name == from);
            if (ship == null)
            {
                ship = new Ship() { Name = from };
                ships.Add(ship);
            }

            ship.Status = status;
            ship.StatusMessage = statusMessage;
            ship.Position = position;
            ship.Cargo = cargo;
            ship.UpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Enqueues a request to make a task to a SHIP.
        /// </summary>
        void ProcessWaiting(string[] lines, ExchangeTasks task)
        {
            string from = Utils.ReadString(lines, "From");

            EnqueueExchangeRequest(from, task);
        }
        #endregion

        #region STATUS
        /// <summary>
        /// BASE requests status from all ships
        /// </summary>
        /// <remarks>
        /// Execute:  REQUEST_STATUS
        /// </remarks>
        void DoRequestStatus()
        {
            if (!requestStatus)
            {
                return;
            }

            if (DateTime.Now - lastRequestStatus < TimeSpan.FromSeconds(config.RequestStatusInterval))
            {
                return;
            }
            lastRequestStatus = DateTime.Now;

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_STATUS",
                $"From={baseId}",
            };
            BroadcastMessage(parts);
        }
        #endregion

        #region EXCHANGE REQUEST
        /// <summary>
        /// BASE reviews exchange requests. It searches for free connectors and gives SHIP the exchange command on the specified connector.
        /// </summary>
        /// <remarks>
        /// Execute:  DOCK
        /// </remarks>
        void DoExchangeRequest()
        {
            if (!requestExchange)
            {
                return;
            }

            if (DateTime.Now - lastExchangeRequest < TimeSpan.FromSeconds(config.RequestReceptionInterval))
            {
                return;
            }
            lastExchangeRequest = DateTime.Now;

            if (DoExchangeUndockRequest())
            {
                return;
            }

            DoExchangeDockRequest();
        }
        bool DoExchangeDockRequest()
        {
            var exRequest = GetPendingExchangeDockRequests();
            if (exRequest.Count == 0)
            {
                return false;
            }
            var freeExchanges = GetFreeExchanges();
            if (freeExchanges.Count == 0)
            {
                return false;
            }
            var waitingShips = GetWaitingShips();
            if (waitingShips.Count == 0)
            {
                return false;
            }
            WriteLog($"Exchange requests: {exRequest.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {waitingShips.Count}");

            var shipExchangePairs = ShipExchangePair.GetNearestShipsFromExchanges(waitingShips, freeExchanges);
            if (shipExchangePairs.Count == 0)
            {
                return false;
            }

            foreach (var request in exRequest)
            {
                var pair = shipExchangePairs.FirstOrDefault(s => s.Ship.Name == request.Ship);
                if (pair == null)
                {
                    continue;
                }

                var ship = pair.Ship;
                var exchange = pair.Exchange;

                request.Idle = false;
                ship.Status = ShipStatus.Docking;
                exchange.DockRequest(ship.Name);

                var wayPoints = exchange.CalculateRouteToConnector();

                string command = null;
                switch (request.Task)
                {
                    case ExchangeTasks.StartLoad:
                        command = "COME_TO_LOAD";
                        break;
                    case ExchangeTasks.StartUnload:
                        command = "COME_TO_UNLOAD";
                        break;
                }

                List<string> parts = new List<string>()
                {
                    $"Command={command}",
                    $"To={request.Ship}",
                    $"From={baseId}",

                    $"Parking={config.Parking}",

                    $"Exchange={exchange.Name}",
                    $"Forward={Utils.VectorToStr(exchange.Forward)}",
                    $"Up={Utils.VectorToStr(exchange.Up)}",
                    $"WayPoints={Utils.VectorListToStr(wayPoints)}",
                };
                BroadcastMessage(parts);

                return true;
            }

            return false;
        }
        bool DoExchangeUndockRequest()
        {
            var exRequest = GetPendingExchangeUndockRequests();
            if (exRequest.Count == 0)
            {
                return false;
            }

            foreach (var request in exRequest)
            {
                var exchange = exchanges.FirstOrDefault(e => e.DockedShipName == request.Ship);
                if (exchange == null)
                {
                    continue;
                }

                var wayPoints = exchange.CalculateRouteFromConnector();

                string command = null;
                switch (request.Task)
                {
                    case ExchangeTasks.StartLoad:
                        command = "UNDOCK_TO_LOAD";
                        break;
                    case ExchangeTasks.StartUnload:
                        command = "UNDOCK_TO_UNLOAD";
                        break;
                }

                List<string> parts = new List<string>()
                {
                    $"Command={command}",
                    $"To={request.Ship}",
                    $"From={baseId}",

                    $"Parking={config.Parking}",

                    $"Exchange={exchange.Name}",
                    $"Forward={Utils.VectorToStr(exchange.Forward)}",
                    $"Up={Utils.VectorToStr(exchange.Up)}",
                    $"WayPoints={Utils.VectorListToStr(wayPoints)}",
                };
                BroadcastMessage(parts);

                return true;
            }

            return false;
        }
        #endregion

        #region UPDATE BASE STATE
        void UpdateBaseState()
        {
            double time = Runtime.TimeSinceLastRun.TotalSeconds;

            foreach (var exchange in exchanges)
            {
                if (exchange.Update(time)) continue;

                //TODO: If there is a dock request and it does not match the connected ships, abort and return the ship to the parking position, and put it on hold.
            }

            //Gets all docked ship names
            var dockedShips = exchanges
                .SelectMany(e => e.DockedShips())
                .ToList();

            //Removes all exchange requests for ships that are already docked
            exchangeRequests.RemoveAll(e => dockedShips.Contains(e.Ship));
        }
        #endregion

        #region UTILITY
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
        void WriteDataLCDs()
        {
            string text = sbData.ToString();
            foreach (var lcd in dataLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, false);
            }
        }
        void WriteLogLCDs()
        {
            string text = sbLog.ToString();
            string[] logLines = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

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
                    lcd.WriteText(text, false);
                }
            }
        }
        void WriteLog(string text)
        {
            if (!config.EnableLogs)
            {
                return;
            }

            sbLog.Insert(0, text + Environment.NewLine);
        }
        void BroadcastMessage(List<string> parts)
        {
            string message = string.Join("|", parts);

            WriteLog($"SendIGCMessage: {message}");

            IGC.SendBroadcastMessage(config.Channel, message);
        }

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == baseId;
        }

        void InitializeExchangeGroups()
        {
            var regEx = config.ExchangesRegex;

            //Find all blocks that have the exchange regex in their name
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, i => i.CubeGrid == Me.CubeGrid && Utils.IsFromGroup(i.CustomName, regEx));

            List<ExchangeGroup> exchanges = new List<ExchangeGroup>();

            //Group them by the group name
            var groups = blocks.GroupBy(b => Utils.ExtractGroupName(b.CustomName, regEx)).ToList();

            //For each group, initialize the blocks of the ExchangeGroup class
            foreach (var group in groups)
            {
                var exchangeGroup = new ExchangeGroup(config)
                {
                    Name = group.Key,
                };

                foreach (var block in group)
                {
                    var connector = block as IMyShipConnector;
                    if (connector != null)
                    {
                        if (connector.CustomName.Contains(config.ExchangeMainConnector)) exchangeGroup.MainConnector = connector;
                        else if (connector.CustomName.Contains(config.ExchangeOtherConnector)) exchangeGroup.Connectors.Add(connector);

                        continue;
                    }

                    var camera = block as IMyCameraBlock;
                    if (camera != null)
                    {
                        exchangeGroup.Camera = camera;
                    }
                }

                if (exchangeGroup.IsValid())
                {
                    WriteLog($"ExchangeGroup {exchangeGroup.Name} initialized.");
                    exchanges.Add(exchangeGroup);
                }
                else
                {
                    WriteLog($"ExchangeGroup {exchangeGroup.Name} is invalid.");
                }
            }
        }

        void EnqueueExchangeRequest(string ship, ExchangeTasks task)
        {
            exchangeRequests.RemoveAll(r => r.Ship == ship);

            exchangeRequests.Add(new ExchangeRequest
            {
                Ship = ship,
                Idle = true,
                Task = task,
            });
        }
        List<ExchangeRequest> GetPendingExchangeDockRequests()
        {
            return exchangeRequests.Where(r => r.Idle && (r.Task == ExchangeTasks.StartLoad || r.Task == ExchangeTasks.StartUnload)).ToList();
        }
        List<ExchangeRequest> GetPendingExchangeUndockRequests()
        {
            return exchangeRequests.Where(r => r.Idle && (r.Task == ExchangeTasks.EndLoad || r.Task == ExchangeTasks.EndUnload)).ToList();
        }
        List<ExchangeGroup> GetFreeExchanges()
        {
            return exchanges.Where(e => e.IsFree()).ToList();
        }
        List<Ship> GetWaitingShips()
        {
            return ships.Where(s => s.Status == ShipStatus.WaitingDock).ToList();
        }

        void PrintExchanges()
        {
            if (!config.ShowExchanges) return;

            sbData.AppendLine();
            sbData.AppendLine("EXCHANGE STATUS");

            if (exchanges.Count == 0)
            {
                sbData.AppendLine("No exchanges available.");
                return;
            }

            foreach (var exchange in exchanges)
            {
                bool isFree = exchange.IsFree();
                string status = isFree ? "Free" : string.Join(", ", exchange.DockedShips());

                sbData.AppendLine($"Exchange {exchange.Name} - {status}");
            }
        }
        void PrintShipStatus()
        {
            if (!config.ShowShips) return;

            sbData.AppendLine();
            sbData.AppendLine("SHIPS STATUS");

            if (ships.Count == 0)
            {
                sbData.AppendLine("No ships available.");
                return;
            }

            foreach (var ship in ships)
            {
                sbData.AppendLine($"+{ship.Name} - {ship.Status}. Cargo at {ship.Cargo:P1}. Last update: {(DateTime.Now - ship.UpdateTime).TotalSeconds:F0}secs");
                sbData.AppendLine(ship.StatusMessage);
                sbData.AppendLine(Separate);
            }

            //Remove ships that have not been updated in a while
            ships.RemoveAll(s => (DateTime.Now - s.UpdateTime).TotalMinutes > 5);
        }
        void PrintExchangeRequests()
        {
            if (!config.ShowExchangeRequests) return;

            sbData.AppendLine();
            sbData.AppendLine("EXCHANGE REQUESTS");

            if (exchangeRequests.Count == 0)
            {
                sbData.AppendLine("No requests available.");
                return;
            }

            foreach (var req in exchangeRequests)
            {
                string unloadStatus = req.Idle ? "Pending" : "On route";
                sbData.AppendLine($"{req.Ship} {req.Task}. {unloadStatus}");
            }
        }
        #endregion

        void LoadFromStorage()
        {
            exchangeRequests.Clear();

            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0) return;

            ExchangeRequest.LoadListFromStorage(storageLines, exchangeRequests);
            ExchangeGroup.LoadListFromStorage(storageLines, exchanges);
            requestStatus = Utils.ReadInt(storageLines, "RequestStatus", 1) == 1;
            requestExchange = Utils.ReadInt(storageLines, "RequestReception", 1) == 1;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>();
            parts.AddRange(ExchangeRequest.SaveListToStorage(exchangeRequests));
            parts.AddRange(ExchangeGroup.SaveListToStorage(exchanges));
            parts.Add($"RequestStatus={(requestStatus ? 1 : 0)}");
            parts.Add($"RequestReception={(requestExchange ? 1 : 0)}");

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
