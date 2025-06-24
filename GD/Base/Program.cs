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
    /// Base script for managing ship deliveries in Space Engineers.
    /// </summary>
    partial class Program : MyGridProgram
    {
        #region Blocks
        readonly IMyCameraBlock camera;
        readonly List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
        readonly IMyBroadcastListener bl;
        readonly List<IMyTextPanel> dataLCDs = new List<IMyTextPanel>();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        #endregion

        readonly string baseId;
        readonly Config config;

        readonly StringBuilder sbData = new StringBuilder();
        readonly StringBuilder sbLog = new StringBuilder();

        readonly List<ExchangeGroup> exchanges = new List<ExchangeGroup>();
        readonly List<Order> orders = new List<Order>();
        readonly List<Ship> ships = new List<Ship>();
        readonly List<ExchangeRequest> exchangeRequests = new List<ExchangeRequest>();

        bool showExchanges = true;
        bool showShips = true;
        bool showOrders = true;
        bool showExchangeRequests = true;
        bool enableLogs = false;

        bool requestStatus = true;
        DateTime lastRequestStatus = DateTime.MinValue;

        bool requestDelivery = true;
        DateTime lastRequestDelivery = DateTime.MinValue;

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

            GridTerminalSystem.GetBlocksOfType(cargos, cargo => cargo.CubeGrid == Me.CubeGrid && cargo.CustomName.Contains(config.BaseWarehouses));
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

        public void Main(string argument, UpdateType updateSource)
        {
            DoRequestStatus();
            DoRequestDelivery();
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
            PrintOrders();
            PrintExchangeRequests();
            WriteDataLCDs(sbData.ToString(), false);
        }

        #region STATUS
        /// <summary>
        /// Seq_A_1 - WH requests status from all ships
        /// Execute:  REQUEST_STATUS
        /// </summary>
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

        #region DELIVERY
        /// <summary>
        /// Seq_C_1 - WH reviews the orders. For each order, it searches for a free ship and gives the loading order to NAVEX on that connector.
        /// Execute:  START_DELIVERY
        /// </summary>
        void DoRequestDelivery()
        {
            if (!requestDelivery)
            {
                return;
            }

            if (DateTime.Now - lastRequestDelivery < TimeSpan.FromSeconds(config.RequestDeliveryInterval))
            {
                return;
            }
            lastRequestDelivery = DateTime.Now;

            var pendantOrders = GetPendingOrders();
            if (pendantOrders.Count == 0)
            {
                return;
            }
            var freeExchanges = GetFreeExchanges();
            if (freeExchanges.Count == 0)
            {
                return;
            }
            var freeShips = GetFreeShips();
            if (freeShips.Count == 0)
            {
                DoRequestStatus();
                return;
            }
            WriteLogLCDs($"Deliveries: {pendantOrders.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {freeShips.Count}");

            var shipExchangePair = GetNearestShipsFromExchanges(freeShips, freeExchanges).FirstOrDefault();
            if (shipExchangePair == null)
            {
                return;
            }

            var order = pendantOrders[0];
            var ship = shipExchangePair.Ship;
            var exchange = shipExchangePair.Exchange;

            order.AssignedShip = ship.Name;
            ship.ShipStatus = ShipStatus.ApproachingLoad;
            exchange.DockRequest(ship.Name);

            List<string> parts = new List<string>()
            {
                $"Command=START_DELIVERY",
                $"To={ship.Name}",
                $"Warehouse={order.Warehouse}",
                $"WarehouseParking={Utils.VectorToStr(order.WarehouseParking)}",
                $"Customer={order.Customer}",
                $"CustomerParking={Utils.VectorToStr(order.CustomerParking)}",
                $"Order={order.Id}",
            };
            BroadcastMessage(parts);

            //Puts the exchange in prepare order mode
            exchange.TimerPrepare?.StartCountdown();
        }
        #endregion

        #region EXCHANGE REQUEST
        /// <summary>
        /// Seq_D_1 - BASEX reviews exchange requests. It searches for free connectors and gives SHIPX the exchange command on the specified connector.
        /// Execute:  UNLOAD_ORDER
        /// </summary>
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
            WriteLogLCDs($"Exchange requests: {exRequest.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {waitingShips.Count}");

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
                ship.ShipStatus = ShipStatus.ApproachingUnload;
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

        #region TERMINAL COMMANDS
        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "RESET") Reset();
            else if (argument == "LIST_EXCHANGES") ListExchanges();
            else if (argument == "LIST_SHIPS") ListShips();
            else if (argument == "LIST_ORDERS") ListOrders();
            else if (argument == "LIST_EXCHANGE_REQUESTS") ListExchangeRequests();
            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "ENABLE_STATUS_REQUEST") EnableStatusRequest();
            else if (argument == "ENABLE_DELIVERY_REQUEST") EnableDeliveryRequest();
            else if (argument == "ENABLE_EXCHANGE_REQUEST") EnableExchangeRequest();
            else if (argument.StartsWith("SHIP_LOADED")) ShipLoaded(argument);
            else if (argument.StartsWith("SET_ORDER")) SetOrder(argument);
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
            orders.Clear();
            ships.Clear();
            exchangeRequests.Clear();

            InitializeExchangeGroups();
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of exchanges
        /// </summary>
        void ListExchanges()
        {
            showExchanges = !showExchanges;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of ships
        /// </summary>
        void ListShips()
        {
            showShips = !showShips;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of orders
        /// </summary>
        void ListOrders()
        {
            showOrders = !showOrders;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of exchange requests
        /// </summary>
        void ListExchangeRequests()
        {
            showExchangeRequests = !showExchangeRequests;
        }
        /// <summary>
        /// Changes the state of the variable that controls the display of logs
        /// </summary>
        void EnableLogs()
        {
            enableLogs = !enableLogs;
        }
        /// <summary>
        /// Changes the state of the variable that controls the status request to the ships
        /// </summary>
        void EnableStatusRequest()
        {
            requestStatus = !requestStatus;
        }
        /// <summary>
        /// Changes the state of the variable that controls the order delivery request
        /// </summary>
        void EnableDeliveryRequest()
        {
            requestDelivery = !requestDelivery;
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
        /// NEW - Sets an empty order for full loading in warehouse, and full unloading in costumer
        /// </summary>
        void SetOrder(string argument)
        {
            string[] lines = argument.Split('|');

            string warehouse = Utils.ReadString(lines, "Warehouse");
            string warehouseParking = Utils.ReadString(lines, "WarehouseParking");

            Order order = new Order
            {
                Warehouse = warehouse,
                WarehouseParking = Utils.StrToVector(warehouseParking),
                Customer = baseId,
                CustomerParking = Utils.StrToVector(config.BaseParking),
            };

            orders.Add(order);
        }
        #endregion

        #region IGC COMMANDS
        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = Utils.ReadArgument(lines, "Command");

            if (command == "RESPONSE_STATUS") CmdResponseStatus(lines);
            else if (command == "REQUEST_ORDER") CmdRequestOrder(lines);
            else if (command == "REQUEST_LOAD") CmdRequestLoad(lines);
            else if (command == "LOADING") CmdLoading(lines);
            else if (command == "REQUEST_UNLOAD") CmdRequestUnload(lines);
            else if (command == "UNLOADING") CmdUnloading(lines);
            else if (command == "UNLOADED") CmdUnloaded(lines);
            else if (command == "ORDER_RECEIVED") CmdOrderReceived(lines);
            else if (command == "REQUEST_DOCK") CmdRequestDock(lines);
        }
        /// <summary>
        /// Seq_A_3 - WH updates the ship's status
        /// Request:  RESPONSE_STATUS
        /// </summary>
        void CmdResponseStatus(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = Utils.ReadString(lines, "From");
            ShipStatus status = (ShipStatus)Utils.ReadInt(lines, "Status");
            string wh = Utils.ReadString(lines, "Warehouse");
            Vector3D whPosition = Utils.ReadVector(lines, "WarehousePosition");
            string customer = Utils.ReadString(lines, "Customer");
            Vector3D customerPosition = Utils.ReadVector(lines, "CustomerPosition");
            Vector3D position = Utils.ReadVector(lines, "Position");
            double speed = Utils.ReadDouble(lines, "Speed");

            var ship = ships.Find(s => s.Name == from);
            if (ship == null)
            {
                ship = new Ship() { Name = from };
                ships.Add(ship);
            }

            ship.ShipStatus = status;
            ship.Warehouse = wh;
            ship.WarehousePosition = whPosition;
            ship.Customer = customer;
            ship.CustomerPosition = customerPosition;
            ship.Position = position;
            ship.Speed = speed;
            ship.UpdateTime = DateTime.Now;
        }
        /// <summary>
        /// Seq_B_2 - WH records the order (order list)
        /// Request:  REQUEST_ORDER
        /// </summary>
        void CmdRequestOrder(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string customer = Utils.ReadString(lines, "Customer");
            Vector3D customerParking = Utils.ReadVector(lines, "CustomerParking");
            string items = Utils.ReadString(lines, "Items");

            Order order = new Order
            {
                Warehouse = baseId,
                WarehouseParking = Utils.StrToVector(config.BaseParking),
                Customer = customer,
                CustomerParking = customerParking,
            };

            foreach (var item in items.Split(';'))
            {
                var parts = item.Split(':');
                if (parts.Length != 2) continue;
                string itemName = parts[0];
                int itemAmount;
                if (!int.TryParse(parts[1], out itemAmount)) continue;

                if (order.Items.ContainsKey(itemName))
                {
                    order.Items[itemName]++;
                }
                else
                {
                    order.Items[itemName] = itemAmount;
                }
            }

            orders.Add(order);
        }
        /// <summary>
        /// Seq_XXX - BASEX registers exchange request (exchange request list)
        /// Request:  REQUEST_LOAD
        /// </summary>
        void CmdRequestLoad(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = Utils.ReadString(lines, "From");
            int orderId = Utils.ReadInt(lines, "Order");

            exchangeRequests.RemoveAll(r => r.Ship == from);

            exchangeRequests.Add(new ExchangeRequest
            {
                Ship = from,
                OrderId = orderId,
                Idle = true,
                Task = ExchangeTasks.DeliveryLoad,
            });
        }
        /// <summary>
        /// Seq_C_3a - SHIPX notifies WH that it has arrived to load the ORDER_ID into the connector and WH does the loading.
        /// Request:  LOADING
        /// </summary>
        void CmdLoading(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = Utils.ReadString(lines, "From");
            int orderId = Utils.ReadInt(lines, "Order");
            string exchangeName = Utils.ReadString(lines, "Exchange");

            var order = orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
            {
                return;
            }
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            if (exchange == null)
            {
                return;
            }

            string moveLog = exchange.MoveCargo(order, cargos);
            WriteLogLCDs(moveLog);

            exchange.TimerUnload?.StartCountdown();
        }
        /// <summary>
        /// Seq_C_5 - BASEX registers exchange request (exchange request list)
        /// Request:  REQUEST_UNLOAD
        /// </summary>
        void CmdRequestUnload(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = Utils.ReadString(lines, "From");
            int orderId = Utils.ReadInt(lines, "Order");

            exchangeRequests.RemoveAll(r => r.Ship == from);

            exchangeRequests.Add(new ExchangeRequest
            {
                Ship = from,
                OrderId = orderId,
                Idle = true,
                Task = ExchangeTasks.DeliveryUnload,
            });
        }
        /// <summary>
        /// Seq_D_2c - BASEX puts the exchange in unload mode
        /// </summary>
        void CmdUnloading(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string exchangeName = Utils.ReadString(lines, "Exchange");
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            if (exchange == null)
            {
                return;
            }

            exchange.TimerUnload?.StartCountdown();
        }
        /// <summary>
        /// Seq_D_3 - BASEX records that order ORDER_ID has been unloaded and removes it from the exchange requests list. It sends [ORDER_RECEIVED] to WH
        /// Request:  UNLOADED
        /// Execute:  ORDER_RECEIVED
        /// </summary>
        void CmdUnloaded(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string warehouse = Utils.ReadString(lines, "Warehouse");

            //Eliminar la orden de descarga del pedido
            int orderId = Utils.ReadInt(lines, "Order");
            var req = exchangeRequests.FirstOrDefault(o => o.OrderId == orderId);
            if (req != null)
            {
                exchangeRequests.Remove(req);
            }

            //Enviar al WH el mensaje de que se ha recibido el pedido
            List<string> parts = new List<string>()
            {
                $"Command=ORDER_RECEIVED",
                $"To={warehouse}",
                $"From={baseId}",
                $"Order={orderId}",
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// Seq_D_4 - WH records that order ORDER_ID has been unloaded and removes it from the order list
        /// </summary>
        void CmdOrderReceived(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            //Eliminar el pedido de la lista
            int orderId = Utils.ReadInt(lines, "Order");
            var order = orders.FirstOrDefault(o => o.Id == orderId);
            if (order != null)
            {
                orders.Remove(order);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        void CmdRequestDock(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = Utils.ReadString(lines, "From");
            ExchangeTasks task = (ExchangeTasks)Utils.ReadInt(lines, "Task");

            exchangeRequests.Add(new ExchangeRequest
            {
                Ship = from,
                OrderId = -1,
                Idle = true,
                Task = task,
            });
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

            var dockedShips = exchanges
                .Select(e => e.UpperShipName)
                .Concat(exchanges.Select(e => e.LowerShipName))
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

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
                        if (connector.CustomName.Contains(config.ExchangeUpperConnector)) exchangeGroup.UpperConnector = connector;
                        else if (connector.CustomName.Contains(config.ExchangeLowerConnector)) exchangeGroup.LowerConnector = connector;

                        continue;
                    }

                    var cargo = block as IMyCargoContainer;
                    if (cargo != null)
                    {
                        exchangeGroup.Cargo = cargo;
                        continue;
                    }

                    var sorter = block as IMyConveyorSorter;
                    if (sorter != null)
                    {
                        if (sorter.CustomName.Contains(config.ExchangeSorterInput)) exchangeGroup.SorterInput = sorter;
                        else if (sorter.CustomName.Contains(config.ExchangeSorterOutput)) exchangeGroup.SorterOutput = sorter;
                        continue;
                    }

                    var timer = block as IMyTimerBlock;
                    if (timer != null)
                    {
                        if (timer.CustomName.Contains(config.ExchangeTimerPrepare)) exchangeGroup.TimerPrepare = timer;
                        else if (timer.CustomName.Contains(config.ExchangeTimerUnload)) exchangeGroup.TimerUnload = timer;
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

        List<ExchangeRequest> GetPendingExchangeRequests()
        {
            return exchangeRequests.Where(r => r.Idle && r.Task != ExchangeTasks.None).ToList();
        }
        List<Order> GetPendingOrders()
        {
            return orders.Where(o => string.IsNullOrWhiteSpace(o.AssignedShip)).ToList();
        }
        List<Ship> GetFreeShips()
        {
            return ships.Where(s => s.ShipStatus == ShipStatus.Idle).ToList();
        }
        List<Ship> GetWaitingShips()
        {
            return ships.Where(s =>
                s.ShipStatus == ShipStatus.WaitingForLoad ||
                s.ShipStatus == ShipStatus.WaitingForUnload ||
                s.ShipStatus == ShipStatus.Idle).ToList();
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
                    double distance = Vector3D.Distance(ship.Position, exchange.UpperConnector.GetPosition());
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
            if (!showExchanges) return;

            sbData.AppendLine();
            sbData.AppendLine("EXCHANGE STATUS");

            if (exchanges.Count == 0)
            {
                sbData.AppendLine("No exchanges available.");
                return;
            }

            foreach (var exchange in exchanges)
            {
                var upStatus = exchange.UpperConnector?.Status.ToString() ?? "None";
                var lowStatus = exchange.LowerConnector?.Status.ToString() ?? "None";

                sbData.AppendLine($"Exchange {exchange.Name} - {exchange.DockedShipName ?? "Free"}. A {upStatus} - B {lowStatus}");
            }
        }
        void PrintShipStatus()
        {
            if (!showShips) return;

            sbData.AppendLine();
            sbData.AppendLine("SHIPS STATUS");

            if (ships.Count == 0)
            {
                sbData.AppendLine("No ships available.");
                return;
            }

            foreach (var ship in ships)
            {
                sbData.AppendLine($"+ {ship.Name} {ship.ShipStatus}. Last update: {(DateTime.Now - ship.UpdateTime).TotalSeconds:F0}secs");
                if (ship.ShipStatus != ShipStatus.RouteToUnload && ship.ShipStatus != ShipStatus.RouteToLoad) continue;

                string origin = ship.ShipStatus == ShipStatus.RouteToUnload ? ship.Warehouse : ship.Customer;
                string destination = ship.ShipStatus == ShipStatus.RouteToUnload ? ship.Customer : ship.Warehouse;
                sbData.AppendLine($"  - On route from [{origin}] to [{destination}]");

                Vector3D originPosition = ship.ShipStatus == ShipStatus.RouteToUnload ? ship.WarehousePosition : ship.CustomerPosition;
                Vector3D destinationPosition = ship.ShipStatus == ShipStatus.RouteToUnload ? ship.CustomerPosition : ship.WarehousePosition;
                double distanceToOrigin = Vector3D.Distance(ship.Position, originPosition);
                double distanceToDestination = Vector3D.Distance(ship.Position, destinationPosition);
                TimeSpan time = TimeSpan.FromSeconds(distanceToDestination / ship.Speed);
                sbData.AppendLine($"  - Distance from origin: {Utils.DistanceToStr(distanceToOrigin)}.");
                sbData.AppendLine($"  - Distance to destination: {Utils.DistanceToStr(distanceToDestination)}.");
                sbData.AppendLine($"  - Estimated arrival: {time}.");
            }

            //Remove ships that have not been updated in a while
            ships.RemoveAll(s => (DateTime.Now - s.UpdateTime).TotalMinutes > 5);
        }
        void PrintOrders()
        {
            if (!showOrders) return;

            sbData.AppendLine();
            sbData.AppendLine("ORDERS STATUS");

            if (orders.Count == 0)
            {
                sbData.AppendLine("No orders available.");
                return;
            }

            foreach (var order in orders)
            {
                sbData.AppendLine($"Id[{order.Id}]. {order.AssignedShip} shipping from {order.Customer} to {order.Warehouse}");
            }
        }
        void PrintExchangeRequests()
        {
            if (!showExchangeRequests) return;

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
                if (req.OrderId > 0)
                {
                    sbData.AppendLine($"{req.Ship} {req.Task}. {unloadStatus} order {req.OrderId}");
                }
                else
                {
                    sbData.AppendLine($"{req.Ship} {req.Task}. {unloadStatus}");
                }
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
        void WriteDataLCDs(string text, bool append)
        {
            foreach (var lcd in dataLCDs)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, append);
            }
        }
        void WriteLogLCDs(string text)
        {
            if (!enableLogs)
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

            WriteLogLCDs($"SendIGCMessage: {message}");

            IGC.SendBroadcastMessage(config.Channel, message);
        }
        #endregion

        void LoadFromStorage()
        {
            orders.Clear();
            exchangeRequests.Clear();

            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0) return;

            Order.LoadListFromStorage(storageLines, orders);
            ExchangeRequest.LoadListFromStorage(storageLines, exchangeRequests);
            ExchangeGroup.LoadListFromStorage(storageLines, exchanges);
            showExchanges = Utils.ReadInt(storageLines, "ShowExchanges") == 1;
            showShips = Utils.ReadInt(storageLines, "ShowShips") == 1;
            showOrders = Utils.ReadInt(storageLines, "ShowOrders") == 1;
            showExchangeRequests = Utils.ReadInt(storageLines, "ShowExchangeRequests") == 1;
            enableLogs = Utils.ReadInt(storageLines, "EnableLogs") == 1;
            requestStatus = Utils.ReadInt(storageLines, "RequestStatus", 1) == 1;
            requestDelivery = Utils.ReadInt(storageLines, "RequestDelivery", 1) == 1;
            requestExchange = Utils.ReadInt(storageLines, "RequestReception", 1) == 1;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>();
            parts.AddRange(Order.SaveListToStorage(orders));
            parts.AddRange(ExchangeRequest.SaveListToStorage(exchangeRequests));
            parts.AddRange(ExchangeGroup.SaveListToStorage(exchanges));
            parts.Add($"ShowExchanges={(showExchanges ? 1 : 0)}");
            parts.Add($"ShowShips={(showShips ? 1 : 0)}");
            parts.Add($"ShowOrders={(showOrders ? 1 : 0)}");
            parts.Add($"ShowExchangeRequests={(showExchangeRequests ? 1 : 0)}");
            parts.Add($"EnableLogs={(enableLogs ? 1 : 0)}");
            parts.Add($"RequestStatus={(requestStatus ? 1 : 0)}");
            parts.Add($"RequestDelivery={(requestDelivery ? 1 : 0)}");
            parts.Add($"RequestReception={(requestExchange ? 1 : 0)}");

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
