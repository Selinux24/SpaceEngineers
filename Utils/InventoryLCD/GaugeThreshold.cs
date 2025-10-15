using VRageMath;

namespace IngameScript
{
    public class GaugeThreshold
    {
        public float Value { get; set; }
        public Color Color { get; set; }

        public GaugeThreshold()
        {

        }
        public GaugeThreshold(float value, Color color)
        {
            Value = value;
            Color = color;
        }

        public override string ToString()
        {
            return $"{Value}:{Color.R},{Color.G},{Color.B},{Color.A}";
        }
    }
}
