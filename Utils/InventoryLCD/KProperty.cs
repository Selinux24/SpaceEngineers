using System;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    internal class KProperty
    {
        readonly Program program;
        readonly MyIni MyIni = new MyIni();

        string limitDefault;
        string colorDefault;

        public string LCDFilter { get; private set; } = "*";
        public GaugeThresholds ItemThresholds { get; set; }
        public GaugeThresholds ChestThresholds { get; set; }
        public GaugeThresholds TankThresholds { get; set; }
        public GaugeThresholds PowerThresholds { get; set; }

        public KProperty(Program program)
        {
            this.program = program;
        }

        public void Load()
        {
            MyIniParseResult result;
            if (!MyIni.TryParse(program.Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }
            limitDefault = MyIni.Get("Limit", "default").ToString("10000");
            colorDefault = MyIni.Get("Color", "default").ToString("128,128,128,255");

            LCDFilter = MyIni.Get("LCD", "filter").ToString("*");

            LoadThresholds();

            if (program.Me.CustomData.Trim().Equals(""))
            {
                Save(true);
            }
        }

        private void LoadThresholds()
        {
            ItemThresholds = LoadThresholds("ItemThresholds", true);
            ChestThresholds = LoadThresholds("ChestThresholds", false);
            TankThresholds = LoadThresholds("TankThresholds", false);
            PowerThresholds = LoadThresholds("PowerThresholds", false);

            if (ItemThresholds == null)
            {
                ItemThresholds = new GaugeThresholds();
                ItemThresholds.Thresholds.Add(new GaugeThreshold(0f, new Color(180, 0, 0, 128)));
                ItemThresholds.Thresholds.Add(new GaugeThreshold(0.25f, new Color(180, 130, 0, 128)));
                ItemThresholds.Thresholds.Add(new GaugeThreshold(0.50f, Color.Green));
                ItemThresholds.Thresholds.Add(new GaugeThreshold(1f, new Color(0, 0, 180, 128)));
            }
            if (ChestThresholds == null)
            {
                ChestThresholds = new GaugeThresholds();
                ChestThresholds.Thresholds.Add(new GaugeThreshold(0f, Color.Green));
                ChestThresholds.Thresholds.Add(new GaugeThreshold(0.50f, new Color(180, 130, 0, 128)));
                ChestThresholds.Thresholds.Add(new GaugeThreshold(0.75f, new Color(180, 0, 0, 128)));
            }
            if (TankThresholds == null)
            {
                TankThresholds = new GaugeThresholds();
                TankThresholds.Thresholds.Add(new GaugeThreshold(0f, new Color(180, 0, 0, 128)));
                TankThresholds.Thresholds.Add(new GaugeThreshold(0.25f, new Color(180, 130, 0, 128)));
                TankThresholds.Thresholds.Add(new GaugeThreshold(0.50f, Color.Green));
                TankThresholds.Thresholds.Add(new GaugeThreshold(1f, new Color(0, 0, 180, 128)));
            }
            if (PowerThresholds == null)
            {
                PowerThresholds = new GaugeThresholds();
                PowerThresholds.Thresholds.Add(new GaugeThreshold(0f, new Color(180, 0, 0, 128)));
                PowerThresholds.Thresholds.Add(new GaugeThreshold(0.25f, new Color(180, 130, 0, 128)));
                PowerThresholds.Thresholds.Add(new GaugeThreshold(0.50f, Color.Green));
                PowerThresholds.Thresholds.Add(new GaugeThreshold(1f, new Color(0, 0, 180, 128)));
            }
        }
        private GaugeThresholds LoadThresholds(string section, bool overflowDefault)
        {
            if (MyIni.ContainsSection(section))
            {
                var thresholds = new GaugeThresholds();
                var found = true;
                var index = 1;
                while (found)
                {
                    var thresholdName = $"threshold_{index}";
                    var threshold = MyIni.Get(section, thresholdName);
                    if (threshold.IsEmpty == false)
                    {
                        var value = threshold.ToString();
                        var values = value.Split(':');
                        var gaugeThreshold = new GaugeThreshold();
                        gaugeThreshold.Value = float.Parse(values[0]);
                        gaugeThreshold.Color = ParseColor(values[1]);
                        thresholds.Thresholds.Add(gaugeThreshold);
                    }
                    else
                    {
                        found = false;
                    }
                    index++;
                }

            }
            return null;
        }
        
        public string Get(string section, string key, string default_value = "")
        {
            return MyIni.Get(section, key).ToString(default_value);
        }
        public int GetInt(string section, string key, int default_value = 0)
        {
            return MyIni.Get(section, key).ToInt32(default_value);
        }
        public Color GetColor(string section, string name, string data, string default_value = null)
        {
            if (default_value == null) default_value = colorDefault;
            string colorValue = MyIni.Get(section, name).ToString(default_value);
            if(colorValue.Equals("")) colorValue = MyIni.Get(section, data).ToString(default_value);

            Color color = Color.Gray;
            // Find matches.
            if (!colorValue.Equals(""))
            {
                string[] colorSplit = colorValue.Split(',');
                color = new Color(int.Parse(colorSplit[0]), int.Parse(colorSplit[1]), int.Parse(colorSplit[2]), int.Parse(colorSplit[3]));
            }
            return color;
        }

        public static Color ParseColor(string colorValue)
        {
            Color color = Color.Gray;
            // Find matches.
            if (string.IsNullOrEmpty(colorValue) == false)
            {
                string[] colorSplit = colorValue.Split(',');
                color = new Color(int.Parse(colorSplit[0]), int.Parse(colorSplit[1]), int.Parse(colorSplit[2]), int.Parse(colorSplit[3]));
            }
            return color;
        }

        public void Save(bool prepare = false)
        {
            MyIniParseResult result;
            if (!MyIni.TryParse(program.Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }
            MyIni.Set("LCD", "filter", LCDFilter);

            MyIni.Set("Limit", "default", limitDefault);
            if (prepare)
            {
                MyIni.Set("Limit", "Cobalt", "1000");
                MyIni.Set("Limit", "Iron", "100000");
                MyIni.Set("Limit", "Gold", "1000");
                MyIni.Set("Limit", "Platinum", "1000");
                MyIni.Set("Limit", "Silver", "1000");
            }
            MyIni.Set("Color", "default", colorDefault);
            if (prepare)
            {
                MyIni.Set("Color", "Cobalt", "000,080,080,255");
                MyIni.Set("Color", "Gold", "255,153,000,255");
                MyIni.Set("Color", "Ice", "040,130,130,255");
                MyIni.Set("Color", "Iron", "040,040,040,255");
                MyIni.Set("Color", "Nickel", "110,080,080,255");
                MyIni.Set("Color", "Platinum", "120,150,120,255");
                MyIni.Set("Color", "Silicon", "150,150,150,255");
                MyIni.Set("Color", "Silver", "120,120,150,255");
                MyIni.Set("Color", "Stone", "120,040,000,200");
                MyIni.Set("Color", "Uranium", "040,130,000,200");
            }

            SaveThresholds(ItemThresholds, "ItemThresholds");
            SaveThresholds(ChestThresholds, "ChestThresholds");
            SaveThresholds(TankThresholds, "TankThresholds");
            SaveThresholds(PowerThresholds, "PowerThresholds");

            program.Me.CustomData = MyIni.ToString();
        }
        private void SaveThresholds(GaugeThresholds gaugeThreshold, string section)
        {
            if (gaugeThreshold != null)
            {
                var index = 0;
                foreach (var threshold in gaugeThreshold.Thresholds)
                {
                    index++;
                    var thresholdName = $"threshold_{index}";
                    MyIni.Set(section, thresholdName, threshold.ToString());
                }
            }
        }
    }
}
