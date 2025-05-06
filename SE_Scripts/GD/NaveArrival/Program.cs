using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace SE_Scripts.GD.NaveArrival
{
    partial class Program : MyGridProgram
    {
        const string shipProgrammableBlock = "HT Automaton Programmable Block Ship";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const double arrivalThreshold = 200.0;

        readonly IMyProgrammableBlock pb;
        readonly IMyRemoteControl remote;
        readonly ArrivalData arrivalData = new ArrivalData();

        class ArrivalData
        {
            public bool HasPosition = false;
            public Vector3D TargetPosition = Vector3D.Zero;
            public string ArrivalMessage = null;

            public void Initialize(Vector3D position, string arrivalMessage)
            {
                HasPosition = true;
                TargetPosition = position;
                ArrivalMessage = arrivalMessage;
            }
            public void Clear()
            {
                HasPosition = false;
                TargetPosition = Vector3D.Zero;
                ArrivalMessage = null;
            }

            public void LoadFromStorage(string[] storageLines)
            {
                if (storageLines.Length == 0)
                {
                    return;
                }

                TargetPosition = StrToVector(ReadString(storageLines, "TargetPosition"));
                HasPosition = ReadInt(storageLines, "HasPosition") == 1;
                ArrivalMessage = ReadString(storageLines, "ArrivalMessage");
            }
            public string SaveToStorage()
            {
                Dictionary<string, string> datos = new Dictionary<string, string>();

                datos["TargetPosition"] = VectorToStr(TargetPosition);
                datos["HasPosition"] = HasPosition ? "1" : "0";
                datos["ArrivalMessage"] = ArrivalMessage ?? "";

                var lineas = new List<string>();
                foreach (var kvp in datos)
                {
                    lineas.Add($"{kvp.Key}={kvp.Value}");
                }

                return string.Join(Environment.NewLine, lineas);
            }
        }

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
        static string VectorToStr(Vector3D v)
        {
            return $"{v.X}:{v.Y}:{v.Z}";
        }
        static string ReadString(string[] lines, string name, string defaultValue = null)
        {
            string cmdToken = $"{name}=";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return value;
        }
        static int ReadInt(string[] lines, string name, int defaultValue = 0)
        {
            string cmdToken = $"{name}=";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return int.Parse(value);
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

            arrivalData.Initialize(StrToVector(parts[0]), parts[1]);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;  // Comenzar a comprobar la llegada
            SaveToStorage();
        }

        void DoArrival()
        {
            if (!arrivalData.HasPosition)
            {
                Echo("Posición objetivo no definida.");
                return;
            }

            double distance = Vector3D.Distance(remote.GetPosition(), arrivalData.TargetPosition);
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

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            Runtime.UpdateFrequency = (UpdateFrequency)ReadInt(storageLines, "UpdateFrequency");
            arrivalData.LoadFromStorage(storageLines);
        }
        void SaveToStorage()
        {
            Storage = $"UpdateFrequency={(int)Runtime.UpdateFrequency}{Environment.NewLine}" + arrivalData.SaveToStorage();
        }
    }
}
