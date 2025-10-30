using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Airlock
    {
        const string soundBlockPlayingString = "%Playing sound...%";

        readonly List<AutoDoor> airlockInteriorList = new List<AutoDoor>();
        readonly List<AutoDoor> airlockExteriorList = new List<AutoDoor>();
        readonly List<CBlock<IMyLightingBlock>> airlockLightList = new List<CBlock<IMyLightingBlock>>();
        readonly List<CBlock<IMySoundBlock>> airlockSoundList = new List<CBlock<IMySoundBlock>>();
        readonly Color alarmColor = new Color(255, 40, 40);
        readonly Color regularColor = new Color(80, 160, 255);
        readonly float alarmBlinkLength = 50f;
        readonly float regularBlinkLength = 100f;
        readonly float blinkInterval = .8f;

        public string Name { get; private set; }
        public string Info { get; private set; }

        public Airlock(
            string airlockName,
            List<IMyDoor> airlockDoors,
            double hangarDoorOpenDuration,
            double regularDoorOpenDuration,
            List<IMyLightingBlock> allLights,
            Color alarmColor,
            Color regularColor,
            float alarmBlinkLength,
            float regularBlinkLength,
            float blinkInterval,
            List<IMySoundBlock> allSounds)
        {
            Name = airlockName;
            this.alarmColor = alarmColor;
            this.regularColor = regularColor;
            this.alarmBlinkLength = alarmBlinkLength;
            this.regularBlinkLength = regularBlinkLength;
            this.blinkInterval = blinkInterval;

            GetBlocks(airlockName, airlockDoors, hangarDoorOpenDuration, regularDoorOpenDuration, allLights, allSounds);
            Info = $" Interior Doors: {airlockInteriorList.Count}\n Exterior Doors: {airlockExteriorList.Count}\n Lights: {airlockLightList.Count}\n Sound Blocks: {airlockSoundList.Count}";
        }
        void GetBlocks(
            string airlockName,
            List<IMyDoor> airlockDoors,
            double hangarDoorOpenDuration,
            double regularDoorOpenDuration,
            List<IMyLightingBlock> allLights,
            List<IMySoundBlock> allSounds)
        {
            //Sort through all doors
            airlockInteriorList.Clear();
            airlockExteriorList.Clear();
            foreach (var door in airlockDoors)
            {
                string name = door.CustomName.Replace(" ", "").ToLower();
                if (!name.Contains(airlockName))
                {
                    continue;
                }

                double autoCloseTime = (door is IMyAirtightHangarDoor) ? hangarDoorOpenDuration : regularDoorOpenDuration;

                if (name.Contains(Program.AirlockInteriorTag))
                {
                    airlockInteriorList.Add(new AutoDoor(door, autoCloseTime));
                }
                else if (name.Contains(Program.AirlockExteriorTag))
                {
                    airlockExteriorList.Add(new AutoDoor(door, autoCloseTime));
                }
            }

            //Sort through all lights 
            airlockLightList.Clear();
            foreach (var light in allLights)
            {
                if (light.CustomName.Replace(" ", "").ToLower().Contains(airlockName))
                {
                    airlockLightList.Add(new CBlock<IMyLightingBlock>(light));
                }
            }

            //Sort through all lights 
            airlockSoundList.Clear();
            foreach (var sound in allSounds)
            {
                if (sound.CustomName.Replace(" ", "").ToLower().Contains(airlockName))
                {
                    airlockSoundList.Add(new CBlock<IMySoundBlock>(sound));
                }
            }

            Info = $" Interior Doors: {airlockInteriorList.Count}\n Exterior Doors: {airlockExteriorList.Count}\n Lights: {airlockLightList.Count}\n Sound Blocks: {airlockSoundList.Count}";
        }

        public void Update(double time)
        {
            if (airlockInteriorList.Count == 0 || airlockExteriorList.Count == 0) return;

            //We assume the airlocks are closed until proven otherwise

            //Door Interior Check
            bool isInteriorClosed = UpdateDoor(airlockInteriorList, time);

            //Door Exterior Check
            bool isExteriorClosed = UpdateDoor(airlockExteriorList, time);

            if (isInteriorClosed) Unlock(airlockExteriorList);
            else Lock(airlockExteriorList);

            if (isExteriorClosed) Unlock(airlockInteriorList);
            else Lock(airlockInteriorList);

            //If all Interior & Exterior doors closed 
            if (isInteriorClosed && isExteriorClosed)
            {
                LightColorChanger(false, airlockLightList);
                PlaySound(false, airlockSoundList);
            }
            else
            {
                LightColorChanger(true, airlockLightList);
                PlaySound(true, airlockSoundList);
            }
        }
        bool UpdateDoor(List<AutoDoor> doorList, double time)
        {
            bool allClosed = true;

            foreach (var door in doorList)
            {
                door.Update(time);

                if (door.IsOpen()) allClosed = false;
            }

            return allClosed;
        }
        void Lock(List<AutoDoor> doorList)
        {
            foreach (var lockDoor in doorList)
            {
                //If door is fully closed, then lock
                if (lockDoor.IsFulllyClosed() && lockDoor.IsEnabled())
                {
                    lockDoor.Disable();
                }
            }
        }
        void Unlock(List<AutoDoor> doorList)
        {
            foreach (var unlockDoor in doorList)
            {
                unlockDoor.Enable();
            }
        }
        void PlaySound(bool shouldPlay, List<CBlock<IMySoundBlock>> soundList)
        {
            foreach (var block in soundList)
            {
                if (!shouldPlay)
                {
                    block.Block.Stop();
                    block.Block.CustomData = block.Block.CustomData.Replace(soundBlockPlayingString, "");
                    continue;
                }

                if (!block.Block.CustomData.Contains(soundBlockPlayingString))
                {
                    block.Block.Play();
                    block.Block.LoopPeriod = 100f;
                    block.Block.CustomData += soundBlockPlayingString;
                }
            }
        }
        void LightColorChanger(bool alarm, List<CBlock<IMyLightingBlock>> listLights)
        {
            //Applies our status colors to the airlock lights        
            Color lightColor;
            float lightBlinkLength;
            if (alarm)
            {
                lightColor = alarmColor;
                lightBlinkLength = alarmBlinkLength;
            }
            else
            {
                lightColor = regularColor;
                lightBlinkLength = regularBlinkLength;
            }

            foreach (var thisLight in listLights)
            {
                thisLight.Block.Color = lightColor;
                thisLight.Block.BlinkLength = lightBlinkLength;
                thisLight.Block.BlinkIntervalSeconds = blinkInterval;
            }
        }
    }
}
