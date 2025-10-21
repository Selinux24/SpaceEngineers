using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class Block
    {
        string lastCustomData = null;

        public IMyTerminalBlock MyBlock { get; private set; }
        public MyIni Ini { get; private set; } = new MyIni();
        public bool Changed { get; private set; }

        public Block(IMyTerminalBlock block)
        {
            MyBlock = block;
        }

        public void Update()
        {
            Changed = MyBlock.CustomData != lastCustomData;
            if (!Changed) return;

            MyIniParseResult result;
            Ini.TryParse(MyBlock.CustomData, out result);

            lastCustomData = MyBlock.CustomData;
        }
    }
}
