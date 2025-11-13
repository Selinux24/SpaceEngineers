using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class DisplayLcd
    {
        const int CleanupTicks = 100;
        const string PrepareTag = "prepare";

        readonly DisplayInventory DisplayInventory;
        readonly DisplayDrill DisplayDrill;
        readonly DisplayMachine DisplayMachine;
        readonly DisplayPower DisplayPower;
        readonly DisplayShip DisplayShip;
        readonly DisplayTank DisplayTank;
        int cleanup;

        public IMyTerminalBlock Block { get; private set; }

        public DisplayLcd(Program program, IMyTerminalBlock block)
        {
            Block = block;

            DisplayInventory = new DisplayInventory(program, this);
            DisplayDrill = new DisplayDrill(program, this);
            DisplayMachine = new DisplayMachine(program, this);
            DisplayPower = new DisplayPower(program, this);
            DisplayShip = new DisplayShip(program);
            DisplayTank = new DisplayTank(program, this);
            cleanup = 0;
        }

        public void Load(MyIni ini)
        {
            DisplayInventory.Load(ini);
            DisplayDrill.Load(ini);
            DisplayMachine.Load(ini);
            DisplayPower.Load(ini);
            DisplayShip.Load(ini);
            DisplayTank.Load(ini);
        }
        public void Save(MyIni ini)
        {
            string customData = Block.CustomData ?? "";
            if (!customData.Trim().Equals(PrepareTag)) return;

            DisplayInventory.Save(ini);
            DisplayDrill.Save(ini);
            DisplayMachine.Save(ini);
            DisplayPower.Save(ini);
            DisplayShip.Save(ini);
            DisplayTank.Save(ini);

            Block.CustomData = ini.ToString();
        }
        public void Draw()
        {
            cleanup++;

            var drawing = new Drawing(Block);
            try
            {
                if (cleanup < CleanupTicks)
                {
                    DisplayInventory.Draw(drawing);
                    DisplayDrill.Draw(drawing);
                    DisplayMachine.Draw(drawing);
                    DisplayPower.Draw(drawing);
                    DisplayShip.Draw(drawing);
                    DisplayTank.Draw(drawing);
                }
                else
                {
                    drawing.Clean();
                    cleanup = 0;
                }
            }
            finally
            {
                drawing.Dispose();
            }
        }
    }
}
