using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class Limits
    {
        const int MassIronIngot = 33218500;
        const int MassSiliconWafer = 9833920;
        const int MassNickelIngot = 37667410;
        const int MassCobaltIngot = 37667410;
        const int MassSilverIngot = 44407895;
        const int MassMagnesiumPowder = 7336957;
        const int MassGoldIngot = 81129780;
        const int MassUraniumIngot = 81131117;
        const int MassPlatinumIngot = 89762520;
        const int MassGravel = 11402027;

        const string IronIngot = "Iron Ingot";
        const string SiliconWafer = "Silicon Wafer";
        const string NickelIngot = "Nickel Ingot";
        const string CobaltIngot = "Cobalt Ingot";
        const string SilverIngot = "Silver Ingot";
        const string MagnesiumPowder = "Magnesium Powder";
        const string GoldIngot = "Gold Ingot";
        const string UraniumIngot = "Uranium Ingot";
        const string PlatinumIngot = "Platinum Ingot";
        const string Gravel = "Gravel";

        readonly MyIni ini;
        readonly string section;
        readonly Dictionary<string, int> itemMass;

        public Limits(MyIni ini, string section)
        {
            this.ini = ini;
            this.section = section;

            itemMass = new Dictionary<string, int>
            {
                { IronIngot, MassIronIngot },
                { SiliconWafer, MassSiliconWafer },
                { NickelIngot, MassNickelIngot },
                { CobaltIngot, MassCobaltIngot },
                { SilverIngot, MassSilverIngot },
                { MagnesiumPowder, MassMagnesiumPowder },
                { GoldIngot, MassGoldIngot },
                { UraniumIngot, MassUraniumIngot },
                { PlatinumIngot, MassPlatinumIngot },
                { Gravel, MassGravel }
            };
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var value = ini.Get(section, key).ToInt32(defaultValue);
            if (!itemMass.ContainsKey(key))
            {
                return value;
            }

            return value * itemMass[key];
        }
    }
}
