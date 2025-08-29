using System;
using System.Collections.Generic;

namespace IngameScript
{
    class ExchangeRequest
    {
        private DateTime doneTime;

        public string Ship;

        public bool Pending { get; private set; } = true;
        public bool Expired => !Pending && (DateTime.Now - doneTime).TotalSeconds > 120;

        public ExchangeRequest()
        {

        }

        public void SetDone()
        {
            Pending = false;
            doneTime = DateTime.Now;
        }

        public static List<string> SaveListToStorage(List<ExchangeRequest> requests)
        {
            List<string> list = new List<string>();
            foreach (var r in requests)
            {
                list.Add($"From={r.Ship}|Pending={(r.Pending ? 1 : 0)}");
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
                    Pending = Utils.ReadInt(parts, "Pending") == 1,
                };

                requests.Add(exchange);
            }
        }
    }
}
