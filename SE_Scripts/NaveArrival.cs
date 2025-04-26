using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace NaveArrival
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const double arrivalThreshold = 200.0;

        readonly IMyRemoteControl remote;

        bool hasPosition = false;
        Vector3D targetPosition = Vector3D.Zero;
        string arrivalMessage = null;
        string arrivalTimer = null;

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
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
        void SendIGCMessage(string message)
        {
            IGC.SendBroadcastMessage(channel, message);
        }

        public Program()
        {
            remote = GetBlockWithName<IMyRemoteControl>(shipRemoteControlPilot);
            if (remote == null)
            {
                Echo($"RemoteControl {shipRemoteControlPilot} no encontrado.");
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.None;
            Echo("Working");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrWhiteSpace(argument))
            {
                ParseTerminalMessage(argument);
                return;
            }

            if ((updateSource & UpdateType.Update10) != 0)
            {
                DoArrival();
            }
        }

        void ParseTerminalMessage(string message)
        {
            hasPosition = false;
            targetPosition = Vector3D.Zero;
            arrivalMessage = null;
            arrivalTimer = null;

            var parts = message.Split('¬');
            if (parts.Length < 2)
            {
                return;
            }

            //Parsear la posición objetivo
            hasPosition = true;
            targetPosition = StrToVector(parts[0]);
            arrivalMessage = parts[1]?.Trim() ?? "";

            if (parts.Length > 2) arrivalTimer = parts[2]?.Trim() ?? "";
        }

        void DoArrival()
        {
            if (!hasPosition)
            {
                Echo("Posición objetivo no definida.");
                return;
            }

            double distance = Vector3D.Distance(remote.GetPosition(), targetPosition);
            Echo($"Distancia a destino: {distance:F2}m.");

            if (distance <= arrivalThreshold)
            {
                Echo("Llegada detectada!.");

                if (!string.IsNullOrWhiteSpace(arrivalMessage))
                {
                    Echo(arrivalMessage);
                    SendIGCMessage(arrivalMessage);
                }
                if (!string.IsNullOrWhiteSpace(arrivalTimer))
                {
                    Echo(arrivalTimer);
                    GetBlockWithName<IMyTimerBlock>(arrivalTimer)?.ApplyAction("Start");
                }

                Runtime.UpdateFrequency = UpdateFrequency.None;  // Detener comprobaciones
                hasPosition = false;
                targetPosition = Vector3D.Zero;
                arrivalMessage = null;
                arrivalTimer = null;
            }
        }
    }
}
