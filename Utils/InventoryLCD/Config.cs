using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class Config
    {
        readonly Program program;
        readonly MyIni ini = new MyIni();

        string limitDefault;
        string colorDefault;
        string lastCustomData;

        public string LCDFilter { get; private set; } = "*";
        public GaugeThresholds ItemThresholds { get; private set; }
        public GaugeThresholds ChestThresholds { get; private set; }
        public GaugeThresholds TankThresholds { get; private set; }
        public GaugeThresholds PowerThresholds { get; private set; }
        public BlockFilter<IMyTextPanel> TextPanelFilter { get; private set; }
        public BlockFilter<IMyCockpit> CockpitFilter { get; private set; }

        public Config(Program program)
        {
            this.program = program;
            lastCustomData = null;
        }

        public void Load()
        {
            if (lastCustomData == program.Me.CustomData) return;

            MyIniParseResult result;
            if (!ini.TryParse(program.Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }

            limitDefault = ini.Get("Limit", "default").ToString("10000");
            colorDefault = ini.Get("Color", "default").ToString("128,128,128,255");

            LCDFilter = ini.Get("LCD", "filter").ToString("*");
            TextPanelFilter = BlockFilter<IMyTextPanel>.Create(program.Me, LCDFilter);
            CockpitFilter = BlockFilter<IMyCockpit>.Create(program.Me, LCDFilter);

            LoadThresholds();

            if (program.Me.CustomData.Trim().Equals(""))
            {
                Save(true);
            }

            lastCustomData = program.Me.CustomData;
        }
        void LoadThresholds()
        {
            ItemThresholds = LoadThresholds("ItemThresholds");
            ChestThresholds = LoadThresholds("ChestThresholds");
            TankThresholds = LoadThresholds("TankThresholds");
            PowerThresholds = LoadThresholds("PowerThresholds");

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
        GaugeThresholds LoadThresholds(string section)
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

        public string Get(string section, string key, string defaultValue = "")
        {
            return ini.Get(section, key).ToString(defaultValue);
        }
        public int GetInt(string section, string key, int defaultValue = 0)
        {
            return ini.Get(section, key).ToInt32(defaultValue);
        }
        public Color GetColor(string section, string name, string data, string defaultValue = null)
        {
            if (defaultValue == null) defaultValue = colorDefault;
            string colorValue = ini.Get(section, name).ToString(defaultValue);
            if (colorValue.Equals("")) colorValue = ini.Get(section, data).ToString(defaultValue);

            Color color = Color.Gray;
            // Find matches.
            if (!colorValue.Equals(""))
            {
                string[] colorSplit = colorValue.Split(',');
                color = new Color(int.Parse(colorSplit[0]), int.Parse(colorSplit[1]), int.Parse(colorSplit[2]), int.Parse(colorSplit[3]));
            }
            return color;
        }

        public void Save(bool prepare = false)
        {
            MyIniParseResult result;
            if (!ini.TryParse(program.Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }
            ini.Set("LCD", "filter", LCDFilter);

            ini.Set("Limit", "default", limitDefault);
            if (prepare)
            {
                ini.Set("Limit", "Cobalt", "1000");
                ini.Set("Limit", "Iron", "100000");
                ini.Set("Limit", "Gold", "1000");
                ini.Set("Limit", "Platinum", "1000");
                ini.Set("Limit", "Silver", "1000");
            }
            ini.Set("Color", "default", colorDefault);
            if (prepare)
            {
                ini.Set("Color", "Cobalt", "000,080,080,255");
                ini.Set("Color", "Gold", "255,153,000,255");
                ini.Set("Color", "Ice", "040,130,130,255");
                ini.Set("Color", "Iron", "040,040,040,255");
                ini.Set("Color", "Nickel", "110,080,080,255");
                ini.Set("Color", "Platinum", "120,150,120,255");
                ini.Set("Color", "Silicon", "150,150,150,255");
                ini.Set("Color", "Silver", "120,120,150,255");
                ini.Set("Color", "Stone", "120,040,000,200");
                ini.Set("Color", "Uranium", "040,130,000,200");
            }

            SaveThresholds(ItemThresholds, "ItemThresholds");
            SaveThresholds(ChestThresholds, "ChestThresholds");
            SaveThresholds(TankThresholds, "TankThresholds");
            SaveThresholds(PowerThresholds, "PowerThresholds");

            program.Me.CustomData = ini.ToString();
        }
        void SaveThresholds(GaugeThresholds gaugeThreshold, string section)
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

        public void Reset()
        {
            program.Me.CustomData = "";
            Load();
            Save();
        }
    }
}
