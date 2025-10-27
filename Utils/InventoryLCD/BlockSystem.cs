using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace IngameScript
{
    class BlockSystem<T> where T : class
    {
        public List<T> List { get; private set; } = new List<T>();
        public bool IsEmpty => List.Count == 0;
        public T First
        {
            get
            {
                if (IsEmpty) return null;

                return List[0];
            }
        }

        public BlockSystem()
        {

        }

        public static void SearchBlocks(Program program, BlockSystem<T> blockSystem, Func<T, bool> collect = null)
        {
            blockSystem.List.Clear();

            program.GridTerminalSystem.GetBlocksOfType(blockSystem.List, collect);
        }
        public static void SearchByFilter(Program program, BlockSystem<T> blockSystem, BlockFilter<T> filter)
        {
            blockSystem.List.Clear();

            if (filter.ByGroup)
            {
                var groups = new List<IMyBlockGroup>();
                program.GridTerminalSystem.GetBlockGroups(groups, filter.GroupVisitor());

                groups.ForEach(group =>
                {
                    var groupList = new List<T>();
                    group.GetBlocksOfType(groupList, filter.BlockVisitor());
                    blockSystem.List.AddList(groupList);
                });
            }
            else
            {
                program.GridTerminalSystem.GetBlocksOfType(blockSystem.List, filter.BlockVisitor());
            }

            program.Echo($"{typeof(T).Name}({filter.Value}):{blockSystem.List.Count}");
        }

        public void ForEach(Action<T> action)
        {
            if (IsEmpty) return;

            List.ForEach(action);
        }
    }
}
