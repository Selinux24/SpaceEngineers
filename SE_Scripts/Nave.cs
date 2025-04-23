using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace Nave
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string shipId = "NaveBETA1";
        const string shipRemoteControlPilot = "Remote Control Pilot";
        const string shipTimerPilot = "Automaton Timer Block Pilot";
        const string shipArrivalPB = "Automaton Programmable Block Arrival";
        const string shipAlignPB = "Automaton Programmable Block Align";
        const string shipTimerLoad = "Automaton Timer Block Load";
        const string shipTimerUnload = "Automaton Timer Block Unload";

        enum ShipStatus
        {
            Unknown,
            Idle,
            Busy,
        }

        IMyBroadcastListener bl;
        IMyRemoteControl remotePilot;
        IMyTimerBlock timerPilot;
        IMyProgrammableBlock arrivalPB;
        IMyProgrammableBlock alignPB;
        IMyTimerBlock timerLoad;
        IMyTimerBlock timerUnload;

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
        void SendIGCMessage(string message)
        {
            IGC.SendBroadcastMessage(channel, message);
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
            if (argument == "DELIVER")
            {
                CmdDeliver();
            }
            else if (argument == "RETURN")
            {
                CmdReturn();
            }
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
            arrivalPB.TryRun($"Position={VectorToStr(orderCustomerParking)}||Command=UNLOAD|To={orderCustomer}|From={shipId}|Order={orderId}");
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
            arrivalPB.TryRun($"Position={VectorToStr(orderCustomerParking)}||Command=WAITING|To={shipId}");
        }

        void ParseMessage(string signal)
        {
            Echo($"Mensaje recibido: {signal}");
            string[] lines = signal.Split('|');

            string command = ReadArgument(lines, "Command");
            if (command == "REQUEST_STATUS")
            {
                CmdRequestStatus(lines);
            }
            else if (command == "LOAD_ORDER")
            {
                CmdLoadOrder(lines);
            }
            else if (command == "UNLOAD_ORDER")
            {
                CmdUnloadOrder(lines);
            }
            else if (command == "WAITING")
            {
                CmdWaiting(lines);
            }
        }
        void CmdRequestStatus(string[] lines)
        {
            //[Command=STATUS|To=Sender|From=Me|Status=Status|Origin=Base|OriginPosition=Position|Destination=Base|DestinationPosition=Position|Position=x:y:z]
            string from = ReadArgument(lines, "From");
            Vector3D position = remotePilot.GetPosition();
            string message = $"Command=STATUS|To={from}|From={shipId}|Status={status}|Origin={orderFrom}|OriginPosition={VectorToStr(orderFromParking)}|Destination={orderCustomer}|DestinationPosition={VectorToStr(orderCustomerParking)}|Position={VectorToStr(position)}";
            SendIGCMessage(message);
        }
        void CmdLoadOrder(string[] lines)
        {
            //[Command=LOAD_ORDER|To=Me|From=Sender|For=Customer|ForParking=CustomerParking|Order=ID_PEDIDO|Forward=x:y:z|Up=x:y:z|WayPoints=x:y:z;]
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

            alignPB.TryRun($"ALIGN|{forward}|{up}|{wayPoints}");
            timerLoad.ApplyAction("Start");
            Echo("A cargar!!");
        }
        void CmdUnloadOrder(string[] lines)
        {
            //[Command=UNLOAD_ORDER|To=Me|Forward=x:y:z|Up=x:y:z|WayPoints=x:y:z;]
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            string forward = ReadArgument(lines, "Forward");
            string up = ReadArgument(lines, "Up");
            string wayPoints = ReadArgument(lines, "WayPoints");

            alignPB.TryRun($"ALIGN|{forward}|{up}|{wayPoints}");
            timerUnload.ApplyAction("Start");
            Echo("A descargar!!");
        }
        void CmdWaiting(string[] lines)
        {
            //[Command=WAITING|To=Me]
            string to = ReadArgument(lines, "To");
            if (to != shipId)
            {
                return;
            }

            status = ShipStatus.Idle;
        }
    }
}
