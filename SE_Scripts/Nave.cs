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
            Busy,
        }

        readonly IMyBroadcastListener bl;
        readonly IMyRemoteControl remotePilot;
        readonly IMyTimerBlock timerPilot;
        readonly IMyProgrammableBlock arrivalPB;
        readonly IMyProgrammableBlock alignPB;
        readonly IMyTimerBlock timerLoad;
        readonly IMyTimerBlock timerUnload;
        readonly List<IMyTextPanel> logLCDs = new List<IMyTextPanel>();
        readonly StringBuilder sbLog = new StringBuilder();

        string exitForward;
        string exitUp;
        string exitWaypoints;
        string exitExchangeName;

        string orderFrom;
        Vector3D orderFromParking;
        string orderCustomer;
        Vector3D orderCustomerParking;
        int orderId;
        ShipStatus status = ShipStatus.Idle;

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
        static Vector3D[] StrToVectorList(string str)
        {
            string[] coords = str.Split(';');
            Vector3D[] vectors = new Vector3D[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                vectors[i] = StrToVector(coords[i]);
            }
            return vectors;
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
        void Align(string forward, string up, string wayPoints, string command, string timerName)
        {
            string message = $"{forward}|{up}|{wayPoints}¬{command}¬{timerName}";
            WriteLogLCDs($"Align: {message}");
            alignPB.TryRun(message);
        }
        void Arrival(Vector3D position, string command, string timerName)
        {
            string message = $"{VectorToStr(position)}¬{command}¬{timerName}";
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

            GridTerminalSystem.GetBlocksOfType(logLCDs, lcd => lcd.CubeGrid == Me.CubeGrid && lcd.CustomName.Contains(shipLogLCDs));

            WriteLCDs("[shipId]", shipId);

            bl = IGC.RegisterBroadcastListener(channel);
            Runtime.UpdateFrequency = UpdateFrequency.Update100; // Ejecuta cada ~1.6s
            Echo($"Listening in channel {channel}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
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

            if (argument == "DELIVER") CmdDeliver();
            else if (argument == "RETURN") CmdReturn();
            else if (argument == "UNLOADED") SendUnloaded();
        }
        void CmdDeliver()
        {
            if (orderId < 0)
            {
                return;
            }

            //Carga las coordenadas de entrega en el remote control de pilotaje
            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(orderCustomerParking, "Customer");

            //Activa el modo de pilotaje para evitar colisiones
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            //Inicia el timer de pilotaje
            timerPilot.ApplyAction("Start");

            //Lanza el script de control de proximidad
            string command = $"Command=UNLOAD|To={orderCustomer}|From={shipId}|Order={orderId}";
            Arrival(orderCustomerParking, command, shipTimerLock);
        }
        void CmdReturn()
        {
            //Carga las coordenadas de entrega en el remote control de pilotaje
            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(orderFromParking, "Origin");

            //Activa el modo de pilotaje para evitar colisiones
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            //Inicia el timer de pilotaje
            timerPilot.ApplyAction("Start");

            //Lanza el script de control de proximidad
            string command = $"Command=WAITING|To={shipId}";
            Arrival(orderCustomerParking, command, shipTimerWaiting);
        }
        void SendUnloaded()
        {
            string message = $"Command=UNLOADED|To={orderFrom}|From={shipId}|Order={orderId}";
            SendIGCMessage(message);

            //Comenzar viaje de regreso a Parking WH
            var wpList = StrToVectorList(exitWaypoints);
            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(wpList.Last(), orderFrom);
            remotePilot.AddWaypoint(orderCustomerParking, "Customer");
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            Align(exitForward, exitUp, exitWaypoints, null, shipTimerWaiting);

            string command = $"Command=WAITING|To={shipId}";
            Arrival(orderFromParking, command, shipTimerWaiting);

            //Limpiar los datos del exchange
            exitForward = "";
            exitUp = "";
            exitWaypoints = "";
            exitExchangeName = "";

            //Limpiar datos del pedido
            orderFrom = "";
            orderFromParking = new Vector3D();
            orderCustomer = "";
            orderCustomerParking = new Vector3D();
            orderId = -1;
            status = ShipStatus.Idle;
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
            string message = $"Command=RESPONSE_STATUS|To={from}|From={shipId}|Status={status}|Origin={orderFrom}|OriginPosition={VectorToStr(orderFromParking)}|Destination={orderCustomer}|DestinationPosition={VectorToStr(orderCustomerParking)}|Position={VectorToStr(position)}";
            SendIGCMessage(message);
        }
        void CmdLoadOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }
            orderFrom = ReadArgument(lines, "From");
            orderFromParking = StrToVector(ReadArgument(lines, "FromParking"));
            orderCustomer = ReadArgument(lines, "For");
            orderCustomerParking = StrToVector(ReadArgument(lines, "ForParking"));
            orderId = int.Parse(ReadArgument(lines, "Order"));
            status = ShipStatus.Busy;

            string forward = ReadArgument(lines, "Forward");
            string up = ReadArgument(lines, "Up");
            string wayPoints = ReadArgument(lines, "WayPoints");
            string exchangeName = ReadArgument(lines, "Exchange");

            string message = $"Command=LOADING|To={orderFrom}|From={shipId}|Order={orderId}|Exchange={exchangeName}";
            Align(forward, up, wayPoints, message, shipTimerLock);

            //Activar modo carga
            timerLoad.ApplyAction("Start");
        }
        void CmdLoaded(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            exitForward = ReadArgument(lines, "Forward");
            exitUp = ReadArgument(lines, "Up");
            exitWaypoints = ReadArgument(lines, "WayPoints");

            var wpList = StrToVectorList(exitWaypoints);
            remotePilot.ClearWaypoints();
            remotePilot.AddWaypoint(wpList.Last(), orderCustomer);
            remotePilot.AddWaypoint(orderFromParking, "Warehouse");
            remotePilot.SetCollisionAvoidance(true);
            remotePilot.FlightMode = FlightMode.OneWay;

            Align(exitForward, exitUp, exitWaypoints, null, shipTimerPilot);

            string command = $"Command=REQUEST_UNLOAD|To={orderCustomer}|From={shipId}|Order={orderId}";
            Arrival(orderCustomerParking, command, shipTimerLock);

            //Limpiar los datos del exchange
            exitForward = "";
            exitUp = "";
            exitWaypoints = "";
        }
        void CmdUnloadOrder(string[] lines)
        {
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            exitForward = ReadArgument(lines, "Forward");
            exitUp = ReadArgument(lines, "Up");
            exitWaypoints = ReadArgument(lines, "WayPoints");
            exitExchangeName = ReadArgument(lines, "Exchange");

            string command = $"Command=LOADING|To={orderFrom}|From={shipId}|Order={orderId}|Exchange={exitExchangeName}";
            Align(exitForward, exitUp, exitWaypoints, command, shipTimerLock);

            //Activar modo descarga
            timerUnload.ApplyAction("Start");
        }
    }
}
