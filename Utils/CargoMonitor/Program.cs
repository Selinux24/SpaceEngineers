using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const char AttributeSep = '=';

        readonly Config config;

        readonly List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>();
        readonly IMyTimerBlock timerOn;
        readonly IMyTimerBlock timerOff;

        double lastCapacity = 0;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData = Config.GetDefault();

                Echo("CustomData not set.");
                return;
            }

            config = new Config(Me.CustomData);
            if (!config.IsValid())
            {
                Echo(config.GetErrors());
                return;
            }

            Echo($"Looking for '{config.CargoContainerName}'");
            Echo($"Using All Grids: {config.UseAllGrids}");

            cargoContainers = GetBlocksOfType<IMyCargoContainer>(config.CargoContainerName, config.UseAllGrids);
            if (cargoContainers.Count == 0)
            {
                Echo("Cargo Containers Not Found.");
                return;
            }

            timerOn = GetBlockWithName<IMyTimerBlock>(config.TimerOn, config.UseAllGrids);
            if (timerOn == null)
            {
                Echo($"Timer '{config.TimerOn}' not found.");
                return;
            }

            timerOff = GetBlockWithName<IMyTimerBlock>(config.TimerOff, config.UseAllGrids);
            if (timerOff == null)
            {
                Echo($"Timer '{config.TimerOff}' not found.");
                return;
            }

            lastCapacity = CalculateCargoPercentage();

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            ParseTerminalMessage(argument);

            var capacity = CalculateCargoPercentage();

            if (capacity >= config.CargoUpperLimit)
            {
                if (lastCapacity < config.CargoUpperLimit)
                {
                    Echo($"Capacity {capacity:P1} >= {config.CargoUpperLimit:P1}. Activating '{config.TimerOff}'");
                    timerOff.StartCountdown();
                }
            }

            if (capacity < config.CargoLowerLimit)
            {
                if (lastCapacity >= config.CargoLowerLimit)
                {
                    Echo($"Capacity {capacity:P1} < {config.CargoLowerLimit:P1}. Activating '{config.TimerOn}'");
                    timerOn.StartCountdown();
                }
            }

            lastCapacity = capacity;

            Echo($"Containers found: {cargoContainers.Count}");
            Echo($"Current capacity: {capacity:P1}");
            Echo($"Last capacity: {lastCapacity:P1}");
        }
        void ParseTerminalMessage(string argument)
        {
            if (argument == "START_BELLOW") lastCapacity = 0;
            if (argument == "START_ABOVE") lastCapacity = 1;
        }

        List<T> GetBlocksOfType<T>(string name, bool useAllGrids) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            if (useAllGrids)
                GridTerminalSystem.GetBlocksOfType(blocks, b => b.CustomName.Contains(name));
            else
                GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks;
        }
        T GetBlockWithName<T>(string name, bool useAllGrids) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            if (useAllGrids)
                GridTerminalSystem.GetBlocksOfType(blocks, b => b.CustomName.Contains(name));
            else
                GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks.FirstOrDefault();
        }

        double CalculateCargoPercentage()
        {
            if (cargoContainers.Count == 0)
            {
                return 0;
            }

            double max = 0;
            double curr = 0;
            foreach (var cargo in cargoContainers)
            {
                var inv = cargo.GetInventory();
                max += (double)inv.MaxVolume;
                curr += (double)inv.CurrentVolume;
            }

            return curr / max;
        }

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            lastCapacity = ReadDouble(storageLines, "lastCapacity", 0);
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"lastCapacity={lastCapacity}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
        static string ReadString(string[] lines, string name, string defaultValue = "")
        {
            string cmdToken = $"{name}{AttributeSep}";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return value;
        }
        static double ReadDouble(string[] lines, string name, double defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return double.Parse(value);
        }
    }
}
