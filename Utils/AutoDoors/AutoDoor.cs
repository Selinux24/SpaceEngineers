using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    public class AutoDoor
    {
        readonly double autoCloseTime = 0;

        double doorOpenTime = 0;
        bool wasOpen = false;

        public IMyDoor Door { get; private set; } = null;

        public AutoDoor(IMyDoor door, double regularDoorCloseTime, double hangarDoorCloseTime)
        {
            Door = door;

            if (door is IMyAirtightHangarDoor)
            {
                autoCloseTime = hangarDoorCloseTime;
            }
            else
            {
                autoCloseTime = regularDoorCloseTime;
            }
        }

        public void AutoClose(double time)
        {
            if (Door.OpenRatio == 0)
            {
                doorOpenTime = 0;
                wasOpen = false;
                return;
            }

            if (!wasOpen)
            {
                //Begin new count
                wasOpen = true;
                doorOpenTime = 0;
                return;
            }

            //Was open
            doorOpenTime += time;

            if (autoCloseTime <= doorOpenTime)
            {
                Door.CloseDoor();
                doorOpenTime = 0;
                wasOpen = false;
            }
        }
    }
}
