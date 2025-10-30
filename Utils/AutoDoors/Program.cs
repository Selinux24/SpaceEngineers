using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string Version = "2.0";
        internal const string AirlockDoorMask = "airlock";
        internal const string AirlockInteriorDoorMask = "airlock interior";
        internal const string AirlockLightMask = "airlock light";
        internal const string AirlockSoundMask = "airlock sound";
        internal const string AirlockInteriorTag = "interior";
        internal const string AirlockExteriorTag = "exterior";
        internal const string DoorExcludeMask = "excluded";
        const double UpdateTime = 1.0 / 6.0;
        const double RuntimeToRealTime = 1.0 / 0.96;

        readonly bool enableAutoDoorCloser = true;
        readonly bool enableAirlockSystem = true;
        readonly bool ignoreAllHangarDoors = true;

        //Door open duration (in seconds) 
        readonly double regularDoorOpenDuration = 2.5;
        readonly double hangarDoorOpenDuration = 15;

        //Airlock Light Settings 
        readonly Color alarmColor = new Color(255, 40, 40); //color of alarm light         
        readonly Color regularColor = new Color(80, 160, 255); //color of regular light 
        readonly float alarmBlinkLength = 50f;  //alarm blink length in % 
        readonly float regularBlinkLength = 100f; //regular blink length in % 
        readonly float blinkInterval = .8f; // blink interval in seconds 

        double currentTime = 141;

        readonly List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
        readonly HashSet<string> airlockNames = new HashSet<string>();
        readonly List<IMyDoor> airlockDoors = new List<IMyDoor>();
        readonly List<IMySoundBlock> allSounds = new List<IMySoundBlock>();
        readonly List<IMyLightingBlock> allLights = new List<IMyLightingBlock>();

        readonly List<Airlock> airlockList = new List<Airlock>();
        readonly List<AutoDoor> autoDoors = new List<AutoDoor>();
        readonly List<AutoDoor> autoDoorsCached = new List<AutoDoor>();

        Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            if (!GrabBlocks())
            {
                Echo("Error during setup");
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
        bool GrabBlocks()
        {
            allBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType(allBlocks, x => x.CubeGrid == Me.CubeGrid);

            airlockDoors.Clear();
            allSounds.Clear();
            allLights.Clear();
            autoDoors.Clear();

            foreach (var block in allBlocks)
            {
                var door = block as IMyDoor;
                if (door != null)
                {
                    if (block.CustomName.ToLower().Contains(AirlockDoorMask))
                    {
                        airlockDoors.Add(door);
                    }

                    if (ShouldAddAutoDoor(door))
                    {
                        double autoCloseTime = (door is IMyAirtightHangarDoor) ? hangarDoorOpenDuration : regularDoorOpenDuration;

                        autoDoors.Add(new AutoDoor(door, autoCloseTime));
                    }
                }

                var light = block as IMyLightingBlock;
                if (light != null && light.CustomName.ToLower().Contains(AirlockLightMask))
                {
                    allLights.Add(light);
                }

                var sound = block as IMySoundBlock;
                if (sound != null && sound.CustomName.ToLower().Contains(AirlockSoundMask))
                {
                    allSounds.Add(sound);
                }
            }

            //Fetch all airlock door names
            airlockNames.Clear();
            foreach (var door in airlockDoors)
            {
                if (!door.CustomName.ToLower().Contains(AirlockInteriorDoorMask))
                {
                    continue;
                }

                //Remove airlock tag, door exclusion string and all spaces
                string name = door.CustomName.ToLower()
                    .Replace(AirlockInteriorDoorMask, "")
                    .Replace($"[{DoorExcludeMask}]", "")
                    .Replace(DoorExcludeMask, "")
                    .Replace(" ", "");

                //Adds name to string list
                airlockNames.Add(name);
            }

            //Evaluate each unique airlock name and get parts associated with it
            airlockList.Clear();
            foreach (var v in airlockNames)
            {
                bool dupe = false;
                foreach (var airlock in airlockList)
                {
                    if (airlock.Name.Equals(v))
                    {
                        dupe = true;
                        break;
                    }
                }

                if (!dupe)
                {
                    airlockList.Add(new Airlock(v, airlockDoors, hangarDoorOpenDuration, regularDoorOpenDuration, allLights, alarmColor, regularColor, alarmBlinkLength, regularBlinkLength, blinkInterval, allSounds));
                }
            }

            autoDoorsCached.Clear();
            foreach (var autoDoor in autoDoors)
            {
                autoDoorsCached.Add(autoDoor);
            }

            return true;
        }

        public void Main(string argument, UpdateType updateType)
        {
            Echo($"Auto Door and Airlock System v{Version}\n");

            if (Skip()) return;

            AutoDoors();

            Airlocks();

            currentTime = 0;

            Echo($"Instructions: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
        }
        bool Skip()
        {
            var lastRuntime = RuntimeToRealTime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0);
            currentTime += lastRuntime;
            return currentTime < UpdateTime;
        }
        bool ShouldAddAutoDoor(IMyDoor door)
        {
            if (ignoreAllHangarDoors && door is IMyAirtightHangarDoor) return false;

            if (door.CustomName.ToLower().Contains(DoorExcludeMask)) return false;

            return true;
        }

        bool CheckInstructions(double proportion = 0.5)
        {
            return Runtime.CurrentInstructionCount >= Runtime.MaxInstructionCount * proportion;
        }

        void AutoDoors()
        {
            if (!enableAutoDoorCloser) return;

            foreach (var door in autoDoors)
            {
                if (CheckInstructions())
                {
                    Echo("Instruction limit hit\nAborting...");
                    return;
                }

                door.Update(currentTime);
            }

            Echo($"\n===Automatic Doors===\nManaged Doors: {autoDoors.Count}");
        }

        void Airlocks()
        {
            if (!enableAirlockSystem) return;
            if (airlockList.Count == 0) return;

            Echo("\n===Airlock Systems===");
            Echo($"Airlock count: {airlockList.Count}");

            foreach (var airlock in airlockList)
            {
                if (CheckInstructions())
                {
                    Echo("Instruction limit hit\nAborting...");
                    return;
                }

                airlock.Update(currentTime);
                Echo($"---------------------------------------------\nAirlock group '{airlock.Name}' found\n{airlock.Info}");
            }
        }
    }
}
