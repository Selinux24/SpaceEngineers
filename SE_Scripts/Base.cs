using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace Base
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string baseId = "BaseBETA1";
        const string baseConnector = "Delivery Connector";
        const string baseCamera = "Camera";
        const string baseParking = "-52394.26:-83868.35:-44561.14";
        const string baseDataLCDs = "[DELIVERY_DATA]";
        const int NumWaypoints = 5;

        class Order
        {
            static int lastId = 0;

            public readonly int Id;
            public string From;
            public Vector3D FromParking;
            public string To;
            public Vector3D ToParking;
            public Dictionary<string, int> Items = new Dictionary<string, int>();

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
            public string Origin;
            public string Destination;
            public Vector3D Position;
        }
        class UnloadRequest
        {
            public string From;
            public int OrderId;
        }

        IMyCameraBlock camera;
        IMyBroadcastListener bl;
        Dictionary<string, IMyShipConnector> upperConnectors = new Dictionary<string, IMyShipConnector>();
        Dictionary<string, IMyShipConnector> lowerConnectors = new Dictionary<string, IMyShipConnector>();
        List<IMyTextPanel> dataLcds = new List<IMyTextPanel>();

        List<Order> orders = new List<Order>();
        List<Ship> ships = new List<Ship>();
        List<UnloadRequest> unloadRequests = new List<UnloadRequest>();
        bool showOrders = false;
        bool showShips = false;

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

        public Program()
        {
            InitializeConnectors();

            camera = GridTerminalSystem.GetBlockWithName(baseCamera) as IMyCameraBlock;
            if (camera == null)
            {
                Echo("Cámara no encontrada.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(dataLcds, lcd => lcd.CustomName.Contains(baseDataLCDs));

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
        void WriteLCDs(string wildcard, string text)
        {
            List<IMyTextPanel> lcds = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(lcds, lcd => lcd.CustomName.Contains(wildcard));
            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, false);
            }
        }
        void WriteDataLCDs(string text, bool append)
        {
            foreach (var lcd in dataLcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text, append);
            }
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
        }

        void ParseTerminalMessage(string argument)
        {
            if (argument == "REQUEST_STATUS")
            {
                CmdRequestStatus();
            }
            else if (argument == "TRY_DELIVERY")
            {
                CmdTryDelivery();
            }
            else if (argument == "TRY_UNLOAD")
            {
                CmdTryUnload();
            }
            else if (argument == "LIST_SHIPS")
            {
                showShips = true;
                CmdListShips();
            }
            else if (argument == "LIST_ORDERS")
            {
                showOrders = true;
                CmdListOrders();
            }
            else if (argument == "FAKE_ORDER")
            {
                CmdFakeOrder();
            }
        }
        void CmdRequestStatus()
        {
            //[Command=REQUEST_STATUS|From=SENDER]
            ships.Clear();
            string message = $"Command=REQUEST_STATUS|From={baseId}";
            SendIGCMessage(message);
        }
        void CmdTryDelivery()
        {
            var pendantOrders = orders.ToList();
            if (pendantOrders.Count == 0)
            {
                return;
            }
            var freeShips = ships.Where(s => s.ShipStatus == ShipStatus.Idle).ToList();
            if (freeShips.Count == 0)
            {
                CmdRequestStatus();
                return;
            }
            var freeConnectors = GetFreeConnectors();

            int deliveryCount = System.Math.Min(freeConnectors.Count, System.Math.Min(freeShips.Count, pendantOrders.Count));
            for (int i = 0; i < deliveryCount; i++)
            {
                var order = pendantOrders[i];
                var ship = freeShips[i];
                var connector = freeConnectors[i];

                //[Command=LOAD_ORDER|To=Ship|From=Me|For=Customer|ForParking=CustomerParking|Order=ID_PEDIDO|Forward=x:y:z|Up=x:y:z|WayPoints=x:y:z;]
                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = string.Join(";", CalculateRouteToConnector(connector).Select(VectorToStr));
                string message = $"Command=LOAD_ORDER|To={ship.Name}|From={baseId}|For={order.To}|ForParking={VectorToStr(order.ToParking)}|Order={order.Id}|Forward={forward}|Up={up}|WayPoints={waypoints}";
                SendIGCMessage(message);
            }
        }
        void CmdTryUnload()
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

                //[Command=UNLOAD|To=Ship|From=Me|For=Customer|ForParking=CustomerParking|Order=ID_PEDIDO|Forward=x:y:z|Up=x:y:z|WayPoints=x:y:z;]
                string forward = VectorToStr(camera.WorldMatrix.Forward);
                string up = VectorToStr(camera.WorldMatrix.Up);
                string waypoints = string.Join(";", CalculateRouteToConnector(connector).Select(VectorToStr));
                string message = $"Command=UNLOAD_ORDER|To={request.From}|Forward={forward}|Up={up}|WayPoints={waypoints}";
                SendIGCMessage(message);
            }
        }
        void CmdListShips()
        {
            if (!showShips) return;

            if (ships.Count == 0)
            {
                WriteDataLCDs("No ships available.", false);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var ship in ships)
            {
                sb.AppendLine($"Name: {ship.Name}, Status: {ship.ShipStatus}, Origin: {ship.Origin}, Destination: {ship.Destination}, Position: {ship.Position}");
            }
            WriteDataLCDs(sb.ToString(), false);
        }
        void CmdListOrders()
        {
            if (!showOrders) return;

            if (orders.Count == 0)
            {
                WriteDataLCDs("No orders available.", false);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var order in orders)
            {
                sb.AppendLine($"OrderId: {order.Id}, From: {order.From}, To: {order.To}, Items: {string.Join(", ", order.Items.Select(i => $"{i.Key}: {i.Value}"))}");
            }
            WriteDataLCDs(sb.ToString(), false);
        }
        void CmdFakeOrder()
        {
            ParseMessage($"Command=ORDER|To={baseId}|From=NOBODY|Parking=0:0:0|Items=SteelPlate:10;");
        }

        void ParseMessage(string signal)
        {
            string[] lines = signal.Split('|');

            string command = ReadArgument(lines, "Command");
            if (command == "ORDER")
            {
                CmdOrder(lines);
            }
            else if (command == "STATUS")
            {
                CmdStatus(lines);
            }
            else if (command == "UNLOAD")
            {
                CmdUnload(lines);
            }
        }
        void CmdOrder(string[] lines)
        {
            //[Command=ORDER|To=Me|From=Sender|Parking=SenderParking|Items=ITEMS:AMOUNT;]
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

            CmdListOrders();
        }
        void CmdStatus(string[] lines)
        {
            //[Command=STATUS|To=Me|From=Sender|Status=Status|Origin=Base|Destination=Base|Position=x:y:z]
            string to = ReadArgument(lines, "To");
            if (to != baseId)
            {
                return;
            }

            string from = ReadArgument(lines, "From");
            ShipStatus status = StrToShipStatus(ReadArgument(lines, "Status"));
            string origin = ReadArgument(lines, "Origin");
            string destination = ReadArgument(lines, "Destination");
            Vector3D position = StrToVector(ReadArgument(lines, "Position"));

            ships.Add(new Ship
            {
                Name = from,
                ShipStatus = status,
                Origin = origin,
                Destination = destination,
                Position = position
            });

            CmdListShips();
        }
        void CmdUnload(string[] lines)
        {
            //[Command=UNLOAD|To=Me|From=Sender|Order=OrderId]
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

        void SendIGCMessage(string message)
        {
            IGC.SendBroadcastMessage(channel, message);
        }
    }
}
