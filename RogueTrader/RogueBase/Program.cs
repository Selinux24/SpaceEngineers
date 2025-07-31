using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// RogueBase script
    /// </summary>
    partial class Program : MyGridProgram
    {
        const string Separate = "-";

        #region Blocks
        readonly IMyCameraBlock camera;
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

            camera = GetBlockWithName<IMyCameraBlock>(config.BaseCamera);
            if (camera == null)
            {
                Echo("Camera not found.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(dataLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(config.BaseDataLCDs));
            GridTerminalSystem.GetBlocksOfType(logLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(config.BaseLogLCDs));

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
            else if (argument.StartsWith("SHIP_LOADED")) ShipLoaded(argument);
            else if (argument.StartsWith("START_LOAD_ROUTE")) StartLoadRoute(argument);
            else if (argument.StartsWith("START_UNLOAD_ROUTE")) StartUnloadRoute(argument);
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
        /// <summary>
        /// Seq_C_3b - WH completes loading and notifies SHIPX
        /// Request:  SHIP_LOADED
        /// Execute:  LOADED
        /// </summary>
        void ShipLoaded(string argument)
        {
            string[] lines = argument.Split('|');

            string exchangeName = Utils.ReadString(lines, "Exchange");
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            if (exchange == null)
            {
                return;
            }

            List<string> parts = new List<string>()
            {
                $"Command=LOADED",
                $"To={exchange.DockedShipName}",
                $"From={baseId}"
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// The base calls a ship to load cargo.
        /// </summary>
        void StartLoadRoute(string argument)
        {
            string[] lines = argument.Split('|');
            string ship = Utils.ReadString(lines, "Ship");

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_LOAD",
                $"From={baseId}",
                $"To={ship}",
                $"Route={config.BaseParking}",
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// The base calls a ship to unload cargo.
        /// </summary>
        /// <param name="argument"></param>
        void StartUnloadRoute(string argument)
        {
            string[] lines = argument.Split('|');
            string ship = Utils.ReadString(lines, "Ship");

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_UNLOAD",
                $"From={baseId}",
                $"To={ship}",
                $"Route={config.BaseParking}",
            };
            BroadcastMessage(parts);
        }
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLog($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = Utils.ReadArgument(lines, "Command");

            if (!IsForMe(lines)) return;

            if (command == "RESPONSE_STATUS") CmdResponseStatus(lines);

            else if (command == "REQUEST_LOAD") CmdRequestExchange(lines, ExchangeTasks.Load);
            else if (command == "REQUEST_UNLOAD") CmdRequestExchange(lines, ExchangeTasks.Unload);
            else if (command == "UNLOADING") CmdUnloading(lines);

            else if (command == "REQUEST_DOCK") CmdRequestDock(lines);
        }
        /// <summary>
        /// Seq_A_3 - WH updates the ship's status
        /// Request:  RESPONSE_STATUS
        /// </summary>
        void CmdResponseStatus(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            ShipStatus status = (ShipStatus)Utils.ReadInt(lines, "Status");
            var cargo = Utils.ReadDouble(lines, "Cargo");
            var position = Utils.ReadVector(lines, "Position");
            string statusMessage = Utils.ReadString(lines, "StatusMessage");

            var ship = ships.Find(s => s.Name == from);
            if (ship == null)
            {
                ship = new Ship() { Name = from };
                ships.Add(ship);
            }

            ship.Status = status;
            ship.Cargo = cargo;
            ship.Position = position;
            ship.StatusMessage = statusMessage;
            ship.UpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Seq_XXX/Seq_C_5 - BASEX registers exchange request (exchange request list)
        /// Request:  REQUEST_DELIVERY_LOAD/REQUEST_DELIVERY_UNLOAD
        /// </summary>
        void CmdRequestExchange(string[] lines, ExchangeTasks task)
        {
            string from = Utils.ReadString(lines, "From");

            exchangeRequests.RemoveAll(r => r.Ship == from);

            exchangeRequests.Add(new ExchangeRequest
            {
                Ship = from,
                Idle = true,
                Task = task,
            });
        }
        /// <summary>
        /// Seq_D_2c - BASEX puts the exchange in unload mode
        /// </summary>
        void CmdUnloading(string[] lines)
        {
            string exchangeName = Utils.ReadString(lines, "Exchange");
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            if (exchange == null)
            {
                return;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void CmdRequestDock(string[] lines)
        {
            string from = Utils.ReadString(lines, "From");
            ExchangeTasks task = (ExchangeTasks)Utils.ReadInt(lines, "Task");

            exchangeRequests.RemoveAll(r => r.Ship == from);

            exchangeRequests.Add(new ExchangeRequest
            {
                Ship = from,
                Idle = true,
                Task = task,
            });
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
        void DoRequestExchange()
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

            var exRequest = GetPendingExchangeRequests();
            if (exRequest.Count == 0)
            {
                return;
            }
            var freeExchanges = GetFreeExchanges();
            if (freeExchanges.Count == 0)
            {
                return;
            }
            var waitingShips = GetWaitingShips();
            if (waitingShips.Count == 0)
            {
                DoRequestStatus();
                return;
            }
            WriteLog($"Exchange requests: {exRequest.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {waitingShips.Count}");

            var shipExchangePairs = GetNearestShipsFromExchanges(waitingShips, freeExchanges);
            if (shipExchangePairs.Count == 0)
            {
                return;
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

                List<string> parts = new List<string>()
                {
                    $"Command=DOCK",
                    $"To={request.Ship}",
                    $"From={baseId}",
                    $"Parking={config.BaseParking}",
                    $"Landing={(config.IsRocketBase?1:0)}",
                    $"Forward={Utils.VectorToStr(camera.WorldMatrix.Forward)}",
                    $"Up={Utils.VectorToStr(camera.WorldMatrix.Up)}",
                    $"WayPoints={Utils.VectorListToStr(exchange.CalculateRouteToConnector())}",
                    $"Task={(int)request.Task}",
                    $"Exchange={exchange.Name}",
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
        void InitializeExchangeGroups()
        {
            //Find all blocks that have the exchange regex in their name
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, i => i.CubeGrid == Me.CubeGrid && config.ExchangesRegex.IsMatch(i.CustomName));

            //Group them by the group name
            var groups = blocks.GroupBy(b => ExtractGroupName(b.CustomName)).ToList();

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

        bool IsForMe(string[] lines)
        {
            return Utils.ReadString(lines, "To") == baseId;
        }

        List<ExchangeRequest> GetPendingExchangeRequests()
        {
            return exchangeRequests.Where(r => r.Idle && r.Task != ExchangeTasks.None).ToList();
        }
        List<Ship> GetWaitingShips()
        {
            return ships.Where(s =>
                s.Status == ShipStatus.WaitingDock ||
                s.Status == ShipStatus.Idle).ToList();
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

        string ExtractGroupName(string input)
        {
            var match = config.ExchangesRegex.Match(input);
            if (match.Success)
            {
                return match.Value;
            }

            return string.Empty;
        }
        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
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
