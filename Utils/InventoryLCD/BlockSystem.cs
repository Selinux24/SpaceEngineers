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
        public static void SearchByFilter(Program program, BlockSystem<T> blockSystem, BlockFilter<T> filter, Func<T, bool> blockCollect = null)
        {
            blockSystem.List.Clear();

            if (filter.ByGroup)
            {
                var groups = new List<IMyBlockGroup>();
                program.GridTerminalSystem.GetBlockGroups(groups, filter.GroupVisitor());

                groups.ForEach(group =>
                {
                    var groupList = new List<T>();
                    group.GetBlocksOfType(groupList, blockCollect);
                    blockSystem.List.AddList(groupList);
                });
            }
            else
            {
                Func<T, bool> cmp = b =>
                {
                    if (blockCollect == null) return filter.BlockVisitor()(b);

                    return filter.BlockVisitor()(b) && blockCollect(b);
                };

                program.GridTerminalSystem.GetBlocksOfType(blockSystem.List, cmp);
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
