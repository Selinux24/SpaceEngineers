using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string WildcardLCDs = "[EMITTER]";

        readonly IMyLaserAntenna antenna;
        readonly IMyTimerBlock timerSend;
        readonly string gpsReceptor;

        bool sending = false;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "LaserAntenna=name\n" +
                    "TimerSend=name\n" +
                    "GPSReceptor=GPS:Name:x:y:z:\n";

                Echo("CustomData not set.");
                return;
            }

            string laserAntennaName = ReadConfig(Me.CustomData, "LaserAntenna");
            if (string.IsNullOrWhiteSpace(laserAntennaName))
            {
                Echo("LaserAntenna name not set.");
                return;
            }
            string timerSendName = ReadConfig(Me.CustomData, "TimerSend");
            if (string.IsNullOrWhiteSpace(timerSendName))
            {
                Echo("TimerSend name not set.");
                return;
            }
            gpsReceptor = ReadConfig(Me.CustomData, "GPSReceptor");
            if (string.IsNullOrWhiteSpace(gpsReceptor))
            {
                Echo("GPS receptor coordinates not set.");
                return;
            }

            antenna = GetBlockWithName<IMyLaserAntenna>(laserAntennaName);
            if (antenna == null)
            {
                Echo($"Antenna {laserAntennaName} not found.");
                return;
            }
            timerSend = GetBlockWithName<IMyTimerBlock>(timerSendName);
            if (timerSend == null)
            {
                Echo($"Timer {timerSendName} not found.");
                return;
            }

            Echo("Working!");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "SEND")
            {
                sending = true;
                Runtime.UpdateFrequency = UpdateFrequency.Update100;

                return;
            }

            if (!sending)
            {
                return;
            }

            var infoLCDs = GetBlocksOfType<IMyTextPanel>(WildcardLCDs);

            if (antenna.Status != MyLaserAntennaStatus.Connected)
            {
                WriteInfoLCDs(infoLCDs, $"{antenna.Status} Connecting to GPS...");
                if (antenna.Status != MyLaserAntennaStatus.SearchingTargetForAntenna && antenna.Status != MyLaserAntennaStatus.RotatingToTarget && antenna.Status != MyLaserAntennaStatus.Connecting)
                {
                    antenna.Enabled = true;
                    antenna.SetTargetCoords(gpsReceptor);
                    antenna.Connect();
                }
                return;
            }

            timerSend.StartCountdown();
            sending = false;
            Runtime.UpdateFrequency = UpdateFrequency.None;
            WriteInfoLCDs(infoLCDs, "Send completed.");
        }

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks.FirstOrDefault();
        }
        List<T> GetBlocksOfType<T>(string filter) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(filter));
            return blocks;
        }
        void WriteInfoLCDs(List<IMyTextPanel> lcds, string text, bool append = true)
        {
            Echo(text);

            foreach (var lcd in lcds)
            {
                lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                lcd.WriteText(text + Environment.NewLine, append);
            }
        }

        static string ReadConfig(string customData, string name)
        {
            string[] lines = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }
    }
}
