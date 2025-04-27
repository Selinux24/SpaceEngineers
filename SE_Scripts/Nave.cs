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
        const string shipTimerLoad = "HT Automaton Timer Block Load";
        const string shipTimerUnload = "HT Automaton Timer Block Unload";
        const string shipTimerWaiting = "HT Automaton Timer Block Waiting";
        const string shipLogLCDs = "[DELIVERY_LOG]";

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
            Unloaded,
            RouteToWarehouse,
        }

        readonly IMyBroadcastListener bl;
        readonly IMyRemoteControl remotePilot;
        readonly IMyTimerBlock timerPilot;
        readonly IMyProgrammableBlock arrivalPB;
        readonly IMyProgrammableBlock alignPB;
        readonly IMyTimerBlock timerLock;
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
        string orderExchangeName;

        string exitForward;
        string exitUp;
        string exitWaypoints;
        string exitExchangeName;

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

            if (!string.IsNullOrEmpty(argument) && (updateSource & UpdateType.Terminal) != 0)
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

            if (argument == "UNLOADED") SendUnloaded();
            else if (argument == "ALIGN_LOADING") AlignLoading();
            else if (argument == "ALIGN_REQUEST_UNLOAD") AlignRequestUnload();
            else if (argument == "ALIGN_UNLOADING") AlignUnloading();
            else if (argument == "ALIGN_UNLOADED") AlignUnloaded();
            else if (argument == "ARRIVAL_WAITING") ArrivalWainting();
            else if (argument == "ARRIVAL_UNLOAD") ArrivalUnload();
            else if (argument == "ARRIVAL_REQUEST_UNLOAD") ArrivalRequestUnload();
        }
        void SendUnloaded()
        {
            string message = $"Command=UNLOADED|To={orderCustomer}|From={shipId}|Order={orderId}";
            SendIGCMessage(message);

            status = ShipStatus.Unloaded;

            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(orderCustomerParking, orderCustomer);
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            //Carga la ruta de salida y al llegar al último waypoint del conector, ejecutará ALIGN_UNLOADED, que activará el pilotaje automático
            Align(exitForward, exitUp, exitWaypoints, "ALIGN_UNLOADED");

            //Monitorizará el viaje hasta la posición de espera del cliente, y ejecutará ARRIVAL_WAITING, y esperará instrucciones de la base
            Arrival(orderCustomerParking, "ARRIVAL_WAITING");

            //Limpiar datos del pedido
            orderId = -1;
            orderWarehouse = "";
            orderWarehouseParking = new Vector3D();
            orderCustomer = "";
            orderCustomerParking = new Vector3D();
            status = ShipStatus.Idle;

            //Limpiar los datos del exchange
            exitForward = "";
            exitUp = "";
            exitWaypoints = "";
            exitExchangeName = "";
        }
        void AlignLoading()
        {
            string message = $"Command=LOADING|To={orderWarehouse}|From={shipId}|Order={orderId}|Exchange={orderExchangeName}";
            SendIGCMessage(message);

            //Atraque
            timerLock.ApplyAction("Start");

            //Activar modo carga
            timerLoad.ApplyAction("Start");
        }
        void AlignRequestUnload()
        {
            timerPilot.ApplyAction("Start");
        }
        void AlignUnloading()
        {
            string command = $"Command=UNLOADING|To={orderCustomer}|From={shipId}|Order={orderId}|Exchange={exitExchangeName}";
            SendIGCMessage(command);

            timerLock.ApplyAction("Start");

            //Activar modo descarga
            timerUnload.ApplyAction("Start");
        }
        void AlignUnloaded()
        {
            //Se produce cuando la nave llega al último waypoint de la ruta de salida
            status = ShipStatus.RouteToWarehouse;

            //Activa el pilotaje automático
            timerPilot.ApplyAction("Start");
        }
        void ArrivalWainting()
        {
            //Se produce cuando la nave llega al último waypoint de la ruta de entrega
            status = ShipStatus.Idle;

            //Pone la nave en espera
            timerWaiting.ApplyAction("Start");
        }
        void ArrivalUnload()
        {
            //Se produce cuando la nave llega al punto de espera de la base de descarga

            //Lanza el comando de descarga
            string command = $"Command=UNLOAD|To={orderCustomer}|From={shipId}|Order={orderId}";
            SendIGCMessage(command);

            timerLock.ApplyAction("Start");
        }
        void ArrivalRequestUnload()
        {
            status = ShipStatus.WaitingForUnload;

            string command = $"Command=REQUEST_UNLOAD|To={orderCustomer}|From={shipId}|Order={orderId}";
            SendIGCMessage(command);

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
        void CmdRequestStatus(string[] lines)
        {
            string from = ReadArgument(lines, "From");
            Vector3D position = remotePilot.GetPosition();
            string message = $"Command=RESPONSE_STATUS|To={from}|From={shipId}|Status={status}|Origin={orderWarehouse}|OriginPosition={VectorToStr(orderWarehouseParking)}|Destination={orderCustomer}|DestinationPosition={VectorToStr(orderCustomerParking)}|Position={VectorToStr(position)}";
            SendIGCMessage(message);
        }
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
            orderExchangeName = ReadArgument(lines, "Exchange");

            string forward = ReadArgument(lines, "Forward");
            string up = ReadArgument(lines, "Up");
            string wayPoints = ReadArgument(lines, "WayPoints");

            //Se acerca hasta el conector de carga y lanza ALIGN_LOADING
            Align(forward, up, wayPoints, "ALIGN_LOADING");
        }
        void CmdLoaded(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.RouteToCustomer;

            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(orderCustomerParking, orderCustomer);
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            Arrival(orderCustomerParking, "ARRIVAL_REQUEST_UNLOAD");

            var forward = ReadArgument(lines, "Forward");
            var up = ReadArgument(lines, "Up");
            var waypoints = ReadArgument(lines, "WayPoints");

            Align(forward, up, waypoints, "ALIGN_REQUEST_UNLOAD");
        }
        void CmdUnloadOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.ApproachingCustomer;

            exitForward = ReadArgument(lines, "Forward");
            exitUp = ReadArgument(lines, "Up");
            exitWaypoints = ReadArgument(lines, "WayPoints");
            exitExchangeName = ReadArgument(lines, "Exchange");

            Align(exitForward, exitUp, exitWaypoints, "ALIGN_UNLOADING");
        }
    }
}
