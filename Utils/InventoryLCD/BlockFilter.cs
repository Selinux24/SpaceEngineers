using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    class BlockFilter<T> where T : class
    {
        public string Value { get; set; }
        public string Filter { get; set; }
        public IMyCubeGrid CubeGrid { get; set; }
        public bool ByContains { get; set; } = false;
        public bool ByGroup { get; set; } = false;
        public bool MultiGrid { get; set; } = false;
        public bool HasInventory { get; set; } = false;

        public static BlockFilter<T> Create(IMyTerminalBlock parent, string filter, bool hasInventory = false)
        {
            var blockFilter = new BlockFilter<T>
            {
                Value = filter,
                CubeGrid = parent.CubeGrid,
                HasInventory = hasInventory
            };

            if (filter.Contains(":"))
            {
                string[] values = filter.Split(':');
                if (values[0].Contains("C")) blockFilter.ByContains = true;
                if (values[0].Contains("G")) blockFilter.ByGroup = true;
                if (values[0].Contains("M")) blockFilter.MultiGrid = true;
                if (!values[1].Equals("*")) blockFilter.Filter = values[1];
            }
            else
            {
                if (!filter.Equals("*")) blockFilter.Filter = filter;
            }

            return blockFilter;
        }
       
        public Func<T, bool> BlockVisitor()
        {
            return (block) =>
            {
                var tBlock = (IMyTerminalBlock)block;

                bool state = true;
                if (Filter != null && !ByGroup)
                {
                    if (ByContains) { if (!tBlock.CustomName.Contains(Filter)) state = false; }
                    else { if (!tBlock.CustomName.Equals(Filter)) state = false; }
                }
                if (!MultiGrid) { if (tBlock.CubeGrid != CubeGrid) state = false; }
                if (HasInventory) { if (!tBlock.HasInventory) state = false; }

                return state;
            };
        }
        public Func<IMyBlockGroup, bool> GroupVisitor()
        {
            return (group) =>
            {
                bool state = true;
                if (Filter != null && ByGroup)
                {
                    if (ByContains) { if (!group.Name.Contains(Filter)) state = false; }
                    else { if (!group.Name.Equals(Filter)) state = false; }
                }
                return state;
            };
        }
    }
}
