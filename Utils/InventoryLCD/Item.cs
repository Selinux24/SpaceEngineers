using System;

namespace IngameScript
{
    class Item : IComparable<Item>
    {
        public string Name { get; set; }
        public string Data { get; set; }
        public string Type { get; set; }
        public double Amount { get; set; }
        public Variances Variance { get; set; }
        public string Icon => $"{Type}/{Data}";

        public int CompareTo(Item other)
        {
            return Amount.CompareTo(other.Amount);
        }
    }
}
