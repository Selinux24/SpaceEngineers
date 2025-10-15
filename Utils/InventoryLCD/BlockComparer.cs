using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript
{
    class BlockComparer : IComparer<IMyTerminalBlock>
    {
        public int Compare(IMyTerminalBlock block1, IMyTerminalBlock block2)
        {
            return block1.CustomName.CompareTo(block2.CustomName);
        }
    }
}
