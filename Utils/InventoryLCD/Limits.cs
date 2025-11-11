using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class Limits
    {
        readonly MyIni ini;
        readonly string section;
        string limitDefault;

        public Limits(MyIni ini, string section)
        {
            this.ini = ini;
            this.section = section;
        }

        public void Load()
        {
            limitDefault = ini.Get(section, "default").ToString("10000");
        }
        public void Save()
        {
            ini.Set(section, "default", limitDefault);
            ini.Set(section, "Cobalt Ore", "10000");
            ini.Set(section, "Cobalt Ingot", "1000");
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            return ini.Get(section, key).ToInt32(defaultValue);
        }
    }
}
