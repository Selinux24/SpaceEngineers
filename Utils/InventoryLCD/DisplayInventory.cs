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
        long volumes = 0;
        long maxVolumes = 0;

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        string filter = "*";
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

        public DisplayInventory(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni ini)
        {
            panel = ini.Get("Inventory", "panel").ToInt32(0);
            filter = ini.Get("Inventory", "filter").ToString("*");
            enable = ini.Get("Inventory", "on").ToBoolean(true);
            scale = ini.Get("Inventory", "scale").ToSingle(1f);

            gauge = ini.Get("Inventory", "gauge_on").ToBoolean(true);
            gaugeFullscreen = ini.Get("Inventory", "gauge_fullscreen").ToBoolean(true);
            gaugeHorizontal = ini.Get("Inventory", "gauge_horizontal").ToBoolean(true);
            gaugeWidth = ini.Get("Inventory", "gauge_width").ToSingle(80f);
            gaugeHeight = ini.Get("Inventory", "gauge_height").ToSingle(40f);
            gaugeShowPercent = ini.Get("Inventory", "gauge_show_percent").ToBoolean(true);

            item = ini.Get("Inventory", "item_on").ToBoolean(true);
            itemGauge = ini.Get("Inventory", "item_gauge_on").ToBoolean(true);
            itemSymbol = ini.Get("Inventory", "item_symbol_on").ToBoolean(true);
            itemSize = ini.Get("Inventory", "item_size").ToSingle(80f);
            itemOre = ini.Get("Inventory", "item_ore").ToBoolean(true);
            itemIngot = ini.Get("Inventory", "item_ingot").ToBoolean(true);
            itemComponent = ini.Get("Inventory", "item_component").ToBoolean(true);
            itemAmmo = ini.Get("Inventory", "item_ammo").ToBoolean(true);

            types.Clear();
            if (itemOre) types.Add(TYPE_ORE);
            if (itemIngot) types.Add(TYPE_INGOT);
            if (itemComponent) types.Add(TYPE_COMPONENT);
            if (itemAmmo) types.Add(TYPE_AMMO);

            Search();
        }
        public void Save(MyIni ini)
        {
            ini.Set("Inventory", "panel", panel);
            ini.Set("Inventory", "filter", filter);
            ini.Set("Inventory", "on", enable);
            ini.Set("Inventory", "scale", scale);

            ini.Set("Inventory", "gauge_on", gauge);
            ini.Set("Inventory", "gauge_fullscreen", gaugeFullscreen);
            ini.Set("Inventory", "gauge_horizontal", gaugeHorizontal);
            ini.Set("Inventory", "gauge_width", gaugeWidth);
            ini.Set("Inventory", "gauge_height", gaugeHeight);
            ini.Set("Inventory", "gauge_show_percent", gaugeShowPercent);

            ini.Set("Inventory", "item_on", item);
            ini.Set("Inventory", "item_gauge_on", itemGauge);
            ini.Set("Inventory", "item_symbol_on", itemSymbol);
            ini.Set("Inventory", "item_size", itemSize);
            ini.Set("Inventory", "item_ore", itemOre);
            ini.Set("Inventory", "item_ingot", itemIngot);
            ini.Set("Inventory", "item_component", itemComponent);
            ini.Set("Inventory", "item_ammo", itemAmmo);
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
            var style = new StyleGauge()
            {
                Orientation = gaugeHorizontal ? SpriteOrientation.Horizontal : SpriteOrientation.Vertical,
                Fullscreen = gaugeFullscreen,
                Width = gaugeWidth,
                Height = gaugeHeight,
                Thresholds = program.Config.ChestThresholds,
                ColorSoftening = .6f,
                Percent = gaugeShowPercent,
            };

            style.Scale(scale);
            drawing.Position = drawing.DrawGauge(drawing.Position, volumes, maxVolumes, style);
            if (gaugeHorizontal)
            {
                drawing.Position += new Vector2(0, 2 * cellSpacing * scale);
            }
        }
        void DisplayByType(SurfaceDrawing drawing)
        {
            int count = 0;
            float height = itemSize;
            float width = 2.5f * itemSize;
            float deltaWidth = width * scale;
            float deltaHeight = height * scale;
            int limit = GetLimit(drawing, deltaHeight, cellSpacing);
            string colorDefault = program.Config.Get("color", "default");
            int limitDefault = program.Config.GetInt("Limit", "default");

            foreach (string type in types)
            {
                var entryList = itemList.OrderByDescending(entry => entry.Value.Amount).Where(entry => entry.Value.Type == type);

                foreach (var entry in entryList)
                {
                    var item = entry.Value;

                    // Icon
                    var style = new StyleIcon()
                    {
                        Path = item.Icon,
                        Width = width,
                        Height = height,
                        Color = program.Config.GetColor("color", item.Name, item.Data, colorDefault),
                        Thresholds = program.Config.ItemThresholds,
                        ColorSoftening = .6f
                    };
                    style.Scale(scale);

                    var p = drawing.Position + new Vector2((cellSpacing + deltaWidth) * (count / limit), (cellSpacing + deltaHeight) * (count - (count / limit) * limit));
                    int l = itemGauge ? program.Config.GetInt("Limit", item.Name, limitDefault) : 0;
                    Variances v = GetVariance(entry.Key, item.Amount);
                    drawing.DrawGaugeIcon(p, item.Name, item.Amount, l, style, itemGauge, itemSymbol, v);

                    count++;
                }
            }

            if (itemList.Count > limit)
            {
                drawing.Position += new Vector2(0, (cellSpacing * scale + height) * limit);
            }
            drawing.Position += new Vector2(0, (cellSpacing * scale + height) * itemList.Count);
        }
        int GetLimit(SurfaceDrawing drawing, float itemSize, float cellSpacing)
        {
            int limit;
            if (gauge && gaugeHorizontal)
            {
                limit = (int)Math.Floor((drawing.Height - (gaugeHeight + topPadding) * scale) / (itemSize + cellSpacing));
            }
            else
            {
                limit = (int)Math.Floor((drawing.Height - topPadding * scale) / (itemSize + cellSpacing));
            }
            return Math.Max(limit, 1);
        }
        Variances GetVariance(string key, double amount)
        {
            if (!lastAmount.ContainsKey(key) || lastAmount[key] < amount) return Variances.Ascending;
            if (lastAmount[key] > amount) return Variances.Descending;
            return Variances.NoVariance;
        }
    }
}
