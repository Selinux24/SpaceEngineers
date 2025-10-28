using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class CBlock<T> where T : IMyTerminalBlock
    {
        string lastCustomData = null;

        public T Block { get; }
        public MyIni Config { get; } = new MyIni();
        public bool ConfigChanged => Block.CustomData != lastCustomData;

        public CBlock(T block)
        {
            Block = block;

            UpdateConfig();
        }

        public bool UpdateConfig()
        {
            MyIniParseResult result;
            if (Config.TryParse(Block.CustomData, out result))
            {
                lastCustomData = Block.CustomData;

                return true;
            }

            return false;
        }
    }
}
