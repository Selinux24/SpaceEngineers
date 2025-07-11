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
        public readonly List<Vector3D> ToCustomer = new List<Vector3D>();
        public string Warehouse;
        public readonly List<Vector3D> ToWarehouse = new List<Vector3D>();
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
        public Order(string warehouse, List<Vector3D> toWarehouse, string customer, List<Vector3D> toCustomer)
        {
            Id = ++lastId;
            Warehouse = warehouse;
            ToWarehouse.AddRange(toWarehouse);
            Customer = customer;
            ToCustomer.AddRange(toCustomer);
        }

        public string SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"Id={Id}",
                $"Customer={Customer}",
                $"ToCustomer={Utils.VectorListToStr(ToCustomer)}",
                $"Warehouse={Warehouse}",
                $"ToWarehouse={Utils.VectorListToStr(ToWarehouse)}",
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
            ToCustomer.Clear();
            ToCustomer.AddRange(Utils.ReadVectorList(parts, "ToCustomer"));
            Warehouse = Utils.ReadString(parts, "Warehouse");
            ToWarehouse.Clear();
            ToWarehouse.AddRange(Utils.ReadVectorList(parts, "ToWarehouse"));
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
