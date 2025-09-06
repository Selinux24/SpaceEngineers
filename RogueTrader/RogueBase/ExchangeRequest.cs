using System;
using System.Collections.Generic;

namespace IngameScript
{
    class ExchangeRequest
    {
        private readonly Config config;
        private DateTime doneTime;

        public string Ship;
        public ExchangeTasks Task;

        public bool Pending { get; private set; } = true;
        public bool Doing => !Pending && (DateTime.Now - doneTime) <= config.ExchangeRequestTimeOut;
        public bool Expired => !Pending && (DateTime.Now - doneTime) > config.ExchangeRequestTimeOut;

        public ExchangeRequest(Config config)
        {
            this.config = config;
        }

        public void SetDoing()
        {
            Pending = false;
            doneTime = DateTime.Now;
        }
        public void SetDone()
        {
            doneTime = DateTime.MinValue;
        }

        public static List<string> SaveListToStorage(List<ExchangeRequest> requests)
        {
            List<string> list = new List<string>();
            foreach (var r in requests)
            {
                list.Add($"From={r.Ship}|Task={(int)r.Task}|Pending={(r.Pending ? 1 : 0)}|doneTime={r.doneTime.Ticks}");
            }

            return new List<string>
            {
                $"UnloadRequestCount={requests.Count}",
                $"UnloadRequests={string.Join("¬", list)}",
            };
        }
        public static void LoadListFromStorage(Config config, string[] storageLines, List<ExchangeRequest> requests)
        {
            int reqCount = Utils.ReadInt(storageLines, "UnloadRequestCount");
            if (reqCount <= 0) return;

            string unloadList = Utils.ReadString(storageLines, "UnloadRequests");
            string[] unloadLines = unloadList.Split('¬');
            for (int i = 0; i < unloadLines.Length; i++)
            {
                var parts = unloadLines[i].Split('|');
                var exchange = new ExchangeRequest(config)
                {
                    Ship = Utils.ReadString(parts, "From"),
                    Task = (ExchangeTasks)Utils.ReadInt(parts, "Task"),
                    Pending = Utils.ReadInt(parts, "Pending") == 1,
                    doneTime = new DateTime(Utils.ReadLong(parts, "doneTime")),
                };

                requests.Add(exchange);
            }
        }
    }
}
