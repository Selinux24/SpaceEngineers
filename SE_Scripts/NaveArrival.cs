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
        const string shipRemoteControlPilot = "Remote Control Pilot";
        const double arrivalThreshold = 200.0;

        IMyRemoteControl remote;

        bool hasPosition = false;
        Vector3D targetPosition = Vector3D.Zero;
        string arrivalMessage = null;

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
        void ParseMessage(string message)
        {
            hasPosition = false;
            targetPosition = Vector3D.Zero;
            arrivalMessage = null;

            var parts = message.Split("||".ToCharArray());
            if (parts.Length != 2)
            {
                return;
            }

            //Parsear la posición objetivo
            targetPosition = StrToVector(parts[0]);
            hasPosition = true;

            arrivalMessage = parts[1]?.Trim() ?? "";
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
                ParseMessage(argument);
                return;
            }

            if ((updateSource & UpdateType.Update10) != 0)
            {
                DoArrival();
            }
        }

        void DoArrival()
        {
            if (!hasPosition)
            {
                Echo("Posición objetivo no definida.");
                return;
            }

            Vector3D currentPos = remote.GetPosition();
            double distance = Vector3D.Distance(currentPos, targetPosition);

            Echo($"Distancia a destino: {distance:F2}m.");

            if (distance <= arrivalThreshold)
            {
                Echo("Llegada detectada!.");
                if (arrivalMessage != null)
                {
                    SendIGCMessage(arrivalMessage);
                }
                Runtime.UpdateFrequency = UpdateFrequency.None;  // Detener comprobaciones
                hasPosition = false;
                targetPosition = Vector3D.Zero;
                arrivalMessage = null;
            }
        }
    }
}
