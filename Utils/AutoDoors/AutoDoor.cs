using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    class AutoDoor
    {
        const string StrAutoDoor = "AutoDoor";
        const string StrAutoCloseTime = "AutoCloseTime";

        double autoCloseTime = 0;
        double doorOpenTime = 0;
        bool wasOpen = false;

        public CBlock<IMyDoor> Door { get; private set; } = null;

        public AutoDoor(IMyDoor door, double autoCloseTime)
        {
            Door = new CBlock<IMyDoor>(door);
            this.autoCloseTime = autoCloseTime;
        }

        public void AutoClose(double time)
        {
            RefreshConfig();

            if (Door.Block.OpenRatio == 0)
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
                Door.Block.CloseDoor();
                doorOpenTime = 0;
                wasOpen = false;
            }
        }
        void RefreshConfig()
        {
            if (!Door.ConfigChanged) return;
            if (!Door.UpdateConfig()) return;

            var value = Door.Config.Get(StrAutoDoor, StrAutoCloseTime);
            if (!value.IsEmpty)
            {
                autoCloseTime = value.ToDouble();
            }
        }
    }
}
