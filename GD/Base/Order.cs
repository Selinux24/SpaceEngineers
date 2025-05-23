using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    class Order
    {
        static int lastId = 0;

        public int Id;
        public string Customer;
        public Vector3D CustomerParking;
        public string Warehouse;
        public Vector3D WarehouseParking;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        public string AssignedShip;

        public Order()
        {
            Id = ++lastId;
        }
        public Order(string line)
        {
            LoadFromStorage(line);
        }

        public string SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Id={Id}",
                $"Customer={Customer}",
                $"CustomerParking={Utils.VectorToStr(CustomerParking)}",
                $"Warehouse={Warehouse}",
                $"WarehouseParking={Utils.VectorToStr(WarehouseParking)}",
                $"AssignedShip={AssignedShip}",
                $"Items={string.Join(";", Items.Select(i => $"{i.Key}:{i.Value}"))}",
            };
            return string.Join("|", parts);
        }
        public void LoadFromStorage(string line)
        {
            var parts = line.Split('|');

            Id = Utils.ReadInt(parts, "Id");
            Customer = Utils.ReadString(parts, "Customer");
            CustomerParking = Utils.ReadVector(parts, "CustomerParking");
            Warehouse = Utils.ReadString(parts, "Warehouse");
            WarehouseParking = Utils.ReadVector(parts, "WarehouseParking");
            AssignedShip = Utils.ReadString(parts, "AssignedShip");

            Items.Clear();
            var itemsParts = Utils.ReadString(parts, "Items").Split(';');
            foreach (var item in itemsParts)
            {
                var itemParts = item.Split(':');
                if (itemParts.Length != 2) continue;

                string itemName = itemParts[0];
                int itemAmount;
                if (!int.TryParse(itemParts[1], out itemAmount)) continue;
                Items[itemName] = itemAmount;
            }

            lastId = Id + 1;
        }

        public static List<string> SaveListToStorage(List<Order> orders)
        {
            var orderList = string.Join("¬", orders.Select(o => o.SaveToStorage()).ToList());

            return new List<string>
            {
                $"OrderCount={orders.Count}",
                $"Orders={orderList}",
            };
        }
        public static void LoadListFromStorage(string[] storageLines, List<Order> orders)
        {
            int orderCount = Utils.ReadInt(storageLines, "OrderCount");
            if (orderCount == 0) return;

            string orderList = Utils.ReadString(storageLines, "Orders");
            string[] ordersLines = orderList.Split('¬');
            for (int i = 0; i < ordersLines.Length; i++)
            {
                orders.Add(new Order(ordersLines[i]));
            }
        }
    }
}
