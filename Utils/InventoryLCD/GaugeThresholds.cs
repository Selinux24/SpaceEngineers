using System.Collections.Generic;

namespace IngameScript
{
    class GaugeThresholds
    {
        public List<GaugeThreshold> Thresholds { get; private set; } = new List<GaugeThreshold>();

        public GaugeThreshold GetGaugeThreshold(float value)
        {
            var gaugeThreshold = Thresholds[0];
            foreach (var threshold in Thresholds)
            {
                if (value >= threshold.Value)
                {
                    gaugeThreshold = threshold;
                }
            }
            return gaugeThreshold;
        }
    }
}
