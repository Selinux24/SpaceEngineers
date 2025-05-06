using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SE_Scripts.GD.Nave
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string shipId = "NaveBETA1";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const string shipArrivalPB = "HT Automaton Programmable Block Arrival";
        const string shipAlignPB = "HT Automaton Programmable Block Align";
        const string shipTimerLock = "HT Automaton Timer Block Locking";
        const string shipTimerUnlock = "HT Automaton Timer Block Unlocking";
        const string shipTimerLoad = "HT Automaton Timer Block Load";
        const string shipTimerUnload = "HT Automaton Timer Block Unload";
        const string shipTimerWaiting = "HT Automaton Timer Block Waiting";
        const string shipLogLCDs = "[DELIVERY_LOG]";

        #region Helper classes
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
        class TripData
        {
            public int OrderId;
            public string OrderWarehouse;
            public Vector3D OrderWarehouseParking;
            public string OrderCustomer;
            public Vector3D OrderCustomerParking;

            public string ExchangeName;
            public string ExchangeForward;
            public string ExchangeUp;
            public string ExchangeApproachingWaypoints;
            public string ExchangeDepartingWaypoints;

            public string AlignFwd;
            public string AlignUp;
            public string Waypoints;
            public string OnLastWaypoint;
            public string DestinationName;
            public Vector3D DestinationPosition;
            public string OnDestinationArrival;
        }
        #endregion

        readonly IMyBroadcastListener bl;
        readonly IMyRemoteControl remotePilot;
        readonly IMyTimerBlock timerPilot;
        readonly IMyProgrammableBlock arrivalPB;
        readonly IMyProgrammableBlock alignPB;
        readonly IMyTimerBlock timerLock;
        readonly IMyTimerBlock timerUnlock;
        readonly IMyTimerBlock timerLoad;
        readonly IMyTimerBlock timerUnload;
        readonly IMyTimerBlock timerWaiting;
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly StringBuilder sbLog = new StringBuilder();

        ShipStatus status = ShipStatus.Idle;

        TripData currentTrip = new TripData();

        bool enableLogs = false;

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
        static string ReverseString(string str)
        {
            string[] parts = str.Split(';');
            Array.Reverse(parts);
            return string.Join(";", parts);
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
        static List<Vector3D> ParseWaypoints(string data)
        {
            List<Vector3D> wp = new List<Vector3D>();

            string[] points = data.Split(';');
            for (int i = 0; i < points.Length; i++)
            {
                wp.Add(StrToVector(points[i]));
            }

            return wp;
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
        void Align(string forward, string up, string wayPoints, string command)
        {
            string message = $"{forward}|{up}|{wayPoints}¬{command}";
            WriteLogLCDs($"Align: {message}");
            alignPB.TryRun(message);
        }
        void Arrival(Vector3D position, string command)
        {
            string message = $"{VectorToStr(position)}¬{command}";
            WriteLogLCDs($"Arrival: {message}");
            arrivalPB.TryRun(message);
        }

        public Program()
        {
            arrivalPB = GetBlockWithName<IMyProgrammableBlock>(shipArrivalPB);
            if (arrivalPB == null)
            {
                Echo($"Programmable Block '{shipArrivalPB}' no localizado.");
                return;
            }

            alignPB = GetBlockWithName<IMyProgrammableBlock>(shipAlignPB);
            if (alignPB == null)
            {
                Echo($"Programmable Block '{shipAlignPB}' no localizado.");
                return;
            }

            remotePilot = GetBlockWithName<IMyRemoteControl>(shipRemoteControlPilot);
            if (remotePilot == null)
            {
                Echo($"Control remoto de pilotaje '{shipRemoteControlPilot}' no locallizado.");
                return;
            }

            timerLock = GetBlockWithName<IMyTimerBlock>(shipTimerLock);
            if (timerLock == null)
            {
                Echo($"Timer de atraque '{shipTimerLock}' no locallizado.");
                return;
            }

            timerUnlock = GetBlockWithName<IMyTimerBlock>(shipTimerUnlock);
            if (timerUnlock == null)
            {
                Echo($"Timer de separación '{shipTimerUnlock}' no locallizado.");
                return;
            }

            timerLoad = GetBlockWithName<IMyTimerBlock>(shipTimerLoad);
            if (timerLoad == null)
            {
                Echo($"Timer de carga '{shipTimerLoad}' no locallizado.");
                return;
            }

            timerUnload = GetBlockWithName<IMyTimerBlock>(shipTimerUnload);
            if (timerUnload == null)
            {
                Echo($"Timer de descarga '{shipTimerUnload}' no locallizado.");
                return;
            }

            timerWaiting = GetBlockWithName<IMyTimerBlock>(shipTimerWaiting);
            if (timerWaiting == null)
            {
                Echo($"Timer de espera '{shipTimerWaiting}' no locallizado.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(logLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(shipLogLCDs));

            WriteLCDs("[shipId]", shipId);

            bl = IGC.RegisterBroadcastListener(channel);
            Runtime.UpdateFrequency = UpdateFrequency.Update100; // Ejecuta cada ~1.6s
            Echo($"Listening in channel {channel}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo($"Estado: {status}");
            if (orderId > 0)
            {
                Echo($"Pedido: {orderId}");
                Echo($"Carga: {orderWarehouse} -> {VectorToStr(orderWarehouseParking)}");
                Echo($"Descarga: {orderCustomer} -> {VectorToStr(orderCustomerParking)}");
            }

            if (!string.IsNullOrEmpty(argument))
            {
                ParseTerminalMessage(argument);
                return;
            }

            while (bl.HasPendingMessage)
            {
                var message = bl.AcceptMessage();
                ParseMessage(message.Data.ToString());
            }
        }

        void ParseTerminalMessage(string argument)
        {
            WriteLogLCDs($"ParseTerminalMessage: {argument}");

            if (argument == "UNLOAD_FINISHED") UnloadFinished();
            else if (argument == "ALIGN_LOADING") AlignLoading();
            else if (argument == "ALIGN_REQUEST_UNLOAD") AlignRequestUnload();
            else if (argument == "ALIGN_UNLOADING") AlignUnloading();
            else if (argument == "ALIGN_UNLOADED") AlignUnloaded();
            else if (argument == "ARRIVAL_WAITING") ArrivalWainting();
            else if (argument == "ARRIVAL_REQUEST_UNLOAD") ArrivalRequestUnload();
            else if (argument == "DO_TRIP_WAYPOINTS") DoTripWaypoints();
            else if (argument == "ENABLE_LOGS") EnableLogs();
        }
        /// <summary>
        /// Sec_C_2b - Cuando la nave llega al conector de carga, informa a la base para comenzar la carga
        /// Request  : ALIGN_LOADING
        /// Execute  : LOADING
        /// </summary>
        void AlignLoading()
        {
            string message = $"Command=LOADING|To={currentTrip.OrderWarehouse}|From={shipId}|Order={currentTrip.OrderId}|Exchange={currentTrip.ExchangeName}";
            SendIGCMessage(message);

            //Atraque
            timerLock.ApplyAction("Start");

            //Activar modo carga
            timerLoad.ApplyAction("Start");

            status = ShipStatus.Loading;
        }
        /// <summary>
        /// Sec_C_4b - NAVEX llega al último waypoint de la salida del conector y activa el piloto automático
        /// Request:  ALIGN_REQUEST_UNLOAD
        /// </summary>
        void AlignRequestUnload()
        {
            remotePilot.SetAutoPilotEnabled(true);
        }
        /// <summary>
        /// Sec_C_4c - NAVEX llega a Parking_BASEX y solicita permiso para descargar
        /// Request:  ARRIVAL_REQUEST_UNLOAD
        /// Execute:  REQUEST_UNLOAD
        /// </summary>
        void ArrivalRequestUnload()
        {
            status = ShipStatus.WaitingForUnload;

            remotePilot.SetAutoPilotEnabled(false);
            remotePilot.ClearWaypoints();

            string command = $"Command=REQUEST_UNLOAD|To={currentTrip.OrderCustomer}|From={shipId}|Order={currentTrip.OrderId}";
            SendIGCMessage(command);

            timerWaiting.ApplyAction("Start");
        }
        /// <summary>
        /// Sec_D_2b - NAVEX avisa a BASEX que ha llegado para descargar el ID_PEDIDO en el conector. Lanza [UNLOADING] a BASEX
        /// Request:  ALIGN_UNLOADING
        /// Execute:  UNLOADING
        /// </summary>
        void AlignUnloading()
        {
            string command = $"Command=UNLOADING|To={currentTrip.OrderCustomer}|From={shipId}|Order={currentTrip.OrderId}|Exchange={currentTrip.ExchangeName}";
            SendIGCMessage(command);

            timerLock.ApplyAction("Start");

            //Activar modo descarga
            timerUnload.ApplyAction("Start");

            status = ShipStatus.Unloading;
        }
        /// <summary>
        /// Sec_D_2d - NAVEX informa del fin de la descarga a BASEX, empieza el camino de vuelta a Parking_WH
        /// Execute:  UNLOADED - Informa a BASEX
        /// Execute:  ALIGN_UNLOADED - Comienza la maniobra de salida del conector
        /// Execute:  ARRIVAL_WAITING - Marca como punto destino Parking_WH
        /// </summary>
        void UnloadFinished()
        {
            string message = $"Command=UNLOADED|To={currentTrip.OrderCustomer}|From={shipId}|Order={currentTrip.OrderId}";
            SendIGCMessage(message);

            status = ShipStatus.RouteToWarehouse;

            //Carga la ruta de salida y al llegar al último waypoint del conector, ejecutará ALIGN_UNLOADED, que activará el pilotaje automático
            currentTrip.AlignFwd = currentTrip.ExchangeForward;
            currentTrip.AlignUp = currentTrip.ExchangeUp;
            currentTrip.Waypoints = currentTrip.ExchangeDepartingWaypoints;
            currentTrip.OnLastWaypoint = "ALIGN_UNLOADED";
            //Monitorizará el viaje hasta la posición de espera del Warehouse, y ejecutará ARRIVAL_WAITING, y esperará instrucciones de la base
            currentTrip.DestinationName = currentTrip.OrderWarehouse;
            currentTrip.DestinationPosition = currentTrip.OrderWarehouseParking;
            currentTrip.OnDestinationArrival = "ARRIVAL_WAITING";

            Depart();

            //Limpiar datos del pedido
            currentTrip.OrderId = -1;
            currentTrip.OrderWarehouse = "";
            currentTrip.OrderWarehouseParking = new Vector3D();
            currentTrip.OrderCustomer = "";
            currentTrip.OrderCustomerParking = new Vector3D();

            //Limpiar datos del exchange
            currentTrip.ExchangeName = null;
            currentTrip.ExchangeForward = null;
            currentTrip.ExchangeUp = null;
            currentTrip.ExchangeApproachingWaypoints = null;
            currentTrip.ExchangeDepartingWaypoints = null;
        }
        /// <summary>
        /// Sec_D_2e - NAVEX activa el piloto automático cuando alcanza el último waypoint del conector
        /// </summary>
        void AlignUnloaded()
        {
            //Activa el pilotaje automático
            remotePilot.SetAutoPilotEnabled(true);
        }
        /// <summary>
        /// Sec_D_2f - NAVEX alcanza Parking_WH y se queda en espera
        /// </summary>
        void ArrivalWainting()
        {
            //Se produce cuando la nave llega al último waypoint de la ruta de entrega
            status = ShipStatus.Idle;

            remotePilot.SetAutoPilotEnabled(false);
            remotePilot.ClearWaypoints();

            //Pone la nave en espera
            timerWaiting.ApplyAction("Start");
        }
        /// <summary>
        /// Cambia el estado de la variable que controla la visualización de los logs
        /// </summary>
        void EnableLogs()
        {
            enableLogs = !enableLogs;
        }

        void ParseMessage(string signal)
        {
            WriteLogLCDs($"ParseMessage: {signal}");

            Echo($"Mensaje recibido: {signal}");
            string[] lines = signal.Split('|');

            string command = ReadArgument(lines, "Command");
            if (command == "REQUEST_STATUS") CmdRequestStatus(lines);
            else if (command == "LOAD_ORDER") CmdLoadOrder(lines);
            else if (command == "LOADED") CmdLoaded(lines);
            else if (command == "UNLOAD_ORDER") CmdUnloadOrder(lines);
            else if (command == "GOTO_WAREHOUSE") CmdGotoWarehouse(lines);
        }
        /// <summary>
        /// Sec_A_2 - La nave responde con su estado
        /// Request:  REQUEST_STATUS
        /// Execute:  RESPONSE_STATUS
        /// </summary>
        void CmdRequestStatus(string[] lines)
        {
            string from = ReadArgument(lines, "From");
            Vector3D position = remotePilot.GetPosition();
            string message = $"Command=RESPONSE_STATUS|To={from}|From={shipId}|Status={status}|Origin={currentTrip.OrderWarehouse}|OriginPosition={VectorToStr(currentTrip.OrderWarehouseParking)}|Destination={currentTrip.OrderCustomer}|DestinationPosition={VectorToStr(currentTrip.OrderCustomerParking)}|Position={VectorToStr(position)}";
            SendIGCMessage(message);
        }
        /// <summary>
        /// Sec_C_2a - NAVEX comienza la navegación al conector especificado y atraca en MODO CARGA.
        /// Request:  LOAD_ORDER
        /// Execute:  ALIGN_LOADING cuando la nave alcance el conector de carga del WH
        /// </summary>
        void CmdLoadOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            currentTrip.OrderId = int.Parse(ReadArgument(lines, "Order"));
            currentTrip.OrderWarehouse = ReadArgument(lines, "Warehouse");
            currentTrip.OrderWarehouseParking = StrToVector(ReadArgument(lines, "WarehouseParking"));
            currentTrip.OrderCustomer = ReadArgument(lines, "Customer");
            currentTrip.OrderCustomerParking = StrToVector(ReadArgument(lines, "CustomerParking"));

            currentTrip.ExchangeName = ReadArgument(lines, "Exchange");
            currentTrip.ExchangeForward = ReadArgument(lines, "Forward");
            currentTrip.ExchangeUp = ReadArgument(lines, "Up");
            currentTrip.ExchangeApproachingWaypoints = ReadArgument(lines, "WayPoints");
            currentTrip.ExchangeDepartingWaypoints = ReverseString(currentTrip.ExchangeApproachingWaypoints);

            status = ShipStatus.ApproachingWarehouse;

            Align(currentTrip.ExchangeForward, currentTrip.ExchangeUp, currentTrip.ExchangeApproachingWaypoints, "ALIGN_LOADING");
        }
        /// <summary>
        /// Sec_C_4a - NAVEX carga la ruta hasta Parking_BASEX y comienza la maniobra de salida desde el conector de WH
        /// Request:  LOADED
        /// Execute:  ALIGN_REQUEST_UNLOAD cuando NAVEX llegue al último waypoint de la ruta de salida del conector
        /// Execute:  ARRIVAL_REQUEST_UNLOAD cuando NAVEX llegue a Parking_BASEX
        /// </summary>
        void CmdLoaded(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.RouteToCustomer;

            currentTrip.AlignFwd = currentTrip.ExchangeForward;
            currentTrip.AlignUp = currentTrip.ExchangeUp;
            currentTrip.Waypoints = currentTrip.ExchangeDepartingWaypoints;
            currentTrip.OnLastWaypoint = "ALIGN_REQUEST_UNLOAD";
            currentTrip.DestinationName = currentTrip.OrderCustomer;
            currentTrip.DestinationPosition = currentTrip.OrderCustomerParking;
            currentTrip.OnDestinationArrival = "ARRIVAL_REQUEST_UNLOAD";

            Depart();

            //Limpiar datos del exchange
            currentTrip.ExchangeName = null;
            currentTrip.ExchangeForward = null;
            currentTrip.ExchangeUp = null;
            currentTrip.ExchangeApproachingWaypoints = null;
            currentTrip.ExchangeDepartingWaypoints = null;
        }
        /// <summary>
        /// Sec_D_2a - NAVEX comienza la navegación al conector especificado y atraca en MODO DESCARGA.
        /// Execute:  ALIGN_UNLOADING
        /// </summary>
        void CmdUnloadOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            currentTrip.ExchangeName = ReadArgument(lines, "Exchange");
            currentTrip.ExchangeForward = ReadArgument(lines, "Forward");
            currentTrip.ExchangeUp = ReadArgument(lines, "Up");
            currentTrip.ExchangeApproachingWaypoints = ReadArgument(lines, "WayPoints");
            currentTrip.ExchangeDepartingWaypoints = ReverseString(currentTrip.ExchangeApproachingWaypoints);

            status = ShipStatus.ApproachingCustomer;

            currentTrip.AlignFwd = currentTrip.ExchangeForward;
            currentTrip.AlignUp = currentTrip.ExchangeUp;
            currentTrip.Waypoints = currentTrip.ExchangeApproachingWaypoints;
            currentTrip.OnLastWaypoint = "ALIGN_UNLOADING";

            Approach();
        }
        /// <summary>
        /// NEW - Va directamente al parking del cliente, sin pasar por el Warehouse
        /// </summary>
        void CmdGotoWarehouse(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            currentTrip.OrderId = int.Parse(ReadArgument(lines, "Order"));
            currentTrip.OrderWarehouse = ReadArgument(lines, "Warehouse");
            currentTrip.OrderWarehouseParking = StrToVector(ReadArgument(lines, "WarehouseParking"));
            currentTrip.OrderCustomer = ReadArgument(lines, "Customer");
            currentTrip.OrderCustomerParking = StrToVector(ReadArgument(lines, "CustomerParking"));

            status = ShipStatus.RouteToWarehouse;

            SetTripAutoPilot(currentTrip.OrderWarehouseParking, currentTrip.OrderWarehouse, "ARRIVAL_WAITING", true);
        }

        /// <summary>
        /// Realiza la maniobra de aproximación desde cualquier posición
        /// </summary>
        void Approach()
        {
            //Obtener la distancia al primer punto de aproximación
            var shipPosition = remotePilot.GetPosition();
            var wp = ParseWaypoints(currentTrip.Waypoints).First();
            double distance = Vector3D.Distance(shipPosition, wp);
            if (distance > 200)
            {
                //Carga en el piloto automático hasta la posición del primer waypoint
                SetTripAutoPilot(wp, "Path to Connector", "DO_TRIP_WAYPOINTS", true);
            }
            else
            {
                DoTripWaypoints();
            }
        }
        /// <summary>
        /// Realiza la maniobra de desacople y viaja hasta el destino
        /// </summary>
        void Depart()
        {
            timerUnlock?.ApplyAction("Start");

            //Comienza la maniobra de salida
            Align(currentTrip.AlignFwd, currentTrip.AlignUp, currentTrip.Waypoints, currentTrip.OnLastWaypoint);

            //Carga los datos en el piloto automático y espera
            SetTripAutoPilot(currentTrip.DestinationPosition, currentTrip.DestinationName, currentTrip.OnDestinationArrival, false);
        }
        /// <summary>
        /// Recorre los waypoints
        /// </summary>
        void DoTripWaypoints()
        {
            Align(currentTrip.AlignFwd, currentTrip.AlignUp, currentTrip.Waypoints, currentTrip.OnLastWaypoint);
        }
        /// <summary>
        /// Configura el piloto automático
        /// </summary>
        void SetTripAutoPilot(Vector3D destination, string destinationName, string onArrival, bool start)
        {
            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(destination, destinationName);
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.WaitForFreeWay = true;
            remotePilot.FlightMode = FlightMode.OneWay;

            Arrival(destination, onArrival);

            if (start) remotePilot.SetAutoPilotEnabled(true);
        }
    }
}
