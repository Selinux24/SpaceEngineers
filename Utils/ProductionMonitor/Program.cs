using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "1.0";
        const char AttributeSep = '=';

        readonly Config config;

        readonly List<IMyAssembler> assemblers = new List<IMyAssembler>();
        readonly IMyTimerBlock timerOn;
        readonly IMyTimerBlock timerOff;

        bool lastState = false;

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

            Echo($"Looking for '{config.AssemblerName}'");
            Echo($"Using All Grids: {config.UseAllGrids}");

            assemblers = GetBlocksOfType<IMyAssembler>(config.AssemblerName, config.UseAllGrids);
            if (assemblers.Count == 0)
            {
                Echo("Assemblers Not Found.");
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

            lastState = CalculateAssemblersState();

            LoadFromStorage();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            SaveToStorage();
        }

        public void Main(string argument)
        {
            Echo($"Production Monitor v{Version}");

            ParseTerminalMessage(argument);

            bool state = CalculateAssemblersState();
            if (state != lastState)
            {
                if (state)
                {
                    Echo($"Assemblers with queued production. Activating '{config.TimerOn}'");
                    timerOn.StartCountdown();
                }
                else
                {
                    Echo($"Assemblers free. Activating '{config.TimerOff}'");
                    timerOff.StartCountdown();
                }

                lastState = state;
            }

            Echo($"Assemblers found: {assemblers.Count}");
            Echo($"{(state ? "Active" : "Inactive")}");
        }
        void ParseTerminalMessage(string argument)
        {
            if (argument == "START_INACTIVE") lastState = false;
            if (argument == "START_ACTIVE") lastState = true;
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

        bool CalculateAssemblersState()
        {
            if (assemblers.Count == 0)
            {
                return false;
            }

            foreach (var assembler in assemblers)
            {
                if (!assembler.IsQueueEmpty)
                {
                    return true;
                }
            }
            return false;
        }

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            lastState = ReadInt(storageLines, "lastState", 0) != 0;
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"lastState={(lastState?1:0)}",
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
        static int ReadInt(string[] lines, string name, int defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return int.Parse(value);
        }
    }
}
