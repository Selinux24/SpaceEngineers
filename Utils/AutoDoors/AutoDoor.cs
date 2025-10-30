using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    class AutoDoor
    {
        const string StrAutoDoor = "AutoDoor";
        const string StrAutoCloseTime = "AutoCloseTime";

        readonly CBlock<IMyDoor> door;
        bool firstUpdate = true;
        double autoCloseTime = 0;
        double doorOpenTime = 0;

        public bool WasOpen { get; private set; } = false;

        public AutoDoor(IMyDoor door, double autoCloseTime)
        {
            this.door = new CBlock<IMyDoor>(door);
            this.autoCloseTime = autoCloseTime;
        }

        public bool IsOpen()
        {
            return door.Block.OpenRatio > 0;
        }
        public bool IsFulllyClosed()
        {
            return door.Block.OpenRatio == 0;
        }
        public bool IsEnabled()
        {
            return door.Block.Enabled;
        }

        public void Enable()
        {
            door.Block.Enabled = true;
        }
        public void Disable()
        {
            door.Block.Enabled = false;
        }

        public void Update(double time)
        {
            RefreshConfig();

            if (door.Block.OpenRatio == 0)
            {
                doorOpenTime = 0;
                WasOpen = false;
                return;
            }

            if (!WasOpen)
            {
                //Begin new count
                WasOpen = true;
                doorOpenTime = 0;
                return;
            }

            //Was open
            doorOpenTime += time;

            if (autoCloseTime <= doorOpenTime)
            {
                door.Block.CloseDoor();
                doorOpenTime = 0;
                WasOpen = false;
            }
        }
        void RefreshConfig()
        {
            if (!firstUpdate && !door.ConfigChanged) return;
            firstUpdate = false;

            if (!door.UpdateConfig()) return;

            var value = door.Config.Get(StrAutoDoor, StrAutoCloseTime);
            if (!value.IsEmpty)
            {
                autoCloseTime = value.ToDouble();
            }
        }
    }
}
