using System.Collections.Generic;

namespace IngameScript
{
    class ExchangeRequest
    {
        public string Ship;
        public int OrderId;
        public bool Idle;
        public ExchangeTasks Task;

        public ExchangeRequest()
        {

        }

        public static List<string> SaveListToStorage(List<ExchangeRequest> requests)
        {
            List<string> list = new List<string>();
            foreach (var r in requests)
            {
                list.Add($"From={r.Ship}|OrderId={r.OrderId}|Idle={(r.Idle ? 1 : 0)}|Task={(int)r.Task}");
            }

            return new List<string>
            {
                $"UnloadRequestCount={requests.Count}",
                $"UnloadRequests={string.Join("¬", list)}",
            };
        }
        public static void LoadListFromStorage(string[] storageLines, List<ExchangeRequest> requests)
        {
            int reqCount = Utils.ReadInt(storageLines, "UnloadRequestCount");
            if (reqCount <= 0) return;

            string unloadList = Utils.ReadString(storageLines, "UnloadRequests");
            string[] unloadLines = unloadList.Split('¬');
            for (int i = 0; i < unloadLines.Length; i++)
            {
                var parts = unloadLines[i].Split('|');
                var exchange = new ExchangeRequest()
                {
                    Ship = Utils.ReadString(parts, "From"),
                    OrderId = Utils.ReadInt(parts, "OrderId"),
                    Idle = Utils.ReadInt(parts, "Idle") == 1,
                    Task = (ExchangeTasks)Utils.ReadInt(parts, "Task"),
                };

                requests.Add(exchange);
            }
        }
    }
}
