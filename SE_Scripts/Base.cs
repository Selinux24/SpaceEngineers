using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI;
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
        const string exchangeTimerLoad = "Load";

        const string warehouseId = "BaseWH1";
        string fakeOrder = $"Command=REQUEST_ORDER|To={warehouseId}|Customer={baseId}|CustomerParking={baseParking}|Items=SteelPlate:10;";

        #region Helper classes
        class ExchangeGroup
        {
            public string Name;
            public IMyShipConnector UpperConnector;
            public IMyShipConnector LowerConnector;
            public IMyCargoContainer Cargo;
            public IMyConveyorSorter SorterInput;
            public IMyConveyorSorter SorterOutput;
            public IMyTimerBlock TimerPrepare;
            public IMyTimerBlock TimerUnload;
            public IMyTimerBlock TimerLoad;

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
            public List<Vector3D> CalculateRouteFromConnector()
            {
                var wp = CalculateRouteToConnector();
                wp.Reverse();
                return wp;
            }
            public void MoveCargo(Order order, List<IMyCargoContainer> cargos)
            {
                //Localiza el cargo más cercano al conector
                //Mueve la carga desde los warehouses a los exchanges
                var shipInv = Cargo.GetInventory();

                foreach (var item in order.Items)
                {
                    var amountRemaining = item.Value;

                    // Buscar ítem en los contenedores de la base
                    foreach (var cargo in cargos)
                    {
                        var inv = cargo.GetInventory();
                        var items = new List<MyInventoryItem>();
                        inv.GetItems(items);

                        for (int o = items.Count - 1; o >= 0 && amountRemaining > 0; o--)
                        {
                            if (items[o].Type.SubtypeId == item.Key)
                            {
                                var transferAmount = MyFixedPoint.Min(amountRemaining, items[o].Amount).ToIntSafe();
                                if (inv.TransferItemTo(shipInv, o, null, true, transferAmount))
                                {
                                    amountRemaining -= transferAmount;
                                    break;
                                }
                            }
                        }
                    }
                }
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
            Busy,
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

        bool showShips = true;
        bool showOrders = true;
        bool showReceptions = true;

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
            if (str == "Busy") return ShipStatus.Busy;
            return ShipStatus.Idle;
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

            var forward = camera.WorldMatrix.Forward;
            var up = camera.WorldMatrix.Up;
            Echo($"{forward.X:F2},{forward.Y:F2},{forward.Z:F2}");
            Echo($"{up.X:F2},{up.Y:F2},{up.Z:F2}");

            bl = IGC.RegisterBroadcastListener(channel);
            Runtime.UpdateFrequency = UpdateFrequency.Update100; // Ejecuta cada ~1.6s
            Echo($"Listening in channel: {channel}");

            WriteDataLCDs($"Listening in channel: {channel}", false);
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
            else if (argument == "LIST_SHIPS") ListShips();
            else if (argument == "LIST_ORDERS") ListOrders();
            else if (argument == "LIST_RECEPTIONS") ListReceptions();
            else if (argument == "FAKE_ORDER") FakeOrder();
        }
        void RequestStatus()
        {
            string message = $"Command=REQUEST_STATUS|From={baseId}";
            SendIGCMessage(message);
        }
        void RequestDelivery()
        {
            var pendantOrders = orders.ToList();
            if (pendantOrders.Count == 0)
            {
                return;
            }
            var freeShips = ships.Where(s => s.ShipStatus == ShipStatus.Idle).ToList();
            if (freeShips.Count == 0)
            {
                RequestStatus();
                return;
            }
            var freeExchanges = GetFreeExchanges();
            WriteLogLCDs($"Deliveries: {pendantOrders.Count}; Free ships: {freeShips.Count}; Free exchanges: {freeExchanges.Count}");

            int deliveryCount = Math.Min(freeExchanges.Count, Math.Min(freeShips.Count, pendantOrders.Count));
            for (int i = 0; i < deliveryCount; i++)
            {
                var order = pendantOrders[i];
                var ship = freeShips[i];
                var exchange = freeExchanges[i];

                order.AssignedShip = ship.Name;

                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = string.Join(";", exchange.CalculateRouteToConnector().Select(VectorToStr));
                string message = $"Command=LOAD_ORDER|To={ship.Name}|Warehouse={baseId}|WarehouseParking={baseParking}|Customer={order.Customer}|CustomerParking={VectorToStr(order.CustomerParking)}|Order={order.Id}|Forward={forward}|Up={up}|WayPoints={waypoints}|Exchange={exchange.Name}";
                SendIGCMessage(message);

                exchange.MoveCargo(order, cargos);
            }
        }
        void RequestReception()
        {
            var freeExchanges = GetFreeExchanges();
            if (freeExchanges.Count == 0)
            {
                return;
            }
            var unloads = unloadRequests.Where(r => r.Idle).ToList();
            if (unloads.Count == 0)
            {
                return;
            }
            WriteLogLCDs($"Receptions: {unloads.Count}; Free exchanges: {freeExchanges.Count}");

            int unloadCount = Math.Min(freeExchanges.Count, unloads.Count);
            for (int i = 0; i < unloadCount; i++)
            {
                var request = unloads[i];
                var exchange = freeExchanges[i];

                request.Idle = false;

                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = string.Join(";", exchange.CalculateRouteToConnector().Select(VectorToStr));
                string message = $"Command=UNLOAD_ORDER|To={request.From}|From={baseId}|Forward={forward}|Up={up}|WayPoints={waypoints}|Exchange={exchange.Name}";
                SendIGCMessage(message);
            }
        }
        void ListShips()
        {
            showShips = !showShips;
        }
        void ListOrders()
        {
            showOrders = !showOrders;
        }
        void ListReceptions()
        {
            showReceptions = !showReceptions;
        }
        void FakeOrder()
        {
            SendIGCMessage(fakeOrder);
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
            else if (command == "WAITING") CmdWaiting(lines);
        }
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

            var exchange = exchanges.Find(e => e.Name == exchangeName);
            exchange?.TimerLoad?.ApplyAction("Start");

            string forward = VectorToStr(camera.WorldMatrix.Forward);
            string up = VectorToStr(camera.WorldMatrix.Up);
            string waypoints = string.Join(";", exchange.CalculateRouteFromConnector().Select(VectorToStr));
            string message = $"Command=LOADED|To={from}|From={baseId}|Forward={forward}|Up={up}|WayPoints={waypoints}";
            SendIGCMessage(message);
        }
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
        void CmdUnloading(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string exchangeName = ReadArgument(lines, "Exchange");
            var exchange = exchanges.Find(e => e.Name == exchangeName);
            exchange?.TimerUnload?.ApplyAction("Start");
        }
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

            //Enviar al WH el mensaje de que se ha recibido el pedido
            string message = $"Command=ORDER_RECEIVED|To={warehouseId}|From={baseId}|Order={orderId}";
            SendIGCMessage(message);
        }
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
        void CmdWaiting(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            //TODO: Nada que hacer...
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
                    IMyShipConnector connector = block as IMyShipConnector;
                    if (connector != null)
                    {
                        if (connector.CustomName.Contains(exchangeUpperConnector)) exchangeGroup.UpperConnector = connector;
                        else if (connector.CustomName.Contains(exchangeLowerConnector)) exchangeGroup.LowerConnector = connector;

                        continue;
                    }

                    IMyCargoContainer cargo = block as IMyCargoContainer;
                    if (cargo != null)
                    {
                        exchangeGroup.Cargo = cargo;
                        continue;
                    }

                    IMyConveyorSorter sorter = block as IMyConveyorSorter;
                    if (sorter != null)
                    {
                        if (sorter.CustomName.Contains(exchangeSorterInput)) exchangeGroup.SorterInput = sorter;
                        else if (sorter.CustomName.Contains(exchangeSorterOutput)) exchangeGroup.SorterOutput = sorter;
                        continue;
                    }

                    IMyTimerBlock timer = block as IMyTimerBlock;
                    if (timer != null)
                    {
                        if (timer.CustomName.Contains(exchangeTimerPrepare)) exchangeGroup.TimerPrepare = timer;
                        else if (timer.CustomName.Contains(exchangeTimerUnload)) exchangeGroup.TimerUnload = timer;
                        else if (timer.CustomName.Contains(exchangeTimerLoad)) exchangeGroup.TimerLoad = timer;
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
        List<ExchangeGroup> GetFreeExchanges()
        {
            return exchanges.Where(e => e.IsFree()).ToList();
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
                sbData.AppendLine($"{ship.Name} Status: {ship.ShipStatus}. Last update: {(DateTime.Now - ship.UpdateTime).TotalSeconds:F0} seconds");
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
