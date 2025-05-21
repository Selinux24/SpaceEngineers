using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    class UnloadRequest
    {
        public string From;
        public int OrderId;
        public bool Idle;

        public UnloadRequest()
        {

        }
        public UnloadRequest(string line)
        {
            LoadFromStorage(line);
        }

        public string SaveToStorage()
        {
            return $"UnloadRequest={From}|OrderId={OrderId}|Idle={(Idle ? 1 : 0)}";
        }
        public void LoadFromStorage(string line)
        {
            var parts = line.Split('|');
            From = Utils.ReadString(parts, "From");
            OrderId = Utils.ReadInt(parts, "OrderId");
            Idle = Utils.ReadInt(parts, "Idle") == 1;
        }

        public static List<string> SaveListToStorage(List<UnloadRequest> unloadRequests)
        {
            var unloadList = string.Join("¬", unloadRequests.Select(o => o.SaveToStorage()).ToList());

            return new List<string>
            {
                $"UnloadRequestCount={unloadRequests.Count}",
                $"UnloadRequests={unloadList}",
            };
        }
        public static void LoadListFromStorage(string[] storageLines, List<UnloadRequest> unloadRequests)
        {
            int reqCount = Utils.ReadInt(storageLines, "UnloadRequestCount");
            if (reqCount <= 0) return;

            string unloadList = Utils.ReadString(storageLines, "UnloadRequests");
            string[] unloadLines = unloadList.Split('¬');
            for (int i = 0; i < unloadLines.Length; i++)
            {
                unloadRequests.Add(new UnloadRequest(unloadLines[i]));
            }
        }
    }
}
