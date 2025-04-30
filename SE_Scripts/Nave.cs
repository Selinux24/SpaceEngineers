using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace Nave
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string shipId = "NaveBETA1";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const string shipArrivalPB = "HT Automaton Programmable Block Arrival";
        const string shipAlignPB = "HT Automaton Programmable Block Align";
        const string shipTimerPilot = "HT Automaton Timer Block Pilot";
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

        int orderId;
        string orderWarehouse;
        Vector3D orderWarehouseParking;
        string orderCustomer;
        Vector3D orderCustomerParking;

        string exchangeName;
        string exchangeForward;
        string exchangeUp;
        string exchangeApproachingWaypoints;
        string exchangeDepartingWaypoints;

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
            timerPilot = GetBlockWithName<IMyTimerBlock>(shipTimerPilot);
            if (timerPilot == null)
            {
                Echo($"Timer '{shipTimerPilot}' no locallizado.");
                return;
            }

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
        }
        /// <summary>
        /// Sec_C_2b - Cuando la nave llega al conector de carga, informa a la base para comenzar la carga
        /// Request  : ALIGN_LOADING
        /// Execute  : LOADING
        /// </summary>
        void AlignLoading()
        {
            string message = $"Command=LOADING|To={orderWarehouse}|From={shipId}|Order={orderId}|Exchange={exchangeName}";
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
            timerPilot.ApplyAction("Start");
        }
        /// <summary>
        /// Sec_C_4c - NAVEX llega a Parking_BASEX y solicita permiso para descargar
        /// Request:  ARRIVAL_REQUEST_UNLOAD
        /// Execute:  REQUEST_UNLOAD
        /// </summary>
        void ArrivalRequestUnload()
        {
            status = ShipStatus.WaitingForUnload;

            string command = $"Command=REQUEST_UNLOAD|To={orderCustomer}|From={shipId}|Order={orderId}";
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
            string command = $"Command=UNLOADING|To={orderCustomer}|From={shipId}|Order={orderId}|Exchange={exchangeName}";
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
            string message = $"Command=UNLOADED|To={orderCustomer}|From={shipId}|Order={orderId}";
            SendIGCMessage(message);

            timerUnlock?.ApplyAction("Start");

            status = ShipStatus.RouteToWarehouse;

            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(orderWarehouseParking, orderCustomer);
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            //Carga la ruta de salida y al llegar al último waypoint del conector, ejecutará ALIGN_UNLOADED, que activará el pilotaje automático
            Align(exchangeForward, exchangeUp, exchangeDepartingWaypoints, "ALIGN_UNLOADED");

            //Monitorizará el viaje hasta la posición de espera del Warehouse, y ejecutará ARRIVAL_WAITING, y esperará instrucciones de la base
            Arrival(orderWarehouseParking, "ARRIVAL_WAITING");

            //Limpiar datos del pedido
            orderId = -1;
            orderWarehouse = "";
            orderWarehouseParking = new Vector3D();
            orderCustomer = "";
            orderCustomerParking = new Vector3D();

            //Limpiar datos del exchange
            exchangeName = null;
            exchangeForward = null;
            exchangeUp = null;
            exchangeApproachingWaypoints = null;
            exchangeDepartingWaypoints = null;
        }
        /// <summary>
        /// Sec_D_2e - NAVEX activa el piloto automático cuando alcanza el último waypoint del conector
        /// </summary>
        void AlignUnloaded()
        {
            //Activa el pilotaje automático
            timerPilot.ApplyAction("Start");
        }
        /// <summary>
        /// Sec_D_2f - NAVEX alcanza Parking_WH y se queda en espera
        /// </summary>
        void ArrivalWainting()
        {
            //Se produce cuando la nave llega al último waypoint de la ruta de entrega
            status = ShipStatus.Idle;

            //Pone la nave en espera
            timerWaiting.ApplyAction("Start");
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
            string message = $"Command=RESPONSE_STATUS|To={from}|From={shipId}|Status={status}|Origin={orderWarehouse}|OriginPosition={VectorToStr(orderWarehouseParking)}|Destination={orderCustomer}|DestinationPosition={VectorToStr(orderCustomerParking)}|Position={VectorToStr(position)}";
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

            status = ShipStatus.ApproachingWarehouse;

            orderId = int.Parse(ReadArgument(lines, "Order"));
            orderWarehouse = ReadArgument(lines, "Warehouse");
            orderWarehouseParking = StrToVector(ReadArgument(lines, "WarehouseParking"));
            orderCustomer = ReadArgument(lines, "Customer");
            orderCustomerParking = StrToVector(ReadArgument(lines, "CustomerParking"));

            exchangeName = ReadArgument(lines, "Exchange");
            exchangeForward = ReadArgument(lines, "Forward");
            exchangeUp = ReadArgument(lines, "Up");
            exchangeApproachingWaypoints = ReadArgument(lines, "WayPoints");
            exchangeDepartingWaypoints = ReverseString(exchangeApproachingWaypoints);

            Align(exchangeForward, exchangeUp, exchangeApproachingWaypoints, "ALIGN_LOADING");
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

            timerUnlock?.ApplyAction("Start");

            status = ShipStatus.RouteToCustomer;

            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(orderCustomerParking, orderCustomer);
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            Arrival(orderCustomerParking, "ARRIVAL_REQUEST_UNLOAD");

            Align(exchangeForward, exchangeUp, exchangeDepartingWaypoints, "ALIGN_REQUEST_UNLOAD");

            //Limpiar datos del exchange
            exchangeName = null;
            exchangeForward = null;
            exchangeUp = null;
            exchangeApproachingWaypoints = null;
            exchangeDepartingWaypoints = null;
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

            status = ShipStatus.ApproachingCustomer;

            exchangeName = ReadArgument(lines, "Exchange");
            exchangeForward = ReadArgument(lines, "Forward");
            exchangeUp = ReadArgument(lines, "Up");
            exchangeApproachingWaypoints = ReadArgument(lines, "WayPoints");
            exchangeDepartingWaypoints = ReverseString(exchangeApproachingWaypoints);

            Align(exchangeForward, exchangeUp, exchangeApproachingWaypoints, "ALIGN_UNLOADING");
        }
    }
}
