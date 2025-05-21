using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string LaserAntennaName = "BaseMiner1 Laser Antenna";
        const string TimerSendName = "BaseMiner1 Timer Laser Off";

        readonly IMyLaserAntenna antenna;

        string gpsReceptor;
        bool sending = false;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            antenna = GetBlockWithName<IMyLaserAntenna>(LaserAntennaName);
            if (antenna == null)
            {
                Echo($"Antenna {LaserAntennaName} not found.");
                return;
            }

            Echo("Working!");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "SEND")
            {
                if (!string.IsNullOrWhiteSpace(Me.CustomData))
                {
                    gpsReceptor = Me.CustomData;
                    sending = !string.IsNullOrWhiteSpace(gpsReceptor);
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                }

                return;
            }

            if (!sending)
            {
                return;
            }

            // Conectar si no está aún
            if (antenna.Status != MyLaserAntennaStatus.Connected)
            {
                Echo($"{antenna.Status} Connecting to GPS...");
                if (antenna.Status != MyLaserAntennaStatus.SearchingTargetForAntenna && antenna.Status != MyLaserAntennaStatus.RotatingToTarget && antenna.Status != MyLaserAntennaStatus.Connecting)
                {
                    antenna.Enabled = true;
                    antenna.SetTargetCoords(gpsReceptor);
                    antenna.Connect();
                }
                return;
            }

            ActivateTimer(TimerSendName);
            gpsReceptor = null;
            sending = false;
            Runtime.UpdateFrequency = UpdateFrequency.None;
            Echo("Send completed.");
        }

        void ActivateTimer(string nombre)
        {
            var timer = GetBlockWithName<IMyTimerBlock>(nombre);
            timer?.StartCountdown();
        }

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks.FirstOrDefault();
        }
    }
}
