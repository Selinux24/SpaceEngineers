using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region Constants
        const string shipProgrammableBlock = "HT Automaton Programmable Block Ship";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const double arrivalThreshold = 200.0;
        #endregion

        #region Blocks
        readonly IMyProgrammableBlock pb;
        readonly IMyRemoteControl remotePilot;
        #endregion

        readonly ArrivalData arrivalData = new ArrivalData();

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
        }

        public Program()
        {
            LoadFromStorage();

            pb = GetBlockWithName<IMyProgrammableBlock>(shipProgrammableBlock);
            if (pb == null)
            {
                Echo($"Programmable Block {shipProgrammableBlock} no encontrado.");
                return;
            }

            remotePilot = GetBlockWithName<IMyRemoteControl>(shipRemoteControlPilot);
            if (remotePilot == null)
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
                if (argument.StartsWith("STOP"))
                {
                    DoStop();
                    return;
                }
                if (argument.StartsWith("RESET"))
                {
                    DoReset();
                    return;
                }

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
            arrivalData.Clear();
            SaveToStorage();

            var parts = message.Split('¬');
            if (parts.Length != 2)
            {
                return;
            }

            arrivalData.Initialize(Utils.StrToVector(parts[0]), parts[1]);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;  // Comenzar a comprobar la llegada
            SaveToStorage();
        }

        void DoStop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;  // Detener comprobaciones
            arrivalData.Clear();
            SaveToStorage();
        }
        void DoReset()
        {
            arrivalData.Clear();
            Runtime.UpdateFrequency = UpdateFrequency.None;  // Detener comprobaciones
            SaveToStorage();
        }
        void DoArrival()
        {
            if (!arrivalData.HasPosition)
            {
                Echo("Posición objetivo no definida.");
                return;
            }

            double distance = Vector3D.Distance(remotePilot.GetPosition(), arrivalData.TargetPosition);
            if (distance <= arrivalThreshold)
            {
                Echo("Posición alcanzada.");

                if (!string.IsNullOrWhiteSpace(arrivalData.ArrivalMessage))
                {
                    pb.TryRun(arrivalData.ArrivalMessage);
                    Echo($"Ejecutado {arrivalData.ArrivalMessage}");
                }

                DoStop();

                return;
            }

            Echo($"Distancia a destino: {distance:F2}m.");
        }

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            Runtime.UpdateFrequency = (UpdateFrequency)Utils.ReadInt(storageLines, "UpdateFrequency");
            arrivalData.LoadFromStorage(storageLines);
        }
        void SaveToStorage()
        {
            Storage = $"UpdateFrequency={(int)Runtime.UpdateFrequency}{Environment.NewLine}" + arrivalData.SaveToStorage();
        }
    }
}
