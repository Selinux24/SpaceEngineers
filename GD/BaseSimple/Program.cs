using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Base script for managing ship deliveries in Space Engineers.
    /// </summary>
    partial class Program : MyGridProgram
    {
        const string Version = "1.3";
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

            lastRequestStatus = DateTime.MinValue + TimeSpan.FromTicks(Me.EntityId);
            lastExchangeRequest = DateTime.MinValue + TimeSpan.FromTicks(Me.EntityId);
            lastRefreshLCDs = DateTime.MinValue + TimeSpan.FromTicks(Me.EntityId);

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            DoRequestStatus();
            DoRequestExchange();

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
            WriteDataLCDs($"SimpleBase v{Version}. {DateTime.Now:HH:mm:ss}", true);
            WriteDataLCDs($"{baseId} in channel {config.Channel}", true);
            WriteDataLCDs($"Accepting requests up to {Utils.DistanceToStr(config.DockRequestMaxDistance)}", true);
            if (config.EnableRequestStatus) WriteDataLCDs($"Next Status Request {GetNextStatusRequest():hh\\:mm\\:ss}", true);
            if (config.EnableRequestExchange) WriteDataLCDs($"Next Exchange Request {GetNextExchangeRequest():hh\\:mm\\:ss}", true);
            if (config.EnableRefreshLCDs) WriteDataLCDs($"Next LCDs Refresh {GetNextLCDsRefresh():hh\\:mm\\:ss}", true);

            PrintExchanges();
            PrintShipStatus();
            PrintExchangeRequests();
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

            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "ENABLE_STATUS_REQUEST") EnableStatusRequest();
            else if (argument == "ENABLE_EXCHANGE_REQUEST") EnableExchangeRequest();
            else if (argument == "ENABLE_REFRESH_LCDS") EnableRefreshLCDs();
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
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = Utils.ReadArgument(lines, "Command");

            if (command == "REQUEST_DOCK") CmdRequestDock(lines);

            if (!IsForMe(lines)) return;

            if (command == "RESPONSE_STATUS") CmdResponseStatus(lines);
        }

        /// <summary>
        /// Updates the ship's status
        /// </summary>
        void CmdResponseStatus(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            ShipStatus status = (ShipStatus)Utils.ReadInt(lines, "Status");
            var position = Utils.ReadVector(lines, "Position");
            string statusMessage = Utils.ReadString(lines, "StatusMessage");

            var ship = ships.Find(s => s.Name == from);
            if (ship == null)
            {
                ship = new Ship() { Name = from };
                ships.Add(ship);
            }

            ship.Status = status;
            ship.Position = position;
            ship.StatusMessage = statusMessage;
            ship.UpdateTime = DateTime.Now;
        }
        /// <summary>
        /// Appends a dock request from a ship
        /// </summary>
        void CmdRequestDock(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            var position = Utils.ReadVector(lines, "Position");

            EnqueueExchangeRequest(from, position);
        }
        #endregion

        #region STATUS
        /// <summary>
        /// Requests status from all ships
        /// </summary>
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
        /// Base reviews exchange requests. It searches for free connectors and gives a ship the exchange command on the specified connector.
        /// </summary>
        void DoRequestExchange()
        {
            if (!config.EnableRequestExchange) return;

            if (DateTime.Now - lastExchangeRequest < config.RequestReceptionInterval) return;
            lastExchangeRequest = DateTime.Now;

            if (IsCurrentExchangeDockRequests()) return;

            var exRequest = GetPendingExchangeRequests();
            if (exRequest.Count == 0) return;

            var freeExchanges = GetFreeExchanges();
            if (freeExchanges.Count == 0) return;

            var waitingShips = GetWaitingShips();
            if (waitingShips.Count == 0)
            {
                DoRequestStatus();
                return;
            }
            WriteLogLCDs($"Exchange requests: {exRequest.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {waitingShips.Count}");

            var shipExchangePairs = GetNearestShipsFromExchanges(waitingShips, freeExchanges);
            if (shipExchangePairs.Count == 0) return;

            foreach (var request in exRequest)
            {
                var pair = shipExchangePairs.FirstOrDefault(s => s.Ship.Name == request.Ship);
                if (pair == null)
                {
                    continue;
                }

                var exchange = pair.Exchange;

                request.SetDone();
                exchange.DockRequest(request.Ship);

                List<string> parts = new List<string>()
                {
                    $"Command=DOCK",
                    $"To={request.Ship}",
                    $"From={baseId}",

                    $"Exchange={exchange.Name}",
                    $"Forward={Utils.VectorToStr(exchange.Forward)}",
                    $"Up={Utils.VectorToStr(exchange.Up)}",
                    $"Waypoints={Utils.VectorListToStr(exchange.CalculateRouteToConnector())}",
                };
                BroadcastMessage(parts);

                break;
            }
        }
        #endregion

        #region UPDATE BASE STATE
        void UpdateBaseState()
        {
            double time = Runtime.TimeSinceLastRun.TotalSeconds;

            foreach (var exchange in exchanges)
            {
                if (exchange.Update(time)) continue;
            }

            exchangeRequests.RemoveAll(r => r.Expired);

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

        Vector3D GetBasePosition()
        {
            return Me.CubeGrid.GetPosition();
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
                }

                string errMsg;
                if (exchangeGroup.IsValid(out errMsg))
                {
                    WriteLogLCDs($"ExchangeGroup {exchangeGroup.Name} initialized.");
                    exchanges.Add(exchangeGroup);
                }
                else
                {
                    WriteLogLCDs($"ExchangeGroup {exchangeGroup.Name} is invalid. {errMsg}");
                }
            }
        }
        void EnqueueExchangeRequest(string ship, Vector3D position)
        {
            var dist = Vector3D.Distance(position, GetBasePosition());
            if (dist > config.DockRequestMaxDistance)
            {
                WriteLogLCDs($"Dock request from {ship} rejected. Distance {dist} exceeds max {Utils.DistanceToStr(config.DockRequestMaxDistance)}.");
                return;
            }

            exchangeRequests.RemoveAll(r => r.Ship == ship);

            exchangeRequests.Add(new ExchangeRequest(config)
            {
                Ship = ship,
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
        List<ExchangeRequest> GetPendingExchangeRequests()
        {
            return exchangeRequests.Where(r => r.Pending).ToList();
        }
        List<Ship> GetWaitingShips()
        {
            return ships.Where(s => s.Status == ShipStatus.WaitingDock).ToList();
        }
        List<ExchangeGroup> GetFreeExchanges()
        {
            return exchanges.Where(e => e.IsFree()).ToList();
        }
        static List<ShipExchangePair> GetNearestShipsFromExchanges(List<Ship> freeShips, List<ExchangeGroup> freeExchanges)
        {
            var shipExchangePairs = new List<ShipExchangePair>();

            if (freeShips.Count == 1 && freeExchanges.Count == 1)
            {
                shipExchangePairs.Add(new ShipExchangePair
                {
                    Ship = freeShips[0],
                    Exchange = freeExchanges[0],
                });

                return shipExchangePairs;
            }

            foreach (var ship in freeShips)
            {
                foreach (var exchange in freeExchanges)
                {
                    double distance = Vector3D.Distance(ship.Position, exchange.MainConnector.GetPosition());
                    shipExchangePairs.Add(new ShipExchangePair
                    {
                        Ship = ship,
                        Exchange = exchange,
                        Distance = distance
                    });
                }
            }

            return shipExchangePairs.OrderBy(pair => pair.Distance).ToList();
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
                bool isFree = exchange.IsFree();
                string status = isFree ? "Free" : string.Join(", ", exchange.DockedShips());

                WriteDataLCDs($"Exchange {exchange.Name} - {status}");
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
                WriteDataLCDs($"+{ship.Name} - {ship.Status} at {Utils.DistanceToStr(Vector3D.Distance(ship.Position, GetBasePosition()))}. Last UDT: {(DateTime.Now - ship.UpdateTime).TotalSeconds:F0}secs");
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
                WriteDataLCDs($"{req.Ship}. {unloadStatus}");
            }
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
