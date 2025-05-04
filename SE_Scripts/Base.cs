using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace Base
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string baseId = "BaseWH1";
        const string baseCamera = "Camera";
        const string baseWarehouses = "Warehouse";
        const string baseParking = "-50554.19:-86466.82:-43745.25";
        const string baseDataLCDs = "[DELIVERY_DATA]";
        const string baseLogLCDs = "[DELIVERY_LOG]";
        const int NumWaypoints = 5;

        const string exchangeGroupName = @"GR_\w+";
        const string exchangeUpperConnector = "Input";
        const string exchangeLowerConnector = "Ouput";
        const string exchangeSorterInput = "Input";
        const string exchangeSorterOutput = "Output";
        const string exchangeTimerPrepare = "Prepare";
        const string exchangeTimerUnload = "Unload";

        const string warehouseId = "BaseWH1";
        readonly string fakeOrder = $"Command=REQUEST_ORDER|To={warehouseId}|Customer={baseId}|CustomerParking={baseParking}|Items=SteelPlate:10;";

        #region Helper classes
        class ExchangeGroup
        {
            public string Name;
            public string DockedShipName;
            public IMyShipConnector UpperConnector;
            public IMyShipConnector LowerConnector;
            public IMyCargoContainer Cargo;
            public IMyConveyorSorter SorterInput;
            public IMyConveyorSorter SorterOutput;
            public IMyTimerBlock TimerPrepare;
            public IMyTimerBlock TimerUnload;

            public bool IsValid()
            {
                return
                    UpperConnector != null &&
                    Cargo != null &&
                    SorterInput != null &&
                    SorterOutput != null;
            }
            public bool IsFree()
            {
                return
                    string.IsNullOrWhiteSpace(DockedShipName) &&
                    UpperConnector.Status == MyShipConnectorStatus.Unconnected &&
                    (LowerConnector?.Status ?? MyShipConnectorStatus.Unconnected) == MyShipConnectorStatus.Unconnected;
            }
            public List<Vector3D> CalculateRouteToConnector()
            {
                List<Vector3D> waypoints = new List<Vector3D>();

                Vector3D targetDock = UpperConnector.GetPosition();   // Punto final
                Vector3D forward = UpperConnector.WorldMatrix.Forward;
                Vector3D approachStart = targetDock + forward * 150;  // Punto de aproximación inicial

                for (int i = 0; i <= NumWaypoints; i++)
                {
                    double t = i / (double)NumWaypoints;
                    Vector3D point = Vector3D.Lerp(approachStart, targetDock, t) + (forward * 2.3);
                    waypoints.Add(point);
                }

                return waypoints;
            }
            public string GetApproachingWaypoints()
            {
                return string.Join(";", CalculateRouteToConnector().Select(VectorToStr));
            }
            public string MoveCargo(Order order, List<IMyCargoContainer> sourceCargos)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"## Exchange: {Name}. MoveCargo {order.Id}");

                sb.AppendLine($"## Destination Cargo: {Cargo.CustomName}. Warehouse cargos: {sourceCargos.Count}");

                //Cargo del exchange a donde tienen que ir los items del pedido
                var dstInv = Cargo.GetInventory();

                foreach (var orderItem in order.Items)
                {
                    var amountRemaining = orderItem.Value;
                    sb.AppendLine($"## Item {orderItem.Key}: {orderItem.Value}.");

                    // Buscar ítem en los contenedores de la base
                    foreach (var srcCargo in sourceCargos)
                    {
                        var srcInv = srcCargo.GetInventory();
                        var srcItems = new List<MyInventoryItem>();
                        srcInv.GetItems(srcItems);

                        if (!srcInv.IsConnectedTo(dstInv))
                        {
                            sb.AppendLine($"## Los cargos {srcCargo.CustomName} y {Cargo.CustomName} no están conectados.");
                            break;
                        }

                        for (int o = srcItems.Count - 1; o >= 0 && amountRemaining > 0; o--)
                        {
                            if (srcItems[o].Type.SubtypeId != orderItem.Key)
                            {
                                sb.AppendLine($"## WARNING Item {srcItems[o].Type.SubtypeId} discarded.");
                                continue;
                            }

                            var transferAmount = MyFixedPoint.Min(amountRemaining, srcItems[o].Amount);
                            sb.AppendLine($"## Moving {transferAmount}/{srcItems[o].Amount:F0} of {orderItem.Key} from {srcCargo.CustomName} to {Cargo.CustomName}.");

                            if (srcInv.TransferItemTo(dstInv, srcItems[o], transferAmount))
                            {
                                sb.AppendLine($"## Moved {transferAmount} of {orderItem.Key} from {srcCargo.CustomName}.");
                                amountRemaining -= transferAmount.ToIntSafe();
                                break;
                            }
                            else
                            {
                                sb.AppendLine($"## ERROR Cannot move {transferAmount} of {orderItem.Key} from {srcCargo.CustomName} to {Cargo.CustomName}.");
                            }
                        }
                    }

                    sb.AppendLine($"## Remaining {amountRemaining} of {orderItem.Key}.");
                }

                return sb.ToString();
            }
        }
        class Order
        {
            static int lastId = 0;

            public readonly int Id;
            public string Customer;
            public Vector3D CustomerParking;
            public string Warehouse;
            public Vector3D WarehouseParking;
            public Dictionary<string, int> Items = new Dictionary<string, int>();
            public string AssignedShip;

            public Order()
            {
                Id = ++lastId;
            }
        }
        enum ShipStatus
        {
            Unknown,
            Idle,

            ApproachingWarehouse,
            Loading,
            RouteToCustomer,

            WaitingForUnload,

            ApproachingCustomer,
            Unloading,
            RouteToWarehouse,
        }
        class Ship
        {
            public string Name;
            public ShipStatus ShipStatus;
            public Vector3D Position;
            public string Origin;
            public Vector3D OriginPosition;
            public string Destination;
            public Vector3D DestinationPosition;
            public DateTime UpdateTime;
        }
        class UnloadRequest
        {
            public string From;
            public int OrderId;
            public bool Idle;
        }
        class ShipExchangePair
        {
            public Ship Ship;
            public ExchangeGroup Exchange;
            public double Distance;
        }
        #endregion

        readonly IMyCameraBlock camera;
        readonly List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
        readonly IMyBroadcastListener bl;
        readonly List<IMyTextPanel> dataLCDs = new List<IMyTextPanel>();
        readonly StringBuilder sbData = new StringBuilder();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly StringBuilder sbLog = new StringBuilder();

        readonly System.Text.RegularExpressions.Regex exchangesRegex = new System.Text.RegularExpressions.Regex(exchangeGroupName);
        readonly List<ExchangeGroup> exchanges = new List<ExchangeGroup>();
        readonly List<Order> orders = new List<Order>();
        readonly List<Ship> ships = new List<Ship>();
        readonly List<UnloadRequest> unloadRequests = new List<UnloadRequest>();

        bool showExchanges = true;
        bool showShips = true;
        bool showOrders = true;
        bool showReceptions = true;
        bool enableLogs = false;

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
        static string ReadArgument(string[] lines, string command)
        {
            string cmdToken = $"{command}=";
            return lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }
        static string VectorToStr(Vector3D v)
        {
            return $"{v.X}:{v.Y}:{v.Z}";
        }
        static Vector3D StrToVector(string str)
        {
            string[] coords = str.Split(':');
            if (coords.Length == 3)
            {
                return new Vector3D(double.Parse(coords[0]), double.Parse(coords[1]), double.Parse(coords[2]));
            }
            return new Vector3D();
        }
        static ShipStatus StrToShipStatus(string str)
        {
            if (str == "Idle") return ShipStatus.Idle;
            if (str == "ApproachingWarehouse") return ShipStatus.ApproachingWarehouse;
            if (str == "Loading") return ShipStatus.Loading;
            if (str == "RouteToCustomer") return ShipStatus.RouteToCustomer;
            if (str == "WaitingForUnload") return ShipStatus.WaitingForUnload;
            if (str == "ApproachingCustomer") return ShipStatus.ApproachingCustomer;
            if (str == "Unloading") return ShipStatus.Unloading;
            if (str == "RouteToWarehouse") return ShipStatus.RouteToWarehouse;
            return ShipStatus.Unknown;
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
        void SendIGCMessage(string message)
        {
            WriteLogLCDs($"SendIGCMessage: {message}");

            IGC.SendBroadcastMessage(channel, message);
        }

        public Program()
        {
            InitializeExchangeGroups();

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
            Echo($"Working. Listening in channel: {channel}");

            Runtime.UpdateFrequency = UpdateFrequency.Update100; // Ejecuta cada ~1.6s
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                ParseTerminalMessage(argument);
            }

            while (bl.HasPendingMessage)
            {
                var message = bl.AcceptMessage();
                ParseMessage(message.Data.ToString());
            }

            if (showShips || showOrders)
            {
                sbData.Clear();
                sbData.AppendLine($"Listening in channel: {channel}");
                PrintExchanges();
                PrintShipStatus();
                PrintOrders();
                PrintReceptions();
                WriteDataLCDs(sbData.ToString(), false);
            }
        }

        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "REQUEST_STATUS") RequestStatus();
            else if (argument == "REQUEST_DELIVERY") RequestDelivery();
            else if (argument == "REQUEST_RECEPTION") RequestReception();
            else if (argument == "LIST_EXCHANGES") ListExchanges();
            else if (argument == "LIST_SHIPS") ListShips();
            else if (argument == "LIST_ORDERS") ListOrders();
            else if (argument == "LIST_RECEPTIONS") ListReceptions();
            else if (argument == "ENABLE_LOGS") EnableLogs();
            else if (argument == "FAKE_ORDER") FakeOrder();
            else if (argument.StartsWith("SHIP_LOADED")) ShipLoaded(argument);
        }
        /// <summary>
        /// Sec_A_1 - WH pide situación a todas las naves
        /// Execute:  REQUEST_STATUS
        /// </summary>
        void RequestStatus()
        {
            string message = $"Command=REQUEST_STATUS|From={baseId}";
            SendIGCMessage(message);
        }
        /// <summary>
        /// Sec_C_1 - WH revisa los pedidos. Para cada pedido, busca una nave libre y le da la orden de carga en un conector para NAVEX
        /// Execute:  LOAD_ORDER
        /// </summary>
        void RequestDelivery()
        {
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
            var freeShips = GetFreeShips(ShipStatus.Idle);
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
            exchange.DockedShipName = ship.Name;

            string forward = VectorToStr(camera.WorldMatrix.Forward);
            string up = VectorToStr(camera.WorldMatrix.Up);
            string waypoints = exchange.GetApproachingWaypoints();
            string message = $"Command=LOAD_ORDER|To={ship.Name}|Warehouse={baseId}|WarehouseParking={baseParking}|Customer={order.Customer}|CustomerParking={VectorToStr(order.CustomerParking)}|Order={order.Id}|Forward={forward}|Up={up}|WayPoints={waypoints}|Exchange={exchange.Name}";
            SendIGCMessage(message);

            //Pone el exchange en modo preparar pedido
            exchange.TimerPrepare?.ApplyAction("TriggerNow");
        }
        /// <summary>
        /// Sec_D_1 - BASEX revisa las peticiones de descarga. Busca conectores libres y da la orden de descarga a NAVEX en el conector especificado
        /// Execute:  UNLOAD_ORDER
        /// </summary>
        void RequestReception()
        {
            var unloads = GetPendingUnloadRequests();
            if (unloads.Count == 0)
            {
                return;
            }
            var freeExchanges = GetFreeExchanges();
            if (freeExchanges.Count == 0)
            {
                return;
            }
            var waitingShips = GetFreeShips(ShipStatus.WaitingForUnload);
            if (waitingShips.Count == 0)
            {
                RequestStatus();
                return;
            }
            WriteLogLCDs($"Receptions: {unloads.Count}; Free exchanges: {freeExchanges.Count}; Free ships: {waitingShips.Count}");

            var shipExchangePairs = GetNearestShipsFromExchanges(waitingShips, freeExchanges);
            foreach (var request in unloads)
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
                exchange.DockedShipName = ship.Name;

                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = exchange.GetApproachingWaypoints();
                string message = $"Command=UNLOAD_ORDER|To={request.From}|From={baseId}|Forward={forward}|Up={up}|WayPoints={waypoints}|Exchange={exchange.Name}";
                SendIGCMessage(message);

                break;
            }
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
            showReceptions = !showReceptions;
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de los logs
        /// </summary>
        void EnableLogs()
        {
            enableLogs = !enableLogs;
        }
        /// <summary>
        /// Sec_B_1 - BASEX revisa el inventario y pide a WH
        /// Execute:  REQUEST_ORDER
        /// </summary>
        void FakeOrder()
        {
            SendIGCMessage(fakeOrder);
        }
        /// <summary>
        /// Sec_C_3b - WH termina la carga y avisa a NAVEX
        /// Request:  SHIP_LOADED
        /// Execute:  LOADED
        /// </summary>
        void ShipLoaded(string argument)
        {
            string[] lines = argument.Split('|');

            string exchangeName = ReadArgument(lines, "Exchange");
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            if (exchange == null)
            {
                return;
            }

            string message = $"Command=LOADED|To={exchange.DockedShipName}|From={baseId}";
            SendIGCMessage(message);

            exchange.DockedShipName = null;
        }

        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            string[] lines = signal.Split('|');
            string command = ReadArgument(lines, "Command");

            if (command == "RESPONSE_STATUS") CmdResponseStatus(lines);
            else if (command == "REQUEST_ORDER") CmdRequestOrder(lines);
            else if (command == "LOADING") CmdLoading(lines);
            else if (command == "REQUEST_UNLOAD") CmdRequestUnload(lines);
            else if (command == "UNLOADING") CmdUnloading(lines);
            else if (command == "UNLOADED") CmdUnloaded(lines);
            else if (command == "ORDER_RECEIVED") CmdOrderReceived(lines);
        }
        /// <summary>
        /// Sec_A_3 - WH actualiza el estado de la nave
        /// Request:  RESPONSE_STATUS
        /// </summary>
        void CmdResponseStatus(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = ReadArgument(lines, "From");
            ShipStatus status = StrToShipStatus(ReadArgument(lines, "Status"));
            string origin = ReadArgument(lines, "Origin");
            Vector3D originPosition = StrToVector(ReadArgument(lines, "OriginPosition"));
            string destination = ReadArgument(lines, "Destination");
            Vector3D destinationPosition = StrToVector(ReadArgument(lines, "DestinationPosition"));
            Vector3D position = StrToVector(ReadArgument(lines, "Position"));

            var ship = ships.Find(s => s.Name == from);
            if (ship == null)
            {
                ship = new Ship() { Name = from };
                ships.Add(ship);
            }

            ship.ShipStatus = status;
            ship.Origin = origin;
            ship.OriginPosition = originPosition;
            ship.Destination = destination;
            ship.DestinationPosition = destinationPosition;
            ship.Position = position;
            ship.UpdateTime = DateTime.Now;
        }
        /// <summary>
        /// Sec_B_2 - WH registra el pedido (lista de pedidos)
        /// Request:  REQUEST_ORDER
        /// </summary>
        void CmdRequestOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string customer = ReadArgument(lines, "Customer");
            Vector3D customerParking = StrToVector(ReadArgument(lines, "CustomerParking"));
            string items = ReadArgument(lines, "Items");

            Order order = new Order
            {
                Warehouse = baseId,
                WarehouseParking = StrToVector(baseParking),
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
        /// Sec_C_3a - NAVEX avisa a WH que ha llegado para cargar el ID_PEDIDO en el connector y WH hace la carga
        /// Request:  LOADING
        /// </summary>
        void CmdLoading(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = ReadArgument(lines, "From");
            int orderId = int.Parse(ReadArgument(lines, "Order"));
            string exchangeName = ReadArgument(lines, "Exchange");

            var order = orders.FirstOrDefault(o => o.Id == orderId);
            var exchange = exchanges.Find(e => e.Name == exchangeName);

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
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = ReadArgument(lines, "From");
            int orderId = int.Parse(ReadArgument(lines, "Order"));

            unloadRequests.Add(new UnloadRequest
            {
                From = from,
                OrderId = orderId,
                Idle = true,
            });
        }
        /// <summary>
        /// Sec_D_2c - BASEX pone el exchange en modo descargar
        /// </summary>
        void CmdUnloading(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string exchangeName = ReadArgument(lines, "Exchange");
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            exchange.TimerUnload?.ApplyAction("Start");
        }
        /// <summary>
        /// Sec_D_3 - BASEX registra que el pedido ID_PEDIDO ha sido descargado y lo elimina de la lista de descargas. Lanza [ORDER_RECEIVED] a WH
        /// Request:  UNLOADED
        /// Execute:  ORDER_RECEIVED
        /// </summary>
        void CmdUnloaded(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            //Eliminar la orden de descarga del pedido
            int orderId = int.Parse(ReadArgument(lines, "Order"));
            var req = unloadRequests.FirstOrDefault(o => o.OrderId == orderId);
            if (req != null)
            {
                unloadRequests.Remove(req);
            }

            //Libera el exchange
            var exchange = exchanges.Find(e => e.DockedShipName == req.From);
            if (exchange != null)
            {
                exchange.DockedShipName = null;
            }

            //Enviar al WH el mensaje de que se ha recibido el pedido
            string message = $"Command=ORDER_RECEIVED|To={warehouseId}|From={baseId}|Order={orderId}";
            SendIGCMessage(message);
        }
        /// <summary>
        /// Sec_D_4 - WH registra que el pedido ID_PEDIDO ha sido descargado y lo elimina de la lista de pedidos
        /// </summary>
        void CmdOrderReceived(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            //Eliminar el pedido de la lista
            int orderId = int.Parse(ReadArgument(lines, "Order"));
            var order = orders.FirstOrDefault(o => o.Id == orderId);
            if (order != null)
            {
                orders.Remove(order);
            }
        }

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

        List<UnloadRequest> GetPendingUnloadRequests()
        {
            return unloadRequests.Where(r => r.Idle).ToList();
        }
        List<Order> GetPendingOrders()
        {
            return orders.Where(o => string.IsNullOrWhiteSpace(o.AssignedShip)).ToList();
        }
        List<Ship> GetFreeShips(ShipStatus status)
        {
            return ships.Where(s => s.ShipStatus == status).ToList();
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

            sbData.AppendLine("EXCHANGE STATUS");

            if (exchanges.Count == 0)
            {
                sbData.AppendLine("No exchanges available.");
                return;
            }

            foreach (var exchange in exchanges)
            {
                sbData.AppendLine($"Exchange: {exchange.Name} Docked ship: {exchange.DockedShipName ?? "Free"}");
                sbData.AppendLine($"Connectors: {exchange.UpperConnector?.Status} - {exchange.LowerConnector?.Status}");
            }
        }
        void PrintShipStatus()
        {
            if (!showShips) return;

            sbData.AppendLine("SHIPS STATUS");

            if (ships.Count == 0)
            {
                sbData.AppendLine("No ships available.");
                return;
            }

            foreach (var ship in ships)
            {
                sbData.AppendLine($"{ship.Name} Status: {ship.ShipStatus}.");
                sbData.AppendLine($"Last known position: {VectorToStr(ship.Position)}. Last update: {(DateTime.Now - ship.UpdateTime).TotalSeconds:F0}secs");
                if (string.IsNullOrEmpty(ship.Origin)) continue;

                double distanceToOrigin = Vector3D.Distance(ship.Position, ship.OriginPosition);
                double distanceToDestination = Vector3D.Distance(ship.Position, ship.DestinationPosition);
                sbData.AppendLine($"On route from [{ship.Origin}] to [{ship.Destination}]");
                sbData.AppendLine($"Distance from origin: {distanceToOrigin:F0}m.");
                sbData.AppendLine($"Distance to destination: {distanceToDestination:F0}m.");
            }
        }
        void PrintOrders()
        {
            if (!showOrders) return;

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
            if (!showReceptions) return;

            sbData.AppendLine("RECEPTIONS STATUS");

            if (unloadRequests.Count == 0)
            {
                sbData.AppendLine("No reception requests available.");
                return;
            }

            foreach (var unload in unloadRequests)
            {
                string unloadStatus = unload.Idle ? "Pending" : "On route";
                sbData.AppendLine($"Order {unload.OrderId} from {unload.From}. {unloadStatus}");
            }
        }
    }
}
