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
            try
            {
                program.GridTerminalSystem.GetBlocksOfType(list, collect);
            }
            catch { }
            if (info == null) program.Echo(string.Format("List <{0}> count: {1}", typeof(T).Name, list.Count));
            else program.Echo(string.Format("List <{0}> count: {1}", info, list.Count));

            return new BlockSystem<T>(list);
        }
        public static BlockSystem<T> SearchByFilter(Program program, BlockFilter<T> filter)
        {
            List<T> list = new List<T>();
            try
            {
                if (filter.ByGroup)
                {
                    List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
                    program.GridTerminalSystem.GetBlockGroups(groups, filter.GroupVisitor());
                    List<T> group_list = new List<T>();
                    groups.ForEach(delegate (IMyBlockGroup group)
                    {
                        group_list.Clear();
                        group.GetBlocksOfType(list, filter.BlockVisitor());
                        list.AddList(group_list);
                    });
                }
                else
                {
                    program.GridTerminalSystem.GetBlocksOfType(list, filter.BlockVisitor());
                }
            }
            catch { }
            program.Echo(string.Format("List<{0}>({1}):{2}", typeof(T).Name, filter.Value, list.Count));
            return new BlockSystem<T>(list);
        }

        public void ForEach(Action<T> action)
        {
            if (!IsEmpty)
            {
                List.ForEach(action);
            }
        }
    }
}
