using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    public class AutoDoor
    {
        double doorOpenTime = 0;
        double autoCloseTime = 0;
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
                //begin new count
                wasOpen = true;
                doorOpenTime = 0;
                return;
            }

            //if wasOpen
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
