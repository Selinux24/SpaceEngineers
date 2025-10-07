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
        Color alarmColor = new Color(255, 40, 40);
        Color regularColor = new Color(80, 160, 255);
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

        public void Update(List<IMyDoor> airlockDoors, List<IMyLightingBlock> allLights, List<IMySoundBlock> allSounds)
        {
            GetBlocks(Name, airlockDoors, allLights, allSounds);
        }

        private void GetBlocks(string airlockName, List<IMyDoor> airlockDoors, List<IMyLightingBlock> allLights, List<IMySoundBlock> allSounds)
        {
            //sort through all doors
            airlockInteriorList.Clear();
            airlockExteriorList.Clear();
            airlockLightList.Clear();
            airlockSoundList.Clear();

            foreach (var thisDoor in airlockDoors)
            {
                string thisDoorName = thisDoor.CustomName.Replace(" ", "").ToLower();
                if (thisDoorName.Contains(airlockName))
                {
                    if (thisDoorName.Contains("airlockinterior"))
                    {
                        airlockInteriorList.Add(thisDoor);
                    }
                    else if (thisDoorName.Contains("airlockexterior"))
                    {
                        airlockExteriorList.Add(thisDoor);
                    }
                }
            }

            //sort through all lights 
            foreach (var thisLight in allLights)
            {
                if (thisLight.CustomName.Replace(" ", "").ToLower().Contains(airlockName))
                {
                    airlockLightList.Add(thisLight);
                }
            }

            //sort through all lights 
            foreach (var thisSound in allSounds)
            {
                if (thisSound.CustomName.Replace(" ", "").ToLower().Contains(airlockName))
                {
                    airlockSoundList.Add(thisSound);
                }
            }

            Info = $" Interior Doors: {airlockInteriorList.Count}\n Exterior Doors: {airlockExteriorList.Count}\n Lights: {airlockLightList.Count}\n Sound Blocks: {airlockSoundList.Count}";
        }

        public void DoLogic()
        {
            //Start checking airlock status   
            if (airlockInteriorList.Count == 0 || airlockExteriorList.Count == 0) //if we have both door types    
            {
                return;
            }

            //we assume the airlocks are closed until proven otherwise        
            bool isInteriorClosed = true;
            bool isExteriorClosed = true;

            //Door Interior Check          
            foreach (var airlockInterior in airlockInteriorList)
            {
                if (airlockInterior.OpenRatio > 0)
                {
                    Lock(airlockExteriorList);
                    isInteriorClosed = false;
                    break;
                    //if any doors yield false, bool will persist until comparison    
                }
            }

            //Door Exterior Check           
            foreach (var airlockExterior in airlockExteriorList)
            {
                if (airlockExterior.OpenRatio > 0)
                {
                    Lock(airlockInteriorList);
                    isExteriorClosed = false;
                    break;
                }
            }

            //if all Interior & Exterior doors closed 
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

            //if all Interior doors closed 
            if (isInteriorClosed)
            {
                Unlock(airlockExteriorList);
            }

            //if all Exterior doors closed     
            if (isExteriorClosed)
            {
                Unlock(airlockInteriorList);
            }
        }

        private void Lock(List<IMyDoor> doorList)
        {
            //locks all doors with the input list
            foreach (var lockDoor in doorList)
            {
                //if door is open, then close
                if (lockDoor.OpenRatio > 0)
                {
                    lockDoor.CloseDoor();
                }

                //if door is fully closed, then lock
                if (lockDoor.OpenRatio == 0 && lockDoor.Enabled)
                {
                    lockDoor.Enabled = false;
                }
            }
        }

        private void Unlock(List<IMyDoor> doorList)
        {
            //unlocks all doors with input list
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
            Color lightColor;
            float lightBlinkLength;

            //applies our status colors to the airlock lights        
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
