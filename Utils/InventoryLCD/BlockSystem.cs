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

        public static BlockSystem<T> SearchBlocks(Program program, Func<T, bool> collect = null, string info = null)
        {
            List<T> list = new List<T>();
            program.GridTerminalSystem.GetBlocksOfType(list, collect);

            if (info == null) program.Echo($"List <{typeof(T).Name}> count: {list.Count}");
            else program.Echo($"List <{info}> count: {list.Count}");

            return new BlockSystem<T>(list);
        }
        public static BlockSystem<T> SearchByFilter(Program program, BlockFilter<T> filter)
        {
            List<T> list = new List<T>();
            if (filter.ByGroup)
            {
                var groups = new List<IMyBlockGroup>();
                program.GridTerminalSystem.GetBlockGroups(groups, filter.GroupVisitor());

                var groupList = new List<T>();
                groups.ForEach(group =>
                {
                    groupList.Clear();
                    group.GetBlocksOfType(list, filter.BlockVisitor());
                    list.AddList(groupList);
                });
            }
            else
            {
                program.GridTerminalSystem.GetBlocksOfType(list, filter.BlockVisitor());
            }

            program.Echo($"{typeof(T).Name}({filter.Value}):{list.Count}");

            return new BlockSystem<T>(list);
        }

        public void Clear()
        {
            List.Clear();
        }
        public void ForEach(Action<T> action)
        {
            if (IsEmpty) return;

            List.ForEach(action);
        }
    }
}
