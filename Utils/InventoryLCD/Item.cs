using System;

namespace IngameScript
{
    class Item : IComparable<Item>
    {
        public const string TYPE_ORE = "MyObjectBuilder_Ore";
        public const string TYPE_INGOT = "MyObjectBuilder_Ingot";
        public const string TYPE_COMPONENT = "MyObjectBuilder_Component";
        public const string TYPE_AMMO = "MyObjectBuilder_AmmoMagazine";

        public string Name { get; set; }
        public string Data { get; set; }
        public string Type { get; set; }
        public double Amount { get; set; }
        public int Variance { get; set; }
        public string Icon => $"{Type}/{Data}";

        public int CompareTo(Item other)
        {
            return Amount.CompareTo(other.Amount);
        }
    }
}
