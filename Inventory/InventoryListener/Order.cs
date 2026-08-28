using System.Collections.Generic;

namespace IngameScript
{
    class Order
    {
        const string OrderSep = "¬";

        public string Items { get; set; }
        public long SourceId { get; set; }

        public void LoadFromStorage(string storageLine)
        {
            if (string.IsNullOrWhiteSpace(storageLine)) return;
            string[] storageLines = storageLine.Split(OrderSep.ToCharArray());

            Items = Utils.ReadString(storageLines, "items", null);
            SourceId = Utils.ReadLong(storageLines, "sourceId", -1);
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"items={Items}",
                $"sourceId={SourceId}",
            };

            return string.Join(OrderSep, parts);
        }
    }
}
