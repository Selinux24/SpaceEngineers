
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
    }
}
