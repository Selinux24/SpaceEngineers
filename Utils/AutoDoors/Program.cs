using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        readonly bool enableAutoDoorCloser = true;
        readonly bool enableAirlockSystem = true;
        readonly bool ignoreAllHangarDoors = true;
        readonly bool ignoreDoorsOnOtherGrids = true;

        //Door open duration (in seconds) 
        readonly double regularDoorOpenDuration = 2.5;
        readonly double hangarDoorOpenDuration = 15;

        //Door exclusion string 
        readonly string doorExcludeString = "Excluded";

        //Airlock Light Settings 
        Color alarmColor = new Color(255, 40, 40); //color of alarm light         
        Color regularColor = new Color(80, 160, 255); //color of regular light 
        readonly float alarmBlinkLength = 50f;  //alarm blink length in % 
        readonly float regularBlinkLength = 100f; //regular blink length in % 
        readonly float blinkInterval = .8f; // blink interval in seconds 

        const double updateTime = 1.0 / 6.0;
        const double refreshTime = 10;
        const double runtimeToRealTime = 1.0 / 0.96;
        double currentRefreshTime = 141;
        double currentTime = 141;
        bool isSetup = false;

        readonly HashSet<string> airlockNames = new HashSet<string>();
        readonly List<IMyDoor> airlockDoors = new List<IMyDoor>();
        readonly List<IMySoundBlock> allSounds = new List<IMySoundBlock>();
        readonly List<IMyLightingBlock> allLights = new List<IMyLightingBlock>();
        readonly List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();

        readonly List<Airlock> airlockList = new List<Airlock>();
        readonly List<AutoDoor> autoDoors = new List<AutoDoor>();
        readonly List<IMyDoor> autoDoorsCached = new List<IMyDoor>();

        readonly List<IMyMechanicalConnectionBlock> allMechanical = new List<IMyMechanicalConnectionBlock>();
        readonly HashSet<IMyCubeGrid> allowedGrids = new HashSet<IMyCubeGrid>();
        bool isFinished = true;

        Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateType)
        {
            var lastRuntime = runtimeToRealTime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0);
            currentRefreshTime += lastRuntime;
            currentTime += lastRuntime;

            if (!isSetup || currentRefreshTime >= refreshTime)
            {
                isSetup = GrabBlocks();
                currentRefreshTime = 0;
            }

            if (!isSetup) return;

            if (currentTime < updateTime) return;

            Echo("Auto Door and Airlock System\n");
            Echo($"Next refresh in {Math.Round(Math.Max(refreshTime - currentRefreshTime, 0))} seconds\n");

            try
            {
                if (enableAutoDoorCloser)
                {
                    AutoDoors(currentTime); //controls auto door closing
                }

                if (enableAirlockSystem)
                {
                    Airlocks(); //controls airlock system
                }
            }
            catch
            {
                Echo("Somethin dun broke");
                isSetup = false; //redo setup
            }

            Echo($"Current instructions: {Runtime.CurrentInstructionCount}\nMax instructions: {Runtime.MaxInstructionCount}");

            currentTime = 0;
        }

        bool GrabBlocks()
        {
            if (ignoreDoorsOnOtherGrids)
            {
                GridTerminalSystem.GetBlocksOfType(allBlocks, x => x.CubeGrid == Me.CubeGrid);
            }
            else
            {
                GetAllowedGrids(Me, 1000);

                if (!isFinished) return false; //break setup until accepted grids are done parsing

                GridTerminalSystem.GetBlocksOfType(allBlocks, x => allowedGrids.Contains(x.CubeGrid));
            }

            airlockDoors.Clear();
            allSounds.Clear();
            allLights.Clear();

            autoDoors.RemoveAll(x => x.Door.CustomName.ToLower().Contains(doorExcludeString.ToLower()));

            //Fetch all blocks that the code needs
            foreach (var block in allBlocks)
            {
                if (block is IMyDoor)
                {
                    if (block.CustomName.ToLower().Contains("airlock"))
                    {
                        airlockDoors.Add(block as IMyDoor);
                    }

                    if (ShouldAddAutoDoor(block) && !autoDoorsCached.Contains(block as IMyDoor))
                    {
                        autoDoors.Add(new AutoDoor(block as IMyDoor, regularDoorOpenDuration, hangarDoorOpenDuration));
                    }
                }
                else if (block is IMyLightingBlock && block.CustomName.ToLower().Contains("airlock light"))
                {
                    allLights.Add(block as IMyLightingBlock);
                }
                else if (block is IMySoundBlock && block.CustomName.ToLower().Contains("airlock sound"))
                {
                    allSounds.Add(block as IMySoundBlock);
                }
            }

            //Fetch all airlock door names
            airlockNames.Clear();
            foreach (var thisDoor in airlockDoors)
            {
                if (thisDoor.CustomName.ToLower().Contains("airlock interior"))
                {
                    //Remove airlock tag
                    string thisName = thisDoor.CustomName.ToLower().Replace("airlock interior", "");

                    //Remove door exclusion string
                    thisName = thisName.Replace($"[{doorExcludeString.ToLower()}]", "").Replace(doorExcludeString.ToLower(), "");
                    //Remove all spaces
                    thisName = thisName.Replace(" ", "");

                    //Adds name to string list
                    airlockNames.Add(thisName);
                }
            }

            //Evaluate each unique airlock name and get parts associated with it
            foreach (var hashValue in airlockNames)
            {
                bool dupe = false;
                foreach (var airlock in airlockList)
                {
                    if (airlock.Name.Equals(hashValue))
                    {
                        airlock.Update(airlockDoors, allLights, allSounds);
                        dupe = true;
                        break;
                    }
                }

                if (!dupe)
                {
                    airlockList.Add(new Airlock(hashValue, airlockDoors, allLights, allSounds, alarmColor, regularColor, alarmBlinkLength, regularBlinkLength, blinkInterval));
                }
            }

            autoDoorsCached.Clear();
            foreach (var autoDoor in autoDoors)
            {
                autoDoorsCached.Add(autoDoor.Door);
            }

            return true;
        }
        void GetAllowedGrids(IMyTerminalBlock reference, int instructionLimit)
        {
            if (isFinished)
            {
                allowedGrids.Clear();
                allowedGrids.Add(reference.CubeGrid);
            }

            GridTerminalSystem.GetBlocksOfType(allMechanical, x => x.TopGrid != null);

            bool foundStuff = true;
            while (foundStuff)
            {
                foundStuff = false;

                for (int i = allMechanical.Count - 1; i >= 0; i--)
                {
                    var block = allMechanical[i];
                    if (allowedGrids.Contains(block.CubeGrid))
                    {
                        allowedGrids.Add(block.TopGrid);
                        allMechanical.RemoveAt(i);
                        foundStuff = true;
                    }
                    else if (allowedGrids.Contains(block.TopGrid))
                    {
                        allowedGrids.Add(block.CubeGrid);
                        allMechanical.RemoveAt(i);
                        foundStuff = true;
                    }
                }

                if (Runtime.CurrentInstructionCount >= instructionLimit)
                {
                    Echo("Instruction limit reached\nawaiting next run");
                    isFinished = false;
                    return;
                }
            }

            isFinished = true;
        }
        bool ShouldAddAutoDoor(IMyTerminalBlock block)
        {
            if (ignoreAllHangarDoors && block is IMyAirtightHangarDoor) return false;
            
            if (block.CustomName.ToLower().Contains(doorExcludeString.ToLower())) return false;
            
            return true;
        }

        bool CheckInstructions(double proportion = 0.5)
        {
            return Runtime.CurrentInstructionCount >= Runtime.MaxInstructionCount * proportion;
        }

        void AutoDoors(double timeElapsed)
        {
            foreach (var thisDoor in autoDoors)
            {
                if (CheckInstructions())
                {
                    Echo("Instruction limit hit\nAborting...");
                    return;
                }

                thisDoor.AutoClose(timeElapsed);
            }

            Echo($"\n===Automatic Doors===\nManaged Doors: {autoDoors.Count}");
        }

        void Airlocks()
        {
            Echo("\n===Airlock Systems===");

            if (airlockList.Count == 0)
            {
                Echo("No airlock groups found");
                return;
            }

            //Iterate through our airlock groups
            Echo($"Airlock count: {airlockList.Count}");
            foreach (var airlock in airlockList)
            {
                if (CheckInstructions())
                {
                    Echo("Instruction limit hit\nAborting...");
                    return;
                }

                airlock.DoLogic();
                Echo($"---------------------------------------------\nAirlock group '{airlock.Name}' found\n{airlock.Info}");
            }
        }
    }
}
