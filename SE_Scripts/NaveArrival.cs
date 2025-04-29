using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace NaveArrival
{
    partial class Program : MyGridProgram
    {
        const string shipProgrammableBlock = "HT Automaton Programmable Block Ship";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const double arrivalThreshold = 200.0;

        readonly IMyProgrammableBlock pb;
        readonly IMyRemoteControl remote;

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

        public Program()
        {
            pb = GetBlockWithName<IMyProgrammableBlock>(shipProgrammableBlock);
            if (pb == null)
            {
                Echo($"Programmable Block {shipProgrammableBlock} no encontrado.");
                return;
            }

            remote = GetBlockWithName<IMyRemoteControl>(shipRemoteControlPilot);
            if (remote == null)
            {
                Echo($"Remote Control {shipRemoteControlPilot} no encontrado.");
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

            if ((updateSource & UpdateType.Update100) != 0)
            {
                DoArrival();
            }
        }

        void ParseTerminalMessage(string message)
        {
            hasPosition = false;
            targetPosition = Vector3D.Zero;
            arrivalMessage = null;

            var parts = message.Split('¬');
            if (parts.Length != 2)
            {
                return;
            }

            //Parsear la posición objetivo
            hasPosition = true;
            targetPosition = StrToVector(parts[0]);
            arrivalMessage = parts[1];

            Runtime.UpdateFrequency = UpdateFrequency.Update100;  // Comenzar a comprobar la llegada
        }

        void DoArrival()
        {
            if (!hasPosition)
            {
                Echo("Posición objetivo no definida.");
                return;
            }

            double distance = Vector3D.Distance(remote.GetPosition(), targetPosition);
            if (distance <= arrivalThreshold)
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;  // Detener comprobaciones

                Echo("Posición alcanzada.");

                if (!string.IsNullOrWhiteSpace(arrivalMessage))
                {
                    pb.TryRun(arrivalMessage);
                    Echo($"Ejecutado {arrivalMessage}");
                }

                hasPosition = false;
                targetPosition = Vector3D.Zero;
                arrivalMessage = null;

                return;
            }

            Echo($"Distancia a destino: {distance:F2}m.");
        }
    }
}
