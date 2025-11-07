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
        const string Weights = "KMGTPEZY";
        const string StrComponent = "MyObjectBuilder_Component";
        const string StrPhysicalGunObject = "MyObjectBuilder_PhysicalGunObject";
        const string StrAmmoMagazine = "MyObjectBuilder_AmmoMagazine";
        const string StrGasContainerObject = "MyObjectBuilder_GasContainerObject";
        const string StrOxygenContainerObject = "MyObjectBuilder_OxygenContainerObject";

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
        static readonly List<MyTuple<string, string>> Icons = new List<MyTuple<string, string>>()
        {
            new MyTuple<string, string>("Position0090_AdvancedHandHeldLauncher", "AdvancedHandHeldLauncherItem"),
            new MyTuple<string, string>("Position0020_AngleGrinder2", "AngleGrinder2Item"),
            new MyTuple<string, string>("Position0030_AngleGrinder3", "AngleGrinder3Item"),
            new MyTuple<string, string>("Position0040_AngleGrinder4", "AngleGrinder4Item"),
            new MyTuple<string, string>("Position0010_AngleGrinder", "AngleGrinderItem"),
            new MyTuple<string, string>("Position0090_AutocannonClip", "AutocannonClip"),
            new MyTuple<string, string>("Position0040_AutomaticRifleGun_Mag_20rd", "AutomaticRifleGun_Mag_20rd"),
            new MyTuple<string, string>("Position0040_AutomaticRifle", "AutomaticRifleItem"),
            new MyTuple<string, string>("Position0080_BasicHandHeldLauncher", "BasicHandHeldLauncherItem"),
            new MyTuple<string, string>("Position0030_Canvas", "Canvas"),
            new MyTuple<string, string>("ComputerComponent", "Computer"),
            new MyTuple<string, string>("ConstructionComponent", "Construction"),
            new MyTuple<string, string>("Position0040_Datapad", "Datapad"),
            new MyTuple<string, string>("DetectorComponent", "Detector"),
            new MyTuple<string, string>("Position0030_EliteAutoPistol", "ElitePistolItem"),
            new MyTuple<string, string>("Position0030_ElitePistolMagazine", "ElitePistolMagazine"),
            new MyTuple<string, string>("ExplosivesComponent", "Explosives"),
            new MyTuple<string, string>("Position0060_FireworksBoxBlue", "FireworksBoxBlue"),
            new MyTuple<string, string>("Position0061_FireworksBoxGreen", "FireworksBoxGreen"),
            new MyTuple<string, string>("Position0064_FireworksBoxPink", "FireworksBoxPink"),
            new MyTuple<string, string>("Position0065_FireworksBoxRainbow", "FireworksBoxRainbow"),
            new MyTuple<string, string>("Position0062_FireworksBoxRed", "FireworksBoxRed"),
            new MyTuple<string, string>("Position0063_FireworksBoxYellow", "FireworksBoxYellow"),
            new MyTuple<string, string>("Position0005_FlareGunMagazine", "FlareClip"),
            new MyTuple<string, string>("Position0005_FlareGun", "FlareGunItem"),
            new MyTuple<string, string>("Position0020_FullAutoPistol", "FullAutoPistolItem"),
            new MyTuple<string, string>("Position0020_FullAutoPistolMagazine", "FullAutoPistolMagazine"),
            new MyTuple<string, string>("GirderComponent", "Girder"),
            new MyTuple<string, string>("GravityGeneratorComponent", "GravityGenerator"),
            new MyTuple<string, string>("Position0060_HandDrill2", "HandDrill2Item"),
            new MyTuple<string, string>("Position0070_HandDrill3", "HandDrill3Item"),
            new MyTuple<string, string>("Position0080_HandDrill4", "HandDrill4Item"),
            new MyTuple<string, string>("Position0050_HandDrill", "HandDrillItem"),
            new MyTuple<string, string>("Position0020_HydrogenBottle", "HydrogenBottle"),
            new MyTuple<string, string>("Position0120_LargeCalibreAmmo", "LargeCalibreAmmo"),
            new MyTuple<string, string>("Position0140_LargeRailgunAmmo", "LargeRailgunAmmo"),
            new MyTuple<string, string>("MedicalComponent", "Medical"),
            new MyTuple<string, string>("Position0110_MediumCalibreAmmo", "MediumCalibreAmmo"),
            new MyTuple<string, string>("Position0100_Missile200mm", "Missile200mm"),
            new MyTuple<string, string>("MotorComponent", "Motor"),
            new MyTuple<string, string>("Position0080_NATO_25x184mmMagazine", "NATO_25x184mm"),
            new MyTuple<string, string>("PlatinumOreToIngot", "Nickel"),
            new MyTuple<string, string>("Position0010_OxygenBottle", "OxygenBottle"),
            new MyTuple<string, string>("Position0060_PreciseAutomaticRifleGun_Mag_5rd", "PreciseAutomaticRifleGun_Mag_5rd"),
            new MyTuple<string, string>("Position0060_PreciseAutomaticRifle", "PreciseAutomaticRifleItem"),
            new MyTuple<string, string>("RadioCommunicationComponent", "RadioCommunication"),
            new MyTuple<string, string>("Position0050_RapidFireAutomaticRifleGun_Mag_50rd", "RapidFireAutomaticRifleGun_Mag_50rd"),
            new MyTuple<string, string>("Position0050_RapidFireAutomaticRifle", "RapidFireAutomaticRifleItem"),
            new MyTuple<string, string>("ReactorComponent", "Reactor"),
            new MyTuple<string, string>("Position0010_SemiAutoPistol", "SemiAutoPistolItem"),
            new MyTuple<string, string>("Position0010_SemiAutoPistolMagazine", "SemiAutoPistolMagazine"),
            new MyTuple<string, string>("Position0130_SmallRailgunAmmo", "SmallRailgunAmmo"),
            new MyTuple<string, string>("ThrustComponent", "Thrust"),
            new MyTuple<string, string>("Position0070_UltimateAutomaticRifleGun_Mag_30rd", "UltimateAutomaticRifleGun_Mag_30rd"),
            new MyTuple<string, string>("Position0070_UltimateAutomaticRifle", "UltimateAutomaticRifleItem"),
            new MyTuple<string, string>("Position0100_Welder2", "Welder2Item"),
            new MyTuple<string, string>("Position0110_Welder3", "Welder3Item"),
            new MyTuple<string, string>("Position0120_Welder4", "Welder4Item"),
            new MyTuple<string, string>("Position0090_Welder", "WelderItem"),
        };

        public static string GetKiloFormat(double value)
        {
            if (value <= 1000.0) return $"{value:0.0}";

            int y = int.Parse(Math.Floor(Math.Log10(value) / 3).ToString());
            double pow = Math.Pow(10, y * 3);
            string suffix = Weights.Substring(y - 1, 1);
            return $"{value / pow:0.0}{suffix}";
        }
        public static string CutString(string value, int limit)
        {
            if (value.Length <= limit) return value;

            int len = (limit - 3) / 2;
            return value.Substring(0, len) + "..." + value.Substring(value.Length - len, len);
        }

        public static string GetType(MyInventoryItem item)
        {
            return item.Type.TypeId;
        }
        public static string GetName(MyInventoryItem item)
        {
            string type = item.Type.TypeId;
            string subType = item.Type.SubtypeId;

            string name = TranslateName(type, subType);
            if (string.IsNullOrWhiteSpace(name)) name = subType;
            return CutString(name, 25);
        }
        static string TranslateName(string type, string subType)
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

        public static string GetType(MyProductionItem item)
        {
            string tName = GetTypeName(item.BlueprintId.SubtypeName);
            string name = GetName(item);
            MyDefinitionId id;
            if (MyDefinitionId.TryParse(tName, name, out id)) return id.TypeId.ToString();

            return item.BlueprintId.TypeId.ToString();
        }
        public static string GetName(MyProductionItem item)
        {
            string stName = item.BlueprintId.SubtypeName;
            if (stName.EndsWith("Component")) stName = stName.Replace("Component", "");
            if (stName.EndsWith("Rifle") || stName.StartsWith("Welder") || stName.StartsWith("HandDrill") || stName.StartsWith("AngleGrinder")) stName = stName + "Item";
            if (stName.EndsWith("Magazine")) stName = stName.Replace("Magazine", "");
            return stName;
        }
        public static string GetData(MyDefinitionId id)
        {
            if (Icons.Exists(i => i.Item1 == id.SubtypeName)) return Icons.Find(i => i.Item1 == id.SubtypeName).Item2;

            return id.SubtypeName;
        }

        static string GetTypeName(string name)
        {
            if (IsPhysicalGunObject(name)) return StrPhysicalGunObject;
            if (IsAmmoMagazine(name)) return StrAmmoMagazine;
            if (IsGasContainerObject(name)) return StrGasContainerObject;
            if (IsOxygenContainerObject(name)) return StrOxygenContainerObject;
            return StrComponent;
        }
        static bool IsPhysicalGunObject(string name)
        {
            return
                name.EndsWith("Rifle") ||
                name.StartsWith("Welder") ||
                name.StartsWith("HandDrill") ||
                name.StartsWith("AngleGrinder");
        }
        static bool IsAmmoMagazine(string name)
        {
            return
                name.Contains("Ammo") ||
                name.Contains("Missile") ||
                name.EndsWith("Magazine") ||
                name.Contains("_Mag_") ||
                name.EndsWith("Clip") ||
                name.Contains("Fireworks");
        }
        static bool IsGasContainerObject(string name)
        {
            return name.EndsWith("HydrogenBottle");
        }
        static bool IsOxygenContainerObject(string name)
        {
            return name.EndsWith("OxygenBottle");
        }
    }
}
