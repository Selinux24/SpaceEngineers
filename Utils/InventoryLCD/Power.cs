using System;

namespace IngameScript
{
    class Power
    {
        public string Type { get; set; }
        public float Current { get; set; } = 0f;
        public float Max { get; set; } = 0f;
        public int Count { get; set; } = 0;
        public double Moyen
        {
            get
            {
                return Math.Round(Current / Count, 2);
            }
        }

        public void AddCurrent(float value)
        {
            Current += value;
            Count += 1;
        }
        public void AddMax(float value)
        {
            Max += value;
        }
    }
}
