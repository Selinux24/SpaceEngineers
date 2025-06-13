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
        #region Constants
        const string baseCamera = "Camera";
        const string baseWarehouses = "Warehouse";
        const string baseDataLCDs = "[DELIVERY_DATA]";
        const string baseLogLCDs = "[DELIVERY_LOG]";

        const string exchangeGroupName = @"GR_\w+";
        const string exchangeUpperConnector = "Input";
        const string exchangeLowerConnector = "Output";
        const string exchangeSorterInput = "Input";
        const string exchangeSorterOutput = "Output";
        const string exchangeTimerPrepare = "Prepare";
        const string exchangeTimerUnload = "Unload";

        const int requestStatusInterval = 10; // seconds, how often to request status from ships
        const int requestDeliveryInterval = 60; // seconds, how often to request deliveries
        const int requestReceptionInterval = 60; // seconds, how often to request receptions
        #endregion

        #region Blocks
        readonly IMyCameraBlock camera;
        readonly List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
        readonly IMyBroadcastListener bl;
        readonly List<IMyTextPanel> dataLCDs = new List<IMyTextPanel>();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        #endregion

        readonly StringBuilder sbData = new StringBuilder();
        readonly StringBuilder sbLog = new StringBuilder();

        readonly string baseId;
        readonly string channel;
        readonly string baseParking;
        readonly System.Text.RegularExpressions.Regex exchangesRegex = new System.Text.RegularExpressions.Regex(exchangeGroupName);
        readonly List<ExchangeGroup> exchanges = new List<ExchangeGroup>();
        readonly List<Order> orders = new List<Order>();
        readonly List<Ship> ships = new List<Ship>();
        readonly List<ExchangeRequest> exchangeRequests = new List<ExchangeRequest>();
        readonly bool fakeOrders = false;

        bool showExchanges = true;
        bool showShips = true;
        bool showOrders = true;
        bool showExchangeRequests = true;
        bool enableLogs = false;
        bool requestStatus = true;
        bool requestDelivery = true;
        bool requestReception = true;

        DateTime lastRequestStatus = DateTime.MinValue;
        DateTime lastRequestDelivery = DateTime.MinValue;
        DateTime lastRequestReception = DateTime.MinValue;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "Channel=name\n" +
                    "Parking=x:y:z\n";

                Echo("CustomData not set.");
                return;
            }

            channel = Utils.ReadConfig(Me.CustomData, "Channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                Echo("Channel name not set.");
                return;
            }
            baseParking = Utils.ReadConfig(Me.CustomData, "Parking");
            if (string.IsNullOrWhiteSpace(baseParking))
            {
                Echo("Parking position not set.");
                return;
            }

            baseId = Me.CubeGrid.CustomName;

            InitializeExchangeGroups();

            LoadFromStorage();

            camera = GetBlockWithName<IMyCameraBlock>(baseCamera);
            if (camera == null)
            {
                Echo("Cámara no encontrada.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(cargos, cargo => cargo.CubeGrid == Me.CubeGrid && cargo.CustomName.Contains(baseWarehouses));
            GridTerminalSystem.GetBlocksOfType(dataLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(baseDataLCDs));
            GridTerminalSystem.GetBlocksOfType(logLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(baseLogLCDs));

            WriteLCDs("[baseId]", baseId);

            bl = IGC.RegisterBroadcastListener(channel);
            Echo($"{baseId} in channel {channel}");

            Runtime.UpdateFrequency = UpdateFrequency.Update100; // Ejecuta cada ~1.6s
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            RequestStatus();
            RequestDelivery();
            RequestReception();

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
            sbData.AppendLine($"{baseId} in channel {channel}");
            PrintExchanges();
            PrintShipStatus();
            PrintOrders();
            PrintReceptions();
            WriteDataLCDs(sbData.ToString(), false);
        }

        #region STATUS
        /// <summary>
        /// Sec_A_1 - WH pide situación a todas las naves
        /// Execute:  REQUEST_STATUS
        /// </summary>
        void RequestStatus()
        {
            if (!requestStatus)
            {
                return;
            }

            if (DateTime.Now - lastRequestStatus < TimeSpan.FromSeconds(requestStatusInterval))
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
        /// Sec_C_1 - WH revisa los pedidos. Para cada pedido, busca una nave libre y le da la orden de carga en un conector para NAVEX
        /// Execute:  START_DELIVERY
        /// </summary>
        void RequestDelivery()
        {
            if (!requestDelivery)
            {
                return;
            }

            if (DateTime.Now - lastRequestDelivery < TimeSpan.FromSeconds(requestDeliveryInterval))
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
                RequestStatus();
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
            ship.ShipStatus = ShipStatus.ApproachingWarehouse;
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

            //Pone el exchange en modo preparar pedido
            exchange.TimerPrepare?.ApplyAction("TriggerNow");
        }
        #endregion

        #region RECEPTION
        /// <summary>
        /// Sec_D_1 - BASEX revisa las peticiones de descarga. Busca conectores libres y da la orden de descarga a NAVEX en el conector especificado
        /// Execute:  UNLOAD_ORDER
        /// </summary>
        void RequestReception()
        {
            if (!requestReception)
            {
                return;
            }

            if (DateTime.Now - lastRequestReception < TimeSpan.FromSeconds(requestReceptionInterval))
            {
                return;
            }
            lastRequestReception = DateTime.Now;

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
                RequestStatus();
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
                var pair = shipExchangePairs.FirstOrDefault(s => s.Ship.Name == request.From);
                if (pair == null)
                {
                    continue;
                }

                var ship = pair.Ship;
                var exchange = pair.Exchange;

                request.Idle = false;
                ship.ShipStatus = ShipStatus.ApproachingCustomer;
                exchange.DockRequest(ship.Name);
                string task = request.Task == ExchangeTasks.Loading ? "LOAD_ORDER" : "UNLOAD_ORDER";

                List<string> parts = new List<string>()
                {
                    $"Command={task}",
                    $"To={request.From}",
                    $"From={baseId}",
                    $"Forward={Utils.VectorToStr(camera.WorldMatrix.Forward)}",
                    $"Up={Utils.VectorToStr(camera.WorldMatrix.Up)}",
                    $"WayPoints={Utils.VectorListToStr(exchange.CalculateRouteToConnector())}",
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
            else if (argument == "LIST_RECEPTIONS") ListReceptions();
            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "ENABLE_STATUS_REQUEST") EnableStatusRequest();
            else if (argument == "ENABLE_DELIVERY_REQUEST") EnableDeliveryRequest();
            else if (argument == "ENABLE_RECEPTION_REQUEST") EnableReceptionRequest();
            else if (argument == "FAKE_ORDER") FakeOrder();
            else if (argument.StartsWith("SHIP_LOADED")) ShipLoaded(argument);
            else if (argument.StartsWith("SET_ORDER")) SetOrder(argument);
            else if (argument.StartsWith("REQUEST_DOCK")) RequestDock(argument);
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
        /// Cambia el estado de la variable que controla la visualización de los exchanges
        /// </summary>
        void ListExchanges()
        {
            showExchanges = !showExchanges;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de las naves
        /// </summary>
        void ListShips()
        {
            showShips = !showShips;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de los pedidos
        /// </summary>
        void ListOrders()
        {
            showOrders = !showOrders;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de las recepciones
        /// </summary>
        void ListReceptions()
        {
            showExchangeRequests = !showExchangeRequests;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de los logs
        /// </summary>
        void EnableLogs()
        {
            enableLogs = !enableLogs;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la petición de estado a las naves
        /// </summary>
        void EnableStatusRequest()
        {
            requestStatus = !requestStatus;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la petición de entrega de pedidos
        /// </summary>
        void EnableDeliveryRequest()
        {
            requestDelivery = !requestDelivery;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la petición de recepción de pedidos
        /// </summary>
        void EnableReceptionRequest()
        {
            requestReception = !requestReception;
        }
        /// <summary>
        /// Sec_B_1 - BASEX revisa el inventario y pide a WH
        /// Execute:  REQUEST_ORDER
        /// </summary>
        void FakeOrder()
        {
            if (!fakeOrders)
            {
                return;
            }

            string warehouse = "BaseWarehouse1";

            List<string> parts = new List<string>()
            {
                $"Command=REQUEST_ORDER",
                $"To={warehouse}",
                $"Customer={baseId}",
                $"CustomerParking={baseParking}",
                $"Items=SteelPlate:1000",
            };
            BroadcastMessage(parts);
        }
        /// <summary>
        /// Sec_C_3b - WH termina la carga y avisa a NAVEX
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
                CustomerParking = Utils.StrToVector(baseParking),
            };

            orders.Add(order);
        }
        /// <summary>
        /// NEW - Requests docking of a ship to the base
        /// </summary>
        void RequestDock(string argument)
        {
            string[] lines = argument.Split('|');

            string ship = Utils.ReadString(lines, "Ship");

            ExchangeRequest req = new ExchangeRequest
            {
                From = ship,
                OrderId = 0, // No order for docking
                Idle = true,
                Task = ExchangeTasks.None,
            };

            exchangeRequests.Add(req);
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
        }
        /// <summary>
        /// Sec_A_3 - WH actualiza el estado de la nave
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
        /// Sec_B_2 - WH registra el pedido (lista de pedidos)
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
                WarehouseParking = Utils.StrToVector(baseParking),
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

        void CmdRequestLoad(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = Utils.ReadString(lines, "From");
            int orderId = Utils.ReadInt(lines, "Order");

            exchangeRequests.Add(new ExchangeRequest
            {
                From = from,
                OrderId = orderId,
                Idle = true,
                Task = ExchangeTasks.Loading,
            });
        }
        /// <summary>
        /// Sec_C_3a - NAVEX avisa a WH que ha llegado para cargar el ID_PEDIDO en el connector y WH hace la carga
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

            exchange.TimerUnload?.ApplyAction("Start");
        }
        /// <summary>
        /// Sec_C_5 - BASEX registra petición de descarga (lista de descargas)
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

            exchangeRequests.Add(new ExchangeRequest
            {
                From = from,
                OrderId = orderId,
                Idle = true,
                Task = ExchangeTasks.Unloading,
            });
        }
        /// <summary>
        /// Sec_D_2c - BASEX pone el exchange en modo descargar
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

            exchange.TimerUnload?.ApplyAction("Start");
        }
        /// <summary>
        /// Sec_D_3 - BASEX registra que el pedido ID_PEDIDO ha sido descargado y lo elimina de la lista de descargas
        /// Request:  UNLOADED
        /// </summary>
        void CmdUnloaded(string[] lines)
        {
            string to = Utils.ReadString(lines, "To");
            if (to != baseId)
            {
                return;
            }

            int orderId = Utils.ReadInt(lines, "Order");

            //Eliminar la orden de descarga del pedido
            var req = exchangeRequests.FirstOrDefault(o => o.OrderId == orderId);
            if (req != null)
            {
                exchangeRequests.Remove(req);
            }

            //Eliminar el pedido de la lista
            var order = orders.FirstOrDefault(o => o.Id == orderId);
            if (order != null)
            {
                orders.Remove(order);
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

                //TODO: Si hay una petición de dock y no coincide con las naves conectadas, abortar y devolver la nave al parking, y ponerla en espera
            }
        }
        #endregion

        #region UTILITY
        void InitializeExchangeGroups()
        {
            //Busca todos los bloques que tengan en el nombre la regex de exchanges
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, i => i.CubeGrid == Me.CubeGrid && exchangesRegex.IsMatch(i.CustomName));

            //Group them by the group name
            var groups = blocks.GroupBy(b => ExtractGroupName(b.CustomName)).ToList();

            //Por cada grupo, inicializa los bloques de la clase ExchangeGroup
            foreach (var group in groups)
            {
                var exchangeGroup = new ExchangeGroup()
                {
                    Name = group.Key,
                };

                foreach (var block in group)
                {
                    var connector = block as IMyShipConnector;
                    if (connector != null)
                    {
                        if (connector.CustomName.Contains(exchangeUpperConnector)) exchangeGroup.UpperConnector = connector;
                        else if (connector.CustomName.Contains(exchangeLowerConnector)) exchangeGroup.LowerConnector = connector;

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
                        if (sorter.CustomName.Contains(exchangeSorterInput)) exchangeGroup.SorterInput = sorter;
                        else if (sorter.CustomName.Contains(exchangeSorterOutput)) exchangeGroup.SorterOutput = sorter;
                        continue;
                    }

                    var timer = block as IMyTimerBlock;
                    if (timer != null)
                    {
                        if (timer.CustomName.Contains(exchangeTimerPrepare)) exchangeGroup.TimerPrepare = timer;
                        else if (timer.CustomName.Contains(exchangeTimerUnload)) exchangeGroup.TimerUnload = timer;
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
            return ships.Where(s => s.ShipStatus == ShipStatus.WaitingForLoad || s.ShipStatus == ShipStatus.WaitingForUnload).ToList();
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
                if (ship.ShipStatus != ShipStatus.RouteToCustomer && ship.ShipStatus != ShipStatus.RouteToWarehouse) continue;

                string origin = ship.ShipStatus == ShipStatus.RouteToCustomer ? ship.Warehouse : ship.Customer;
                string destination = ship.ShipStatus == ShipStatus.RouteToCustomer ? ship.Customer : ship.Warehouse;
                sbData.AppendLine($"  - On route from [{origin}] to [{destination}]");

                Vector3D originPosition = ship.ShipStatus == ShipStatus.RouteToCustomer ? ship.WarehousePosition : ship.CustomerPosition;
                Vector3D destinationPosition = ship.ShipStatus == ShipStatus.RouteToCustomer ? ship.CustomerPosition : ship.WarehousePosition;
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
        void PrintReceptions()
        {
            if (!showExchangeRequests) return;

            sbData.AppendLine();
            sbData.AppendLine("RECEPTIONS STATUS");

            if (exchangeRequests.Count == 0)
            {
                sbData.AppendLine("No reception requests available.");
                return;
            }

            foreach (var unload in exchangeRequests)
            {
                string unloadStatus = unload.Idle ? "Pending" : "On route";
                sbData.AppendLine($"Order {unload.OrderId} from {unload.From}. {unloadStatus}");
            }
        }

        string ExtractGroupName(string input)
        {
            var match = exchangesRegex.Match(input);
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

            IGC.SendBroadcastMessage(channel, message);
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
            requestReception = Utils.ReadInt(storageLines, "RequestReception", 1) == 1;
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
            parts.Add($"RequestReception={(requestReception ? 1 : 0)}");

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
