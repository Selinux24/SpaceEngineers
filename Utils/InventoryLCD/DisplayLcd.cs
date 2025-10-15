using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class DisplayLcd
    {
        readonly Program program;
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
            this.program = program;
            Block = block;

            DisplayInventory = new DisplayInventory(program, this);
            DisplayDrill = new DisplayDrill(program, this);
            DisplayMachine = new DisplayMachine(program, this);
            DisplayPower = new DisplayPower(program);
            DisplayShip = new DisplayShip(program);
            DisplayTank = new DisplayTank(program);
            cleanup = 0;
        }

        public void Load(MyIni MyIni)
        {
            DisplayInventory.Load(MyIni);
            DisplayDrill.Load(MyIni);
            DisplayMachine.Load(MyIni);
            DisplayPower.Load(MyIni);
            DisplayShip.Load(MyIni);
            DisplayTank.Load(MyIni);

            if (!Block.CustomData.Trim().Equals("prepare") && !program.ForceUpdate)
            {
                return;
            }

            program.DrawingSurface.WriteText($"Prepare:{Block.CustomName}\n", true);
            DisplayInventory.Save(MyIni);
            DisplayDrill.Save(MyIni);
            DisplayMachine.Save(MyIni);
            DisplayPower.Save(MyIni);
            DisplayShip.Save(MyIni);
            DisplayTank.Save(MyIni);
            Block.CustomData = MyIni.ToString();
        }
        public void Draw()
        {
            cleanup++;
            var drawing = new Drawing(Block);
            try
            {
                if (cleanup < 100)
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
                drawing?.Dispose();
            }
        }
    }
}
