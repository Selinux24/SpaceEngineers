using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    class GaugeThresholds
    {
        public List<GaugeThreshold> Thresholds { get; private set; } = new List<GaugeThreshold>();
     
        public GaugeThreshold GetGaugeThreshold(float value)
        {
            var gaugeThreshold = Thresholds.First();
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
