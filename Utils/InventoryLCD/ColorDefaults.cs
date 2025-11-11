using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class ColorDefaults
    {
        readonly MyIni ini;
        readonly string section;
        string colorDefault;

        public ColorDefaults(MyIni ini, string section)
        {
            this.ini = ini;
            this.section = section;
        }

        public void Load()
        {
            colorDefault = ini.Get(section, "default").ToString("128,128,128,255");
        }
        public void Save()
        {
            ini.Set(section, "default", colorDefault);
            ini.Set(section, "Ice", "040,130,130,255");
            ini.Set(section, "Stone", "120,040,000,200");
            ini.Set(section, "Gravel", "108,108,108,255");
            ini.Set(section, "Cobalt Ore", "000,080,080,255");
            ini.Set(section, "Cobalt Ingot", "000,080,080,255");
            ini.Set(section, "Magnesium Ore", "140,130,240,255");
            ini.Set(section, "Magnesium Powder", "140,130,240,255");
            ini.Set(section, "Gold Ore", "255,153,000,255");
            ini.Set(section, "Gold Ingot", "255,153,000,255");
            ini.Set(section, "Iron Ore", "040,040,040,255");
            ini.Set(section, "Iron Ingot", "040,040,040,255");
            ini.Set(section, "Nickel Ore", "110,080,080,255");
            ini.Set(section, "Nickel Ingot", "110,080,080,255");
            ini.Set(section, "Platinum Ore", "120,150,120,255");
            ini.Set(section, "Platinum Ingot", "120,150,120,255");
            ini.Set(section, "Silicon Ore", "150,150,150,255");
            ini.Set(section, "Silicon Wafer", "150,150,150,255");
            ini.Set(section, "Silver Ore", "120,120,150,255");
            ini.Set(section, "Silver Ingot", "120,120,150,255");
            ini.Set(section, "Uranium Ore", "040,130,000,200");
            ini.Set(section, "Uranium Ingot", "040,130,000,200");
        }

        public Color GetColor(string name, string data, string defaultValue = null)
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
        public string GetDefault()
        {
            return colorDefault;
        }
    }
}
