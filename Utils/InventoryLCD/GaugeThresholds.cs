using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

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

        public static GaugeThresholds LoadThresholds(MyIni ini, string section)
        {
            if (!ini.ContainsSection(section)) return null;

            var thresholds = new GaugeThresholds();
            var found = true;
            var index = 1;
            while (found)
            {
                var thrName = $"threshold_{index}";
                var thr = ini.Get(section, thrName);
                if (thr.IsEmpty == false)
                {
                    var v = thr.ToString().Split(':');
                    var gaugeThreshold = new GaugeThreshold(float.Parse(v[0]), ParseColor(v[1]));
                    thresholds.Thresholds.Add(gaugeThreshold);
                }
                else
                {
                    found = false;
                }
                index++;
            }

            return null;
        }
        static Color ParseColor(string colorValue)
        {
            if (string.IsNullOrEmpty(colorValue)) return Color.Gray;

            string[] colorSplit = colorValue.Split(',');
            return new Color(int.Parse(colorSplit[0]), int.Parse(colorSplit[1]), int.Parse(colorSplit[2]), int.Parse(colorSplit[3]));
        }
        public static void SaveThresholds(MyIni ini, GaugeThresholds gaugeThreshold, string section)
        {
            if (gaugeThreshold == null) return;

            var index = 0;
            foreach (var threshold in gaugeThreshold.Thresholds)
            {
                index++;
                var thresholdName = $"threshold_{index}";
                ini.Set(section, thresholdName, threshold.ToString());
            }
        }

        public static GaugeThresholds DefaultItemThesholds()
        {
            var t = new GaugeThresholds();
            t.Thresholds.Add(new GaugeThreshold(0f, new Color(180, 0, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.25f, new Color(180, 130, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.50f, Color.Green));
            t.Thresholds.Add(new GaugeThreshold(1f, new Color(0, 0, 180, 128)));
            return t;
        }
        public static GaugeThresholds DefaultChestThesholds()
        {
            var t = new GaugeThresholds();
            t.Thresholds.Add(new GaugeThreshold(0f, Color.Green));
            t.Thresholds.Add(new GaugeThreshold(0.50f, new Color(180, 130, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.75f, new Color(180, 0, 0, 128)));
            return t;
        }
        public static GaugeThresholds DefaultTankThesholds()
        {
            var t = new GaugeThresholds();
            t.Thresholds.Add(new GaugeThreshold(0f, new Color(180, 0, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.25f, new Color(180, 130, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.50f, Color.Green));
            t.Thresholds.Add(new GaugeThreshold(1f, new Color(0, 0, 180, 128)));
            return t;
        }
        public static GaugeThresholds DefaultPowerThesholds()
        {
            var t = new GaugeThresholds();
            t.Thresholds.Add(new GaugeThreshold(0f, new Color(180, 0, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.25f, new Color(180, 130, 0, 128)));
            t.Thresholds.Add(new GaugeThreshold(0.50f, Color.Green));
            t.Thresholds.Add(new GaugeThreshold(1f, new Color(0, 0, 180, 128)));
            return t;
        }
    }
}
