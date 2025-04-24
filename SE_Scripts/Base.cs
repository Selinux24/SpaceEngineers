using Sandbox.ModAPI.Ingame;
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
        const string baseId = "BaseBETA1";
        const string baseConnector = "Delivery Connector";
        const string baseCamera = "Camera";
        const string baseWarehouses = "Warehouse";
        const string baseExchanges = "Exchange";
        const string baseParking = "-50554.19:-86466.82:-43745.25";
        const string baseDataLCDs = "[DELIVERY_DATA]";
        const string baseLogLCDs = "[DELIVERY_LOG]";
        const int NumWaypoints = 5;

        #region Helper classes
        class Order
        {
            static int lastId = 0;

            public readonly int Id;
            public string From;
            public Vector3D FromParking;
            public string To;
            public Vector3D ToParking;
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
        }
        #endregion

        readonly IMyCameraBlock camera;
        readonly List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
        readonly List<IMyCargoContainer> exchanges = new List<IMyCargoContainer>();
        readonly IMyBroadcastListener bl;
        readonly Dictionary<string, IMyShipConnector> upperConnectors = new Dictionary<string, IMyShipConnector>();
        readonly Dictionary<string, IMyShipConnector> lowerConnectors = new Dictionary<string, IMyShipConnector>();
        readonly List<IMyTextPanel> dataLCDs = new List<IMyTextPanel>();
        readonly StringBuilder sbData = new StringBuilder();
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly StringBuilder sbLog = new StringBuilder();

        readonly List<Order> orders = new List<Order>();
        readonly List<Ship> ships = new List<Ship>();
        readonly List<UnloadRequest> unloadRequests = new List<UnloadRequest>();

        bool showOrders = true;
        bool showShips = true;

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
            InitializeConnectors();

            camera = GetBlockWithName<IMyCameraBlock>(baseCamera);
            if (camera == null)
            {
                Echo("Cámara no encontrada.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(cargos, cargo => cargo.CubeGrid == Me.CubeGrid && cargo.CustomName.Contains(baseWarehouses));
            GridTerminalSystem.GetBlocksOfType(exchanges, cargo => cargo.CubeGrid == Me.CubeGrid && cargo.CustomName.Contains(baseExchanges));
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
            var freeConnectors = GetFreeConnectors();

            int deliveryCount = System.Math.Min(freeConnectors.Count, System.Math.Min(freeShips.Count, pendantOrders.Count));
            for (int i = 0; i < deliveryCount; i++)
            {
                var order = pendantOrders[i];
                var ship = freeShips[i];
                var connector = freeConnectors[i];

                order.AssignedShip = ship.Name;

                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = string.Join(";", CalculateRouteToConnector(connector).Select(VectorToStr));
                string message = $"Command=LOAD_ORDER|To={ship.Name}|From={baseId}|For={order.To}|ForParking={VectorToStr(order.ToParking)}|Order={order.Id}|Forward={forward}|Up={up}|WayPoints={waypoints}";
                SendIGCMessage(message);

                MoveCargo(connector, order);
            }
        }
        void RequestReception()
        {
            var freeConnectors = GetFreeConnectors();
            if (freeConnectors.Count == 0)
            {
                return;
            }
            var unloadRequests = this.unloadRequests.ToList();
            if (unloadRequests.Count == 0)
            {
                return;
            }

            int unloadCount = System.Math.Min(freeConnectors.Count, unloadRequests.Count);
            for (int i = 0; i < unloadCount; i++)
            {
                var request = unloadRequests[i];
                var connector = freeConnectors[i];

                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = string.Join(";", CalculateRouteToConnector(connector).Select(VectorToStr));
                string message = $"Command=UNLOAD_ORDER|To={request.From}|From={baseId}|Forward={forward}|Up={up}|WayPoints={waypoints}";
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
        void FakeOrder()
        {
            ParseMessage($"Command=REQUEST_ORDER|To={baseId}|From=NOBODY|Parking=0:0:0|Items=SteelPlate:10;");
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
            //[Command=RESPONSE_STATUS|To=Me|From=Sender|Status=Status|Origin=Base|OriginPosition=Position|Destination=Base|DestinationPosition=Position|Position=x:y:z]
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
            if (ship != null)
            {
                ship.ShipStatus = status;
                ship.Origin = origin;
                ship.OriginPosition = originPosition;
                ship.Destination = destination;
                ship.DestinationPosition = destinationPosition;
                ship.Position = position;
                ship.UpdateTime = DateTime.Now;
            }
            else
            {
                ships.Add(new Ship
                {
                    Name = from,
                    ShipStatus = status,
                    Position = position,
                    Origin = origin,
                    OriginPosition = originPosition,
                    Destination = destination,
                    DestinationPosition = destinationPosition,
                    UpdateTime = DateTime.Now
                });
            }
        }
        void CmdRequestOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = ReadArgument(lines, "From");
            Vector3D fromParking = StrToVector(ReadArgument(lines, "Parking"));
            string items = ReadArgument(lines, "Items");

            Order order = new Order();
            order.From = from;
            order.FromParking = fromParking;
            order.To = to;
            order.ToParking = StrToVector(baseParking);
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

            //TODO: Carga de items

            string message = $"Command=LOADED|To={from}|From={baseId}";
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
                OrderId = orderId
            });
        }
        void CmdUnloading(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            //TODO: detectar cuando se ha terminado la descarga, para lanzar el comando UNLOADED
        }
        void CmdUnloaded(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            int orderId = int.Parse(ReadArgument(lines, "Order"));
            //Eliminar la orden de descarga del pedido
            var order = orders.FirstOrDefault(o => o.Id == orderId);
            if (order != null)
            {
                orders.Remove(order);
            }

            //Enviar al WH el mensaje de que se ha recibido el pedido
            string message = $"Command=ORDER_RECEIVED|To={order.From}|From={baseId}|Order={orderId}";
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

        void InitializeConnectors()
        {
            List<IMyShipConnector> connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(connectors, c => c.CustomName.Contains(baseConnector));
            if (connectors.Count == 0)
            {
                Echo("Conectores no encontrados.");
                return;
            }

            foreach (var connector in connectors)
            {
                if (!connector.CustomName.Contains("1") && !connector.CustomName.Contains("2")) continue;

                string baseName = connector.CustomName.Substring(0, connector.CustomName.Length - 1);
                string suffix = connector.CustomName.Substring(connector.CustomName.Length - 1);

                if (suffix == "1") upperConnectors[baseName] = connector;
                if (suffix == "2") lowerConnectors[baseName] = connector;
            }
        }
        List<IMyShipConnector> GetFreeConnectors()
        {
            List<IMyShipConnector> freeConnectors = new List<IMyShipConnector>();
            foreach (var pair in upperConnectors.Keys)
            {
                if (!lowerConnectors.ContainsKey(pair)) continue;

                IMyShipConnector con1 = upperConnectors[pair];
                IMyShipConnector con2 = lowerConnectors[pair];
                if (con1.Status == MyShipConnectorStatus.Unconnected && con2.Status == MyShipConnectorStatus.Unconnected)
                {
                    freeConnectors.Add(con1);
                }
            }
            return freeConnectors;
        }
        List<Vector3D> CalculateRouteToConnector(IMyShipConnector con1)
        {
            List<Vector3D> waypoints = new List<Vector3D>();

            Vector3D targetDock = con1.GetPosition();   // Punto final
            Vector3D forward = con1.WorldMatrix.Forward;
            Vector3D approachStart = targetDock + forward * 150;  // Punto de aproximación inicial

            for (int i = 0; i <= NumWaypoints; i++)
            {
                double t = i / (double)NumWaypoints;
                Vector3D point = Vector3D.Lerp(approachStart, targetDock, t) + (forward * 2.3);
                Echo($"GPS:wp_{i}:{point.X:F2}:{point.Y:F2}:{point.Z:F2}:#FFFF00FF");
                waypoints.Add(point);
            }

            return waypoints;
        }
        IMyCargoContainer GetClossetCargoContainer(IMyShipConnector con1)
        {
            IMyCargoContainer closest = null;
            double closestDistance = double.MaxValue;

            foreach (var container in exchanges)
            {
                // Verifica que esté conectado por conveyor
                if (container.GetInventory().IsConnectedTo(con1.GetInventory()))
                {
                    double distance = Vector3D.Distance(con1.GetPosition(), container.GetPosition());
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = container;
                    }
                }
            }

            return closest;
        }
        void MoveCargo(IMyShipConnector con1, Order order)
        {
            //Localiza el cargo más cercano al conector
            IMyCargoContainer closest = GetClossetCargoContainer(con1);
            if (closest != null)
            {
                //Mueve la carga desde los warehouses a los exchanges
                var shipInv = closest.GetInventory();

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
                sbData.AppendLine($"Id[{order.Id}]. {order.AssignedShip} shipping from {order.From} to {order.To}");
            }
        }
    }
}
