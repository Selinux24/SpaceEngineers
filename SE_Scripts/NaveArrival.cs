using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRageMath;

namespace NaveArrival
{
    partial class Program : MyGridProgram
    {
        const string shipRemoteControlPilot = "Remote Control Pilot";
        const string shipTimerArrival = "Automaton Timer Block Arrival";
        const double arrivalThreshold = 200.0;

        IMyRemoteControl remote;
        IMyTimerBlock arrivalTimer;

        Vector3D targetPosition = Vector3D.Zero;

        public Program()
        {
            remote = GridTerminalSystem.GetBlockWithName(shipRemoteControlPilot) as IMyRemoteControl;
            if (remote == null)
            {
                Echo($"RemoteControl {shipRemoteControlPilot} no encontrado.");
                return;
            }

            arrivalTimer = GridTerminalSystem.GetBlockWithName(shipTimerArrival) as IMyTimerBlock;
            if (arrivalTimer == null)
            {
                Echo($"Timer {shipTimerArrival} no encontrado.");
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.None;
            Echo("Working");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrWhiteSpace(argument) && argument.StartsWith("GPS:"))
            {
                targetPosition = ParseGPS(argument);
                Runtime.UpdateFrequency = UpdateFrequency.Update10;  // Empieza a comprobar
                return;
            }

            if ((updateSource & UpdateType.Update10) != 0)
            {
                if (targetPosition == Vector3D.Zero)
                {
                    Echo("Posición objetivo no definida.");
                    return;
                }

                Vector3D currentPos = remote.GetPosition();
                double distance = Vector3D.Distance(currentPos, targetPosition);

                Echo($"Distancia a destino: {distance:F2}");

                if (distance <= arrivalThreshold)
                {
                    arrivalTimer.ApplyAction("Start");
                    Echo("¡Llegada detectada! Timer activado.");
                    Runtime.UpdateFrequency = UpdateFrequency.None;  // Detener comprobaciones
                }
            }
        }

        static Vector3D ParseGPS(string gps)
        {
            var parts = gps.Split(':');
            if (parts.Length >= 5)
            {
                double x = double.Parse(parts[2]);
                double y = double.Parse(parts[3]);
                double z = double.Parse(parts[4]);
                return new Vector3D(x, y, z);
            }
            return Vector3D.Zero;
        }
    }
}
