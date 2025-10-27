using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    class BlockSystem<T> where T : class
    {
        public List<T> List { get; private set; } = new List<T>();
        public bool IsEmpty
        {
            get
            {
                if (List != null && List.Count > 0)
                {
                    return false;
                }
                return true;
            }
        }
        public T First
        {
            get
            {
                if (!IsEmpty)
                {
                    return List.First();
                }
                return null;
            }
        }

        public BlockSystem()
        {

        }
        public BlockSystem(List<T> list)
        {
            List = list;
        }

        public static void SearchBlocks(Program program, BlockSystem<T> blockSystem, Func<T, bool> collect = null, string info = null)
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

                var groupList = new List<T>();
                groups.ForEach(group =>
                {
                    groupList.Clear();
                    group.GetBlocksOfType(blockSystem.List, filter.BlockVisitor());
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
