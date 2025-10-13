using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    public class Airlock
    {
        const string soundBlockPlayingString = "%Playing sound...%";

        readonly List<IMyDoor> airlockInteriorList = new List<IMyDoor>();
        readonly List<IMyDoor> airlockExteriorList = new List<IMyDoor>();
        readonly List<IMyLightingBlock> airlockLightList = new List<IMyLightingBlock>();
        readonly List<IMySoundBlock> airlockSoundList = new List<IMySoundBlock>();
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
            List<IMyLightingBlock> allLights,
            List<IMySoundBlock> allSounds,
            Color alarmColor,
            Color regularColor,
            float alarmBlinkLength,
            float regularBlinkLength,
            float blinkInterval)
        {
            Name = airlockName;
            this.alarmColor = alarmColor;
            this.regularColor = regularColor;
            this.alarmBlinkLength = alarmBlinkLength;
            this.regularBlinkLength = regularBlinkLength;
            this.blinkInterval = blinkInterval;

            GetBlocks(Name, airlockDoors, allLights, allSounds);
            Info = $" Interior Doors: {airlockInteriorList.Count}\n Exterior Doors: {airlockExteriorList.Count}\n Lights: {airlockLightList.Count}\n Sound Blocks: {airlockSoundList.Count}";
        }
        private void GetBlocks(string airlockName, List<IMyDoor> airlockDoors, List<IMyLightingBlock> allLights, List<IMySoundBlock> allSounds)
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

                if (name.Contains(Program.AirlockInteriorTag))
                {
                    airlockInteriorList.Add(door);
                }
                else if (name.Contains(Program.AirlockExteriorTag))
                {
                    airlockExteriorList.Add(door);
                }
            }

            //Sort through all lights 
            airlockLightList.Clear();
            foreach (var light in allLights)
            {
                if (light.CustomName.Replace(" ", "").ToLower().Contains(airlockName))
                {
                    airlockLightList.Add(light);
                }
            }

            //Sort through all lights 
            airlockSoundList.Clear();
            foreach (var sound in allSounds)
            {
                if (sound.CustomName.Replace(" ", "").ToLower().Contains(airlockName))
                {
                    airlockSoundList.Add(sound);
                }
            }

            Info = $" Interior Doors: {airlockInteriorList.Count}\n Exterior Doors: {airlockExteriorList.Count}\n Lights: {airlockLightList.Count}\n Sound Blocks: {airlockSoundList.Count}";
        }

        public void DoLogic()
        {
            if (airlockInteriorList.Count == 0 || airlockExteriorList.Count == 0) return;

            //We assume the airlocks are closed until proven otherwise

            //Door Interior Check
            bool isInteriorClosed = true;
            foreach (var airlockInterior in airlockInteriorList)
            {
                if (airlockInterior.OpenRatio > 0)
                {
                    Lock(airlockExteriorList);
                    isInteriorClosed = false;
                    break;
                    //If any doors yield false, bool will persist until comparison
                }
            }

            //Door Exterior Check
            bool isExteriorClosed = true;
            foreach (var airlockExterior in airlockExteriorList)
            {
                if (airlockExterior.OpenRatio > 0)
                {
                    Lock(airlockInteriorList);
                    isExteriorClosed = false;
                    break;
                }
            }

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

            //If all Interior doors closed 
            if (isInteriorClosed) Unlock(airlockExteriorList);

            //If all Exterior doors closed     
            if (isExteriorClosed) Unlock(airlockInteriorList);
        }

        private void Lock(List<IMyDoor> doorList)
        {
            foreach (var lockDoor in doorList)
            {
                //If door is open, then close
                if (lockDoor.OpenRatio > 0)
                {
                    lockDoor.CloseDoor();
                }

                //If door is fully closed, then lock
                if (lockDoor.OpenRatio == 0 && lockDoor.Enabled)
                {
                    lockDoor.Enabled = false;
                }
            }
        }

        private void Unlock(List<IMyDoor> doorList)
        {
            foreach (var unlockDoor in doorList)
            {
                unlockDoor.Enabled = true;
            }
        }

        private void PlaySound(bool shouldPlay, List<IMySoundBlock> soundList)
        {
            foreach (var block in soundList)
            {
                if (!shouldPlay)
                {
                    block.Stop();
                    block.CustomData = block.CustomData.Replace(soundBlockPlayingString, "");
                    continue;
                }

                if (!block.CustomData.Contains(soundBlockPlayingString))
                {
                    block.Play();
                    block.LoopPeriod = 100f;
                    block.CustomData += soundBlockPlayingString;
                }
            }
        }

        private void LightColorChanger(bool alarm, List<IMyLightingBlock> listLights)
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
                thisLight.Color = lightColor;
                thisLight.BlinkLength = lightBlinkLength;
                thisLight.BlinkIntervalSeconds = blinkInterval;
            }
        }
    }
}
