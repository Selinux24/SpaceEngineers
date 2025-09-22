using System;
using System.Collections.Generic;

namespace IngameScript
{
    class ExchangeRequest
    {
        private readonly Config config;
        private DateTime doneTime;

        public string ExchangeType { get; private set; }
        public string Ship { get; private set; }

        public bool Pending { get; private set; } = true;
        public bool Doing => !Pending && (DateTime.Now - doneTime) <= config.ExchangeRequestTimeOut;
        public bool Expired => !Pending && (DateTime.Now - doneTime) > config.ExchangeRequestTimeOut;

        public ExchangeRequest(Config config, string exchangeType, string ship)
        {
            this.config = config;
            ExchangeType = exchangeType;
            Ship = ship;
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
                list.Add($"Type={r.ExchangeType}|From={r.Ship}|Pending={(r.Pending ? 1 : 0)}|doneTime={r.doneTime.Ticks}");
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

                var exchangeType = Utils.ReadString(parts, "Type");
                var ship = Utils.ReadString(parts, "From");

                var exchange = new ExchangeRequest(config, exchangeType, ship)
                {
                    Pending = Utils.ReadInt(parts, "Pending") == 1,
                    doneTime = new DateTime(Utils.ReadLong(parts, "doneTime")),
                };

                requests.Add(exchange);
            }
        }
    }
}
