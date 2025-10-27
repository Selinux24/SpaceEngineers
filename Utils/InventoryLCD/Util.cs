using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public class Util
    {
        static readonly List<MyTuple<string, string>> Gases = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Hydrogen","MyObjectBuilder_GasProperties/Hydrogen"),
            new MyTuple<string, string>("Oxygen","MyObjectBuilder_GasProperties/Oxygen"),
        };

        static readonly List<MyTuple<string, string>> Ores = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Cobalt Ore","MyObjectBuilder_Ore/Cobalt"),
            new MyTuple<string, string>("Gold Ore","MyObjectBuilder_Ore/Gold"),
            new MyTuple<string, string>("Ice","MyObjectBuilder_Ore/Ice"),
            new MyTuple<string, string>("Iron Ore","MyObjectBuilder_Ore/Iron"),
            new MyTuple<string, string>("Magnesium Ore","MyObjectBuilder_Ore/Magnesium"),
            new MyTuple<string, string>("Nickel Ore","MyObjectBuilder_Ore/Nickel"),
            new MyTuple<string, string>("Organic","MyObjectBuilder_Ore/Organic"),
            new MyTuple<string, string>("Platinum Ore","MyObjectBuilder_Ore/Platinum"),
            new MyTuple<string, string>("Scrap Metal","MyObjectBuilder_Ore/Scrap"),
            new MyTuple<string, string>("Silicon Ore","MyObjectBuilder_Ore/Silicon"),
            new MyTuple<string, string>("Silver Ore","MyObjectBuilder_Ore/Silver"),
            new MyTuple<string, string>("Stone","MyObjectBuilder_Ore/Stone"),
            new MyTuple<string, string>("Uranium Ore","MyObjectBuilder_Ore/Uranium"),
        };

        static readonly List<MyTuple<string, string>> Ingots = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Cobalt Ingot","MyObjectBuilder_Ingot/Cobalt"),
            new MyTuple<string, string>("Gold Ingot","MyObjectBuilder_Ingot/Gold"),
            new MyTuple<string, string>("Gravel","MyObjectBuilder_Ingot/Stone"),
            new MyTuple<string, string>("Iron Ingot","MyObjectBuilder_Ingot/Iron"),
            new MyTuple<string, string>("Magnesium Powder","MyObjectBuilder_Ingot/Magnesium"),
            new MyTuple<string, string>("Nickel Ingot","MyObjectBuilder_Ingot/Nickel"),
            new MyTuple<string, string>("Old Scrap Metal","MyObjectBuilder_Ingot/Scrap"),
            new MyTuple<string, string>("Platinum Ingot","MyObjectBuilder_Ingot/Platinum"),
            new MyTuple<string, string>("Prototech Scrap","MyObjectBuilder_Ingot/PrototechScrap"),
            new MyTuple<string, string>("Silicon Wafer","MyObjectBuilder_Ingot/Silicon"),
            new MyTuple<string, string>("Silver Ingot","MyObjectBuilder_Ingot/Silver"),
            new MyTuple<string, string>("Uranium Ingot","MyObjectBuilder_Ingot/Uranium"),
        };

        static readonly List<MyTuple<string, string>> Magazines = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("5.56x45mm NATO magazine","MyObjectBuilder_AmmoMagazine/NATO_5p56x45mm"),
            new MyTuple<string, string>("Artillery Shell","MyObjectBuilder_AmmoMagazine/LargeCalibreAmmo"),
            new MyTuple<string, string>("Assault Cannon Shell","MyObjectBuilder_AmmoMagazine/MediumCalibreAmmo"),
            new MyTuple<string, string>("Autocannon Magazine","MyObjectBuilder_AmmoMagazine/AutocannonClip"),
            new MyTuple<string, string>("Fireworks Blue","MyObjectBuilder_AmmoMagazine/FireworksBoxBlue"),
            new MyTuple<string, string>("Fireworks Green","MyObjectBuilder_AmmoMagazine/FireworksBoxGreen"),
            new MyTuple<string, string>("Fireworks Pink","MyObjectBuilder_AmmoMagazine/FireworksBoxPink"),
            new MyTuple<string, string>("Fireworks Rainbow","MyObjectBuilder_AmmoMagazine/FireworksBoxRainbow"),
            new MyTuple<string, string>("Fireworks Red","MyObjectBuilder_AmmoMagazine/FireworksBoxRed"),
            new MyTuple<string, string>("Fireworks Yellow","MyObjectBuilder_AmmoMagazine/FireworksBoxYellow"),
            new MyTuple<string, string>("Flare Gun Clip","MyObjectBuilder_AmmoMagazine/FlareClip"),
            new MyTuple<string, string>("Gatling Ammo Box","MyObjectBuilder_AmmoMagazine/NATO_25x184mm"),
            new MyTuple<string, string>("Large Railgun Sabot","MyObjectBuilder_AmmoMagazine/LargeRailgunAmmo"),
            new MyTuple<string, string>("MR-20 Rifle Magazine","MyObjectBuilder_AmmoMagazine/AutomaticRifleGun_Mag_20rd"),
            new MyTuple<string, string>("MR-30E Rifle Magazine","MyObjectBuilder_AmmoMagazine/UltimateAutomaticRifleGun_Mag_30rd"),
            new MyTuple<string, string>("MR-50A Rifle Magazine","MyObjectBuilder_AmmoMagazine/RapidFireAutomaticRifleGun_Mag_50rd"),
            new MyTuple<string, string>("MR-8P Rifle Magazine","MyObjectBuilder_AmmoMagazine/PreciseAutomaticRifleGun_Mag_5rd"),
            new MyTuple<string, string>("Rocket","MyObjectBuilder_AmmoMagazine/Missile200mm"),
            new MyTuple<string, string>("S-10 Pistol Magazine","MyObjectBuilder_AmmoMagazine/SemiAutoPistolMagazine"),
            new MyTuple<string, string>("S-10E Pistol Magazine","MyObjectBuilder_AmmoMagazine/ElitePistolMagazine"),
            new MyTuple<string, string>("S-20A Pistol Magazine","MyObjectBuilder_AmmoMagazine/FullAutoPistolMagazine"),
            new MyTuple<string, string>("Small Railgun Sabot","MyObjectBuilder_AmmoMagazine/SmallRailgunAmmo"),
        };

        static readonly List<MyTuple<string, string>> Components = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Bulletproof Glass","MyObjectBuilder_Component/BulletproofGlass"),
            new MyTuple<string, string>("Canvas","MyObjectBuilder_Component/Canvas"),
            new MyTuple<string, string>("Computer","MyObjectBuilder_Component/Computer"),
            new MyTuple<string, string>("Construction Comp.","MyObjectBuilder_Component/Construction"),
            new MyTuple<string, string>("Detector Comp.","MyObjectBuilder_Component/Detector"),
            new MyTuple<string, string>("Display","MyObjectBuilder_Component/Display"),
            new MyTuple<string, string>("Engineer Plushie","MyObjectBuilder_Component/EngineerPlushie"),
            new MyTuple<string, string>("Explosives","MyObjectBuilder_Component/Explosives"),
            new MyTuple<string, string>("Girder","MyObjectBuilder_Component/Girder"),
            new MyTuple<string, string>("Gravity Comp.","MyObjectBuilder_Component/GravityGenerator"),
            new MyTuple<string, string>("Interior Plate","MyObjectBuilder_Component/InteriorPlate"),
            new MyTuple<string, string>("Large Steel Tube","MyObjectBuilder_Component/LargeTube"),
            new MyTuple<string, string>("Medical Comp.","MyObjectBuilder_Component/Medical"),
            new MyTuple<string, string>("Metal Grid","MyObjectBuilder_Component/MetalGrid"),
            new MyTuple<string, string>("Motor","MyObjectBuilder_Component/Motor"),
            new MyTuple<string, string>("Power Cell","MyObjectBuilder_Component/PowerCell"),
            new MyTuple<string, string>("Prototech Capacitor","MyObjectBuilder_Component/PrototechCapacitor"),
            new MyTuple<string, string>("Prototech Circuitry","MyObjectBuilder_Component/PrototechCircuitry"),
            new MyTuple<string, string>("Prototech Cooling Unit","MyObjectBuilder_Component/PrototechCoolingUnit"),
            new MyTuple<string, string>("Prototech Frame","MyObjectBuilder_Component/PrototechFrame"),
            new MyTuple<string, string>("Prototech Machinery","MyObjectBuilder_Component/PrototechMachinery"),
            new MyTuple<string, string>("Prototech Panel","MyObjectBuilder_Component/PrototechPanel"),
            new MyTuple<string, string>("Prototech Propulsion Unit","MyObjectBuilder_Component/PrototechPropulsionUnit"),
            new MyTuple<string, string>("Radio-comm Comp.","MyObjectBuilder_Component/RadioCommunication"),
            new MyTuple<string, string>("Reactor Comp.","MyObjectBuilder_Component/Reactor"),
            new MyTuple<string, string>("Saberoid Plushie","MyObjectBuilder_Component/SabiroidPlushie"),
            new MyTuple<string, string>("Small Steel Tube","MyObjectBuilder_Component/SmallTube"),
            new MyTuple<string, string>("Solar Cell","MyObjectBuilder_Component/SolarCell"),
            new MyTuple<string, string>("Steel Plate","MyObjectBuilder_Component/SteelPlate"),
            new MyTuple<string, string>("Superconductor","MyObjectBuilder_Component/Superconductor"),
            new MyTuple<string, string>("Thruster Comp.","MyObjectBuilder_Component/Thrust"),
            new MyTuple<string, string>("Zone Chip","MyObjectBuilder_Component/ZoneChip"),
        };

        static readonly List<MyTuple<string, string>> Tools = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Elite Grinder","MyObjectBuilder_PhysicalGunObject/AngleGrinder4Item"),
            new MyTuple<string, string>("Elite Hand Drill","MyObjectBuilder_PhysicalGunObject/HandDrill4Item"),
            new MyTuple<string, string>("Elite Welder","MyObjectBuilder_PhysicalGunObject/Welder4Item"),
            new MyTuple<string, string>("Enhanced Grinder","MyObjectBuilder_PhysicalGunObject/AngleGrinder2Item"),
            new MyTuple<string, string>("Enhanced Hand Drill","MyObjectBuilder_PhysicalGunObject/HandDrill2Item"),
            new MyTuple<string, string>("Enhanced Welder","MyObjectBuilder_PhysicalGunObject/Welder2Item"),
            new MyTuple<string, string>("Flare Gun","MyObjectBuilder_PhysicalGunObject/FlareGunItem"),
            new MyTuple<string, string>("Grinder","MyObjectBuilder_PhysicalGunObject/AngleGrinderItem"),
            new MyTuple<string, string>("Hand Drill","MyObjectBuilder_PhysicalGunObject/HandDrillItem"),
            new MyTuple<string, string>("MR-20 Rifle","MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem"),
            new MyTuple<string, string>("MR-30E Rifle","MyObjectBuilder_PhysicalGunObject/UltimateAutomaticRifleItem"),
            new MyTuple<string, string>("MR-50A Rifle","MyObjectBuilder_PhysicalGunObject/RapidFireAutomaticRifleItem"),
            new MyTuple<string, string>("MR-8P Rifle","MyObjectBuilder_PhysicalGunObject/PreciseAutomaticRifleItem"),
            new MyTuple<string, string>("PRO-1 Rocket Launcher","MyObjectBuilder_PhysicalGunObject/AdvancedHandHeldLauncherItem"),
            new MyTuple<string, string>("Proficient Grinder","MyObjectBuilder_PhysicalGunObject/AngleGrinder3Item"),
            new MyTuple<string, string>("Proficient Hand Drill","MyObjectBuilder_PhysicalGunObject/HandDrill3Item"),
            new MyTuple<string, string>("Proficient Welder","MyObjectBuilder_PhysicalGunObject/Welder3Item"),
            new MyTuple<string, string>("RO-1 Rocket Launcher","MyObjectBuilder_PhysicalGunObject/BasicHandHeldLauncherItem"),
            new MyTuple<string, string>("S-10 Pistol","MyObjectBuilder_PhysicalGunObject/SemiAutoPistolItem"),
            new MyTuple<string, string>("S-10E Pistol","MyObjectBuilder_PhysicalGunObject/ElitePistolItem"),
            new MyTuple<string, string>("S-20A Pistol","MyObjectBuilder_PhysicalGunObject/FullAutoPistolItem"),
            new MyTuple<string, string>("Welder","MyObjectBuilder_PhysicalGunObject/WelderItem"),
        };

        static readonly List<MyTuple<string, string>> Bottles = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Hydrogen Bottle","MyObjectBuilder_GasContainerObject/HydrogenBottle"),
            new MyTuple<string, string>("Oxygen Bottle","MyObjectBuilder_OxygenContainerObject/OxygenBottle"),
        };

        static readonly List<MyTuple<string, string>> Others = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Clang Kola","MyObjectBuilder_ConsumableItem/ClangCola"),
            new MyTuple<string, string>("Cosmic Coffee","MyObjectBuilder_ConsumableItem/CosmicCoffee"),
            new MyTuple<string, string>("Datapad","MyObjectBuilder_Datapad/Datapad"),
            new MyTuple<string, string>("Medkit","MyObjectBuilder_ConsumableItem/Medkit"),
            new MyTuple<string, string>("Package","MyObjectBuilder_Package/Package"),
            new MyTuple<string, string>("Powerkit","MyObjectBuilder_ConsumableItem/Powerkit"),
            new MyTuple<string, string>("Space Credit","MyObjectBuilder_PhysicalObject/SpaceCredit"),
        };

        static public string GetKiloFormat(double value)
        {
            double pow = 1.0;
            string suffix = "";
            if (value > 1000.0)
            {
                int y = int.Parse(Math.Floor(Math.Log10(value) / 3).ToString());
                suffix = "KMGTPEZY".Substring(y - 1, 1);
                pow = Math.Pow(10, y * 3);
            }
            return string.Format("{0:0.0}{1}", (value / pow), suffix);

        }
      
        static public string GetType(MyInventoryItem item)
        {
            return item.Type.TypeId;
        }
        static public string GetName(MyInventoryItem item)
        {
            string type = item.Type.TypeId;
            string subType = item.Type.SubtypeId;

            string name = TranslateName(type, subType);
            if (string.IsNullOrWhiteSpace(name)) name = subType;
            return CutString(name, 25);
        }
        static public string TranslateName(string type, string subType)
        {
            string key = $"{type}/{subType}";
            if (type.Equals("MyObjectBuilder_Ore")) return Ores.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_Ingot")) return Ingots.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_GasProperties")) return Gases.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_Component")) return Components.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_PhysicalGunObject")) return Tools.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_AmmoMagazine")) return Magazines.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_ConsumableItem")) return Others.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_GasContainerObject")) return Bottles.Find(i => i.Item2.Equals(key)).Item1;
            if (type.Equals("MyObjectBuilder_OxygenContainerObject")) return Bottles.Find(i => i.Item2.Equals(key)).Item1;
            return subType;
        }
    
        static public string GetType(MyProductionItem item)
        {
            MyDefinitionId id;
            string stName = item.BlueprintId.SubtypeName;
            string tName = GetName(item);

            if ((stName.EndsWith("Rifle") || stName.StartsWith("Welder") || stName.StartsWith("HandDrill") || stName.StartsWith("AngleGrinder"))
                && MyDefinitionId.TryParse("MyObjectBuilder_PhysicalGunObject", tName, out id)) return id.TypeId.ToString();
            if (stName.StartsWith("Hydrogen") && MyDefinitionId.TryParse("MyObjectBuilder_GasContainerObject", tName, out id)) return id.TypeId.ToString();
            if (stName.StartsWith("Oxygen") && MyDefinitionId.TryParse("MyObjectBuilder_OxygenContainerObject", tName, out id)) return id.TypeId.ToString();
            if ((stName.Contains("Missile") || stName.EndsWith("Magazine")) && MyDefinitionId.TryParse("MyObjectBuilder_AmmoMagazine", tName, out id)) return id.TypeId.ToString();
            if (MyDefinitionId.TryParse("MyObjectBuilder_Component", tName, out id)) return id.TypeId.ToString();
            return item.BlueprintId.TypeId.ToString();
        }
        static public string GetName(MyProductionItem item)
        {
            string stName = item.BlueprintId.SubtypeName;
            if (stName.EndsWith("Component")) stName = stName.Replace("Component", "");
            if (stName.EndsWith("Rifle") || stName.StartsWith("Welder") || stName.StartsWith("HandDrill") || stName.StartsWith("AngleGrinder")) stName = stName + "Item";
            if (stName.EndsWith("Magazine")) stName = stName.Replace("Magazine", "");
            return stName;
        }
       
        static public string CutString(string value, int limit)
        {
            if (value.Length > limit)
            {
                int len = (limit - 3) / 2;
                return value.Substring(0, len) + "..." + value.Substring(value.Length - len, len);
            }
            return value;
        }
    }
}
