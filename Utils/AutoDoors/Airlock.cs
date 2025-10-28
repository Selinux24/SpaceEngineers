using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    public class Airlock
    {
        const string soundBlockPlayingString = "%Playing sound...%";

        readonly List<CBlock<IMyDoor>> airlockInteriorList = new List<CBlock<IMyDoor>>();
        readonly List<CBlock<IMyDoor>> airlockExteriorList = new List<CBlock<IMyDoor>>();
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

            GetBlocks(airlockName, airlockDoors, allLights, allSounds);
            Info = $" Interior Doors: {airlockInteriorList.Count}\n Exterior Doors: {airlockExteriorList.Count}\n Lights: {airlockLightList.Count}\n Sound Blocks: {airlockSoundList.Count}";
        }
        void GetBlocks(string airlockName, List<IMyDoor> airlockDoors, List<IMyLightingBlock> allLights, List<IMySoundBlock> allSounds)
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
                    airlockInteriorList.Add(new CBlock<IMyDoor>(door));
                }
                else if (name.Contains(Program.AirlockExteriorTag))
                {
                    airlockExteriorList.Add(new CBlock<IMyDoor>(door));
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

        public void DoLogic()
        {
            if (airlockInteriorList.Count == 0 || airlockExteriorList.Count == 0) return;

            //We assume the airlocks are closed until proven otherwise

            //Door Interior Check
            bool isInteriorClosed = true;
            foreach (var airlockInterior in airlockInteriorList)
            {
                if (airlockInterior.Block.OpenRatio > 0)
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
                if (airlockExterior.Block.OpenRatio > 0)
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
        void Lock(List<CBlock<IMyDoor>> doorList)
        {
            foreach (var lockDoor in doorList)
            {
                //If door is open, then close
                if (lockDoor.Block.OpenRatio > 0)
                {
                    lockDoor.Block.CloseDoor();
                }

                //If door is fully closed, then lock
                if (lockDoor.Block.OpenRatio == 0 && lockDoor.Block.Enabled)
                {
                    lockDoor.Block.Enabled = false;
                }
            }
        }
        void Unlock(List<CBlock<IMyDoor>> doorList)
        {
            foreach (var unlockDoor in doorList)
            {
                unlockDoor.Block.Enabled = true;
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
