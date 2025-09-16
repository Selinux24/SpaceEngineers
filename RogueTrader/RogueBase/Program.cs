using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
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
        const string Version = "1.9";
        const string Separate = "------";

        #region Blocks
        readonly IMyBroadcastListener bl;
        readonly List<IMyTextSurface> dataLCDs = new List<IMyTextSurface>();
        readonly List<TextPanelDesc> logLCDs = new List<TextPanelDesc>();
        #endregion

        readonly string baseId;
        readonly Config config;

        readonly StringBuilder sbData = new StringBuilder();
        readonly StringBuilder sbLog = new StringBuilder();

        readonly List<ExchangeGroup> exchanges = new List<ExchangeGroup>();
        readonly List<Ship> ships = new List<Ship>();
        readonly List<Plan> plans = new List<Plan>();
        readonly List<ExchangeRequest> exchangeRequests = new List<ExchangeRequest>();

        DateTime lastRequestStatus = DateTime.MinValue;
        DateTime lastExchangeRequest = DateTime.MinValue;
        DateTime lastRefreshLCDs = DateTime.MinValue;

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

            RefreshLCDs();

            InitializeExchangeGroups();

            bl = IGC.RegisterBroadcastListener(config.Channel);

            LoadFromStorage();

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
            WriteDataLCDs($"RogueBase v{Version}. {DateTime.Now:HH:mm:ss}", true);
            WriteDataLCDs($"{baseId} in channel {config.Channel}", true);
            if (config.EnableRequestStatus) WriteDataLCDs($"Next Status Request {GetNextStatusRequest():hh\\:mm\\:ss}", true);
            if (config.EnableRequestExchange) WriteDataLCDs($"Next Exchange Request {GetNextExchangeRequest():hh\\:mm\\:ss}", true);
            if (config.EnableRefreshLCDs) WriteDataLCDs($"Next LCDs Refresh {GetNextLCDsRefresh():hh\\:mm\\:ss}", true);

            PrintExchanges();
            PrintShipStatus();
            PrintExchangeRequests();
            PrintShipPlans();
            FlushDataLCDs();
            FlushLogLCDs();
        }

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();

            else if (argument == "LIST_EXCHANGES") ListExchanges();
            else if (argument == "LIST_SHIPS") ListShips();
            else if (argument == "LIST_EXCHANGE_REQUESTS") ListExchangeRequests();
            else if (argument == "LIST_PLANS") ListPlans();

            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "ENABLE_STATUS_REQUEST") EnableStatusRequest();
            else if (argument == "ENABLE_EXCHANGE_REQUEST") EnableExchangeRequest();
            else if (argument == "ENABLE_REFRESH_LCDS") EnableRefreshLCDs();

            else if (argument == "LIST_PLAN") RequestShipPlan();
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
        /// Changes the state of the variable that controls the display of exchanges
        /// </summary>
        void ListExchanges()
        {
            config.ShowExchanges = !config.ShowExchanges;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of ships
        /// </summary>
        void ListShips()
        {
            config.ShowShips = !config.ShowShips;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of exchange requests
        /// </summary>
        void ListExchangeRequests()
        {
            config.ShowExchangeRequests = !config.ShowExchangeRequests;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of the flight plans
        /// </summary>
        void ListPlans()
        {
            config.ShowPlans = !config.ShowPlans;
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
            config.EnableRequestStatus = !config.EnableRequestStatus;
        }
        /// <summary>
        /// Changes the state of the variable that controls the exchange requests
        /// </summary>
        void EnableExchangeRequest()
        {
            config.EnableRequestExchange = !config.EnableRequestExchange;
        }
        /// <summary>
        /// Changes the state of the variable that controls the refresh of LCDs
        /// </summary>
        void EnableRefreshLCDs()
        {
            config.EnableRefreshLCDs = !config.EnableRefreshLCDs;
        }
        /// <summary>
        /// Requests all SHIPs to send its plan
        /// </summary>
        void RequestShipPlan()
        {
            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_PLAN",
                $"From={baseId}",
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

            if (!IsForMe(lines)) return;

            if (command == "RESPONSE_STATUS") ProcessResponseStatus(lines);
            if (command == "RESPONSE_PLAN") ProcessResponsePlan(lines);

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
        /// BASE updates the SHIP plan
        /// </summary>
        void ProcessResponsePlan(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            var position = Utils.ReadVector(lines, "Position");
            var plan = Utils.ReadStringList(lines, "Plan");

            plans.RemoveAll(p => p.Ship == from);
            plans.Add(new Plan()
            {
                Ship = from,
                Position = position,
                GPSList = plan,
            });
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
            if (!config.EnableRequestStatus)
            {
                return;
            }

            if (DateTime.Now - lastRequestStatus < config.RequestStatusInterval)
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
            if (!config.EnableRequestExchange) return;

            if (DateTime.Now - lastExchangeRequest < config.RequestReceptionInterval) return;
            lastExchangeRequest = DateTime.Now;

            if (IsCurrentExchangeDockRequests()) return;

            if (DoExchangeUndockRequest()) return;

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
            WriteLogLCDs($"Exchange requests: {exRequest.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {waitingShips.Count}");

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

                var exchange = pair.Exchange;

                request.SetDoing();
                exchange.DockRequest(request.Ship);

                string command = null;
                switch (request.Task)
                {
                    case ExchangeTasks.StartLoad:
                        command = "COME_TO_LOAD";
                        exchange.TimerLoad?.StartCountdown();
                        break;
                    case ExchangeTasks.StartUnload:
                        command = "COME_TO_UNLOAD";
                        exchange.TimerUnload?.StartCountdown();
                        break;
                }

                List<string> parts = new List<string>()
                {
                    $"Command={command}",
                    $"To={request.Ship}",
                    $"From={baseId}",

                    $"Landing={(config.InGravity?1:0)}",

                    $"Exchange={exchange.Name}",
                    $"Forward={Utils.VectorToStr(exchange.Forward)}",
                    $"Up={Utils.VectorToStr(exchange.Up)}",
                    $"Waypoints={Utils.VectorListToStr(exchange.CalculateRouteToConnector())}",
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

                request.SetDoing();

                string command = null;
                switch (request.Task)
                {
                    case ExchangeTasks.EndLoad:
                        command = "UNDOCK_TO_LOAD";
                        break;
                    case ExchangeTasks.EndUnload:
                        command = "UNDOCK_TO_UNLOAD";
                        break;
                }

                List<string> parts = new List<string>()
                {
                    $"Command={command}",
                    $"To={request.Ship}",
                    $"From={baseId}",

                    $"Landing={(config.InGravity?1:0)}",

                    $"Exchange={exchange.Name}",
                    $"Forward={Utils.VectorToStr(exchange.Forward)}",
                    $"Up={Utils.VectorToStr(exchange.Up)}",
                    $"Waypoints={Utils.VectorListToStr(exchange.CalculateRouteFromConnector())}",
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

            foreach (var e in exchanges)
            {
                e.Update(time);
            }

            foreach (var r in exchangeRequests)
            {
                if (r.Expired) continue;

                if (string.IsNullOrWhiteSpace(r.Ship)) continue;

                if (r.Task != ExchangeTasks.StartLoad && r.Task != ExchangeTasks.StartUnload) continue;

                if (ShipIsDocked(r.Ship)) r.SetDone();
            }

            FreeExchanges();

            DoRefreshLCDs();
        }
        void DoRefreshLCDs()
        {
            if (!config.EnableRefreshLCDs)
            {
                return;
            }

            if (DateTime.Now - lastRefreshLCDs > config.RefreshLCDsInterval)
            {
                RefreshLCDs();
            }
        }
        void RefreshLCDs()
        {
            dataLCDs.Clear();
            var data = GetBlocksOfType<IMyTextPanel>(config.DataLCDs);
            var dataCps = GetBlocksImplementType<IMyTextSurfaceProvider>(config.DataLCDs).Where(c => config.DataLCDs.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Success);
            dataLCDs.AddRange(data);
            dataLCDs.AddRange(dataCps.Select(c => c.GetSurface(int.Parse(config.DataLCDs.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Value))));

            logLCDs.Clear();
            var log = GetBlocksOfType<IMyTextPanel>(config.LogLCDs);
            var logCps = GetBlocksImplementType<IMyTextSurfaceProvider>(config.LogLCDs).Where(c => config.LogLCDs.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Success);
            logLCDs.AddRange(log.Select(l => new TextPanelDesc(l, l)));
            logLCDs.AddRange(logCps.Select(c => new TextPanelDesc((IMyTerminalBlock)c, c.GetSurface(int.Parse(config.LogLCDs.Match(((IMyTerminalBlock)c).CustomName).Groups[1].Value)))));

            lastRefreshLCDs = DateTime.Now;
        }
        #endregion

        #region UTILITY
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

        void WriteDataLCDs(string text, bool echo = false)
        {
            if (echo)
            {
                Echo(text);
            }

            sbData.AppendLine(text);
        }
        void WriteLogLCDs(string text)
        {
            if (!config.EnableLogs)
            {
                return;
            }

            sbLog.Insert(0, text + Environment.NewLine);
        }
        void FlushDataLCDs()
        {
            string text = sbData.ToString();
            foreach (var lcd in dataLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, false);
            }
        }
        void FlushLogLCDs()
        {
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

            WriteLogLCDs($"SendIGCMessage: {message}");

            IGC.SendBroadcastMessage(config.Channel, message);
        }

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == baseId;
        }

        TimeSpan GetNextStatusRequest()
        {
            return lastRequestStatus + config.RequestStatusInterval - DateTime.Now;
        }

        void InitializeExchangeGroups()
        {
            exchanges.Clear();

            var regEx = config.ExchangesRegex;

            //Find all blocks that have the exchange regex in their name
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, i => i.CubeGrid == Me.CubeGrid && Utils.IsFromGroup(i.CustomName, regEx));

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
                    }

                    var camera = block as IMyCameraBlock;
                    if (camera != null)
                    {
                        exchangeGroup.Camera = camera;
                    }

                    var timer = block as IMyTimerBlock;
                    if (timer != null)
                    {
                        if (timer.CustomName.Contains(config.ExchangeTimerLoad)) exchangeGroup.TimerLoad = timer;
                        else if (timer.CustomName.Contains(config.ExchangeTimerUnload)) exchangeGroup.TimerUnload = timer;
                        else if (timer.CustomName.Contains(config.ExchangeTimerFree)) exchangeGroup.TimerFree = timer;
                    }
                }

                if (exchangeGroup.IsValid())
                {
                    WriteLogLCDs($"ExchangeGroup {exchangeGroup.Name} initialized.");
                    exchanges.Add(exchangeGroup);
                }
                else
                {
                    WriteLogLCDs($"ExchangeGroup {exchangeGroup.Name} is invalid.");
                }
            }
        }
        void EnqueueExchangeRequest(string ship, ExchangeTasks task)
        {
            exchangeRequests.RemoveAll(r => r.Ship == ship);

            exchangeRequests.Add(new ExchangeRequest(config)
            {
                Ship = ship,
                Task = task,
            });
        }
        TimeSpan GetNextExchangeRequest()
        {
            return lastExchangeRequest + config.RequestReceptionInterval - DateTime.Now;
        }
        bool IsCurrentExchangeDockRequests()
        {
            return exchangeRequests.Any(r => r.Doing);
        }
        List<ExchangeRequest> GetPendingExchangeDockRequests()
        {
            return exchangeRequests.Where(r => r.Pending && (r.Task == ExchangeTasks.StartLoad || r.Task == ExchangeTasks.StartUnload)).ToList();
        }
        List<ExchangeRequest> GetPendingExchangeUndockRequests()
        {
            return exchangeRequests.Where(r => r.Pending && (r.Task == ExchangeTasks.EndLoad || r.Task == ExchangeTasks.EndUnload)).ToList();
        }
        List<ExchangeGroup> GetFreeExchanges()
        {
            return exchanges.Where(e => e.IsFree()).ToList();
        }
        List<Ship> GetWaitingShips()
        {
            return ships.Where(s => s.Status == ShipStatus.WaitingDock).ToList();
        }
        bool ShipIsDocked(string ship)
        {
            if (string.IsNullOrWhiteSpace(ship)) return false;

            return exchanges.Any(e => e.DockedShips().Any(d => d == ship));
        }
        List<ExchangeGroup> GetShipExchanges(string ship)
        {
            if (string.IsNullOrWhiteSpace(ship)) return new List<ExchangeGroup>();

            return exchanges.Where(e => e.DockedShips().Any(d => d == ship)).Distinct().ToList();
        }
        void FreeExchanges()
        {
            var exGroups = new List<ExchangeGroup>();
            foreach (var r in exchangeRequests)
            {
                if (!r.Expired) continue;

                exGroups.AddRange(GetShipExchanges(r.Ship));
            }

            foreach (var e in exGroups.Distinct())
            {
                e.TimerFree?.StartCountdown();
            }

            exchangeRequests.RemoveAll(r => r.Expired);
        }

        TimeSpan GetNextLCDsRefresh()
        {
            return lastRefreshLCDs + config.RefreshLCDsInterval - DateTime.Now;
        }

        void PrintExchanges()
        {
            if (!config.ShowExchanges) return;

            WriteDataLCDs("");
            WriteDataLCDs("EXCHANGE STATUS");

            if (exchanges.Count == 0)
            {
                WriteDataLCDs("No exchanges available.");
                return;
            }

            foreach (var exchange in exchanges)
            {
                WriteDataLCDs($"Exchange {exchange.Name} - {exchange.GetState()}");
            }
        }
        void PrintShipStatus()
        {
            if (!config.ShowShips) return;

            WriteDataLCDs("");
            WriteDataLCDs("SHIPS STATUS");

            if (ships.Count == 0)
            {
                WriteDataLCDs("No ships available.");
                return;
            }

            var last = ships[ships.Count - 1];
            foreach (var ship in ships)
            {
                WriteDataLCDs($"+{ship.Name} - {ship.Status}. Cargo at {ship.Cargo:P1}. Last update: {(DateTime.Now - ship.UpdateTime).TotalSeconds:F0}secs");
                if (!string.IsNullOrWhiteSpace(ship.StatusMessage)) WriteDataLCDs(ship.StatusMessage);
                if (ship != last) WriteDataLCDs(Separate);
            }

            //Remove ships that have not been updated in a while
            ships.RemoveAll(s => (DateTime.Now - s.UpdateTime).TotalMinutes > 5);
        }
        void PrintExchangeRequests()
        {
            if (!config.ShowExchangeRequests) return;

            WriteDataLCDs("");
            WriteDataLCDs("EXCHANGE REQUESTS");

            if (exchangeRequests.Count == 0)
            {
                WriteDataLCDs("No requests available.");
                return;
            }

            foreach (var req in exchangeRequests)
            {
                string unloadStatus = req.Pending ? "Pending" : "On route";
                WriteDataLCDs($"{req.Ship} {req.Task}. {unloadStatus}");
            }
        }
        void PrintShipPlans()
        {
            if (!config.ShowPlans) return;

            WriteDataLCDs("");
            WriteDataLCDs("FLIGHT PLANS");

            if (plans.Count == 0)
            {
                WriteDataLCDs("No plans available.");
                return;
            }

            var last = plans[plans.Count - 1];
            foreach (var plan in plans)
            {
                WriteDataLCDs($"+{plan.Ship}");
                WriteDataLCDs(plan.GetWaypoints());
                if (plan != last) WriteDataLCDs(Separate);
            }

            //Remove ships that have not been updated in a while
            ships.RemoveAll(s => (DateTime.Now - s.UpdateTime).TotalMinutes > 5);
        }
        #endregion

        void LoadFromStorage()
        {
            exchangeRequests.Clear();

            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0) return;

            ExchangeRequest.LoadListFromStorage(config, storageLines, exchangeRequests);
            ExchangeGroup.LoadListFromStorage(storageLines, exchanges);
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>();
            parts.AddRange(ExchangeRequest.SaveListToStorage(exchangeRequests));
            parts.AddRange(ExchangeGroup.SaveListToStorage(exchanges));

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
