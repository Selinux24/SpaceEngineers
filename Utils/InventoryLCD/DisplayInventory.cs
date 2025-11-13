using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class DisplayInventory
    {
        const string SECTION = "Inventory";
        const string SECTION_ITEM_THRESHOLDS = "InventoryItemThresholds";
        const string SECTION_CHEST_THRESHOLDS = "InventoryChestThresholds";
        const string SECTION_COLORS = "InventoryColor";
        const string SECTION_LIMITS = "InventoryLimit";

        const string TYPE_ORE = "MyObjectBuilder_Ore";
        const string TYPE_INGOT = "MyObjectBuilder_Ingot";
        const string TYPE_COMPONENT = "MyObjectBuilder_Component";
        const string TYPE_AMMO = "MyObjectBuilder_AmmoMagazine";

        readonly Program program;
        readonly DisplayLcd displayLcd;

        readonly float topPadding = 10f;
        readonly float cellSpacing = 2f;
        readonly Dictionary<string, Item> itemList = new Dictionary<string, Item>();
        readonly Dictionary<string, double> lastAmount = new Dictionary<string, double>();
        readonly List<string> types = new List<string>();
        readonly BlockSystem<IMyTerminalBlock> inventories = new BlockSystem<IMyTerminalBlock>();

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        string filter = "*";
        float padding = 2f;
        float margin = 2f;

        bool gauge = true;
        bool gaugeFullscreen = true;
        bool gaugeHorizontal = true;
        bool gaugeShowPercent = true;
        float gaugeWidth = 80f;
        float gaugeHeight = 40f;

        bool item = true;
        bool itemGauge = true;
        bool itemSymbol = true;
        float itemSize = 80f;
        bool itemOre = true;
        bool itemIngot = true;
        bool itemComponent = true;
        bool itemAmmo = true;
        GaugeThresholds itemThresholds;
        GaugeThresholds chestThresholds;
        ColorDefaults colorDefaults;
        Limits limits;

        StyleGauge styleGauge;
        float iconWidth;
        float iconHeight;
        StyleIcon styleIcon;
        string cDefault;
        int lDefault;

        long volumes = 0;
        long maxVolumes = 0;

        public DisplayInventory(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni ini)
        {
            if (!ini.ContainsSection(SECTION)) return;

            panel = ini.Get(SECTION, "panel").ToInt32(0);
            filter = ini.Get(SECTION, "filter").ToString("*");
            enable = ini.Get(SECTION, "on").ToBoolean(true);
            scale = ini.Get(SECTION, "scale").ToSingle(1f);
            padding = ini.Get(SECTION, "padding").ToSingle(2f);
            margin = ini.Get(SECTION, "margin").ToSingle(2f);

            gauge = ini.Get(SECTION, "gauge_on").ToBoolean(true);
            gaugeFullscreen = ini.Get(SECTION, "gauge_fullscreen").ToBoolean(true);
            gaugeHorizontal = ini.Get(SECTION, "gauge_horizontal").ToBoolean(true);
            gaugeWidth = ini.Get(SECTION, "gauge_width").ToSingle(80f);
            gaugeHeight = ini.Get(SECTION, "gauge_height").ToSingle(40f);
            gaugeShowPercent = ini.Get(SECTION, "gauge_show_percent").ToBoolean(true);

            item = ini.Get(SECTION, "item_on").ToBoolean(true);
            itemGauge = ini.Get(SECTION, "item_gauge_on").ToBoolean(true);
            itemSymbol = ini.Get(SECTION, "item_symbol_on").ToBoolean(true);
            itemSize = ini.Get(SECTION, "item_size").ToSingle(80f);
            itemOre = ini.Get(SECTION, "item_ore").ToBoolean(true);
            itemIngot = ini.Get(SECTION, "item_ingot").ToBoolean(true);
            itemComponent = ini.Get(SECTION, "item_component").ToBoolean(true);
            itemAmmo = ini.Get(SECTION, "item_ammo").ToBoolean(true);

            itemThresholds = GaugeThresholds.LoadThresholds(ini, SECTION_ITEM_THRESHOLDS);
            if (itemThresholds == null) itemThresholds = GaugeThresholds.DefaultItemThesholds();

            chestThresholds = GaugeThresholds.LoadThresholds(ini, SECTION_CHEST_THRESHOLDS);
            if (chestThresholds == null) chestThresholds = GaugeThresholds.DefaultChestThesholds();

            colorDefaults = new ColorDefaults(ini, SECTION_COLORS);
            colorDefaults.Load();
            cDefault = colorDefaults.GetDefault();

            limits = new Limits(ini, SECTION_LIMITS);
            limits.Load();
            lDefault = limits.GetInt("default");

            types.Clear();
            if (itemOre) types.Add(TYPE_ORE);
            if (itemIngot) types.Add(TYPE_INGOT);
            if (itemComponent) types.Add(TYPE_COMPONENT);
            if (itemAmmo) types.Add(TYPE_AMMO);

            styleGauge = new StyleGauge()
            {
                Orientation = gaugeHorizontal ? SpriteOrientation.Horizontal : SpriteOrientation.Vertical,
                Fullscreen = gaugeFullscreen,
                Width = gaugeWidth,
                Height = gaugeHeight,
                Thresholds = chestThresholds,
                ColorSoftening = .6f,
                Percent = gaugeShowPercent,
                Padding = new StylePadding(padding),
                Margin = new StyleMargin(margin),
            };
            styleGauge.Scale(scale);

            iconWidth = 2.5f * itemSize;
            iconHeight = itemSize;

            styleIcon = new StyleIcon()
            {
                Width = iconWidth,
                Height = iconHeight,
                Thresholds = itemThresholds,
                ColorSoftening = .6f,
                Padding = new StylePadding(padding),
                Margin = new StyleMargin(margin),
            };
            styleIcon.Scale(scale);

            Search();
        }
        public void Save(MyIni ini)
        {
            ini.Set(SECTION, "panel", panel);
            ini.Set(SECTION, "filter", filter);
            ini.Set(SECTION, "on", enable);
            ini.Set(SECTION, "scale", scale);
            ini.Set(SECTION, "padding", padding);
            ini.Set(SECTION, "margin", margin);

            ini.Set(SECTION, "gauge_on", gauge);
            ini.Set(SECTION, "gauge_fullscreen", gaugeFullscreen);
            ini.Set(SECTION, "gauge_horizontal", gaugeHorizontal);
            ini.Set(SECTION, "gauge_width", gaugeWidth);
            ini.Set(SECTION, "gauge_height", gaugeHeight);
            ini.Set(SECTION, "gauge_show_percent", gaugeShowPercent);

            ini.Set(SECTION, "item_on", item);
            ini.Set(SECTION, "item_gauge_on", itemGauge);
            ini.Set(SECTION, "item_symbol_on", itemSymbol);
            ini.Set(SECTION, "item_size", itemSize);
            ini.Set(SECTION, "item_ore", itemOre);
            ini.Set(SECTION, "item_ingot", itemIngot);
            ini.Set(SECTION, "item_component", itemComponent);
            ini.Set(SECTION, "item_ammo", itemAmmo);

            GaugeThresholds.SaveThresholds(ini, itemThresholds, SECTION_ITEM_THRESHOLDS);
            GaugeThresholds.SaveThresholds(ini, chestThresholds, SECTION_CHEST_THRESHOLDS);
            colorDefaults?.Save();
            limits?.Save();
        }

        void Search()
        {
            var blockFilter = BlockFilter<IMyTerminalBlock>.Create(displayLcd.Block, filter, true);
            BlockSystem<IMyTerminalBlock>.SearchByFilter(program, inventories, blockFilter);
        }

        public void Draw(Drawing drawing)
        {
            if (!enable) return;

            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();

            if (gauge)
            {
                InventoryVolumes();
                DisplayGauge(surface);
            }
            else
            {
                surface.Position += new Vector2(0, topPadding);
            }

            if (!item) return;
            if (types.Count == 0) return;

            InventoryCount();
            DisplayByType(surface);
        }
        void InventoryVolumes()
        {
            volumes = 0;
            maxVolumes = 1;
            inventories.ForEach(block =>
            {
                for (int i = 0; i < block.InventoryCount; i++)
                {
                    var inv = block.GetInventory(i);
                    volumes += inv.CurrentVolume.RawValue;
                    maxVolumes += inv.MaxVolume.RawValue;
                }
            });
        }
        void InventoryCount()
        {
            lastAmount.Clear();
            foreach (var item in itemList)
            {
                lastAmount.Add(item.Key, item.Value.Amount);
            }

            itemList.Clear();
            foreach (var block in inventories.List)
            {
                for (int i = 0; i < block.InventoryCount; i++)
                {
                    var inv = block.GetInventory(i);
                    var items = new List<MyInventoryItem>();
                    inv.GetItems(items);

                    foreach (var item in items)
                    {
                        string type = Util.GetType(item);
                        string name = Util.GetName(item);
                        string data = item.Type.SubtypeId;
                        double amount = 0;
                        double.TryParse(item.Amount.ToString(), out amount);

                        string key = $"{type}_{name}";
                        if (itemList.ContainsKey(key))
                        {
                            itemList[key].Amount += amount;
                        }
                        else
                        {
                            itemList.Add(key, new Item()
                            {
                                Type = type,
                                Name = name,
                                Data = data,
                                Amount = amount
                            });
                        }
                    }
                }
            }
        }

        void DisplayGauge(SurfaceDrawing drawing)
        {
            drawing.Position = drawing.DrawGauge(styleGauge, drawing.Position, volumes, maxVolumes);
            if (gaugeHorizontal)
            {
                drawing.Position += new Vector2(0, 2 * cellSpacing * scale);
            }
        }
        void DisplayByType(SurfaceDrawing drawing)
        {
            int count = 0;
            float deltaWidth = iconWidth * scale;
            float deltaHeight = iconHeight * scale;
            int limit = GetLimit(drawing, deltaHeight, cellSpacing);

            foreach (string type in types)
            {
                var entryList = itemList.OrderByDescending(entry => entry.Value.Amount).Where(entry => entry.Value.Type == type);

                foreach (var entry in entryList)
                {
                    var item = entry.Value;

                    var p = drawing.Position + new Vector2((cellSpacing + deltaWidth) * (count / limit), (cellSpacing + deltaHeight) * (count - (count / limit) * limit));
                    styleIcon.Path = item.Icon;
                    styleIcon.Color = colorDefaults.GetColor(item.Name, item.Data, cDefault);
                    int l = itemGauge ? limits.GetInt(item.Name, lDefault) : 0;
                    var v = GetVariance(entry.Key, item.Amount);
                    drawing.DrawGaugeIcon(styleIcon, p, item.Name, item.Amount, l, itemGauge, itemSymbol, v);

                    count++;
                }
            }

            if (itemList.Count > limit)
            {
                drawing.Position += new Vector2(0, (cellSpacing * scale + iconHeight) * limit);
            }
            drawing.Position += new Vector2(0, (cellSpacing * scale + iconHeight) * itemList.Count);
        }
        int GetLimit(SurfaceDrawing drawing, float itemSize, float cellSpacing)
        {
            float v;
            if (gauge && gaugeHorizontal)
            {
                v = (drawing.Height - (gaugeHeight + topPadding) * scale) / (itemSize + cellSpacing);
            }
            else
            {
                v = (drawing.Height - topPadding * scale) / (itemSize + cellSpacing);
            }
            return Math.Max((int)Math.Floor(v), 1);
        }
        Variances GetVariance(string key, double amount)
        {
            if (!lastAmount.ContainsKey(key) || lastAmount[key] < amount) return Variances.Ascending;
            if (lastAmount[key] > amount) return Variances.Descending;
            return Variances.NoVariance;
        }
    }
}
