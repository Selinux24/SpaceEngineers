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
        readonly Program program;
        readonly DisplayLcd displayLcd;

        readonly float topPadding = 10f;
        readonly float cellSpacing = 2f;
        readonly Dictionary<string, Item> itemList = new Dictionary<string, Item>();
        readonly Dictionary<string, double> lastAmount = new Dictionary<string, double>();
        readonly List<string> types = new List<string>();

        int panel = 0;
        bool enable = false;
        bool search = true;
        float scale = 1f;
        string filter = "*";
        bool gauge = true;
        bool gaugeFullscreen = true;
        bool gaugeHorizontal = true;
        float gaugeWidth = 80f;
        float gaugeHeight = 40f;
        bool item = true;
        bool itemGauge = true;
        float itemSize = 80f;
        bool itemOre = true;
        bool itemIngot = true;
        bool itemComponent = true;
        bool itemAmmo = true;
        BlockSystem<IMyTerminalBlock> inventories = null;

        public DisplayInventory(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni MyIni)
        {
            panel = MyIni.Get("Inventory", "panel").ToInt32(0);
            filter = MyIni.Get("Inventory", "filter").ToString("*");
            enable = MyIni.Get("Inventory", "on").ToBoolean(true);
            scale = MyIni.Get("Inventory", "scale").ToSingle(1f);

            gauge = MyIni.Get("Inventory", "gauge_on").ToBoolean(true);
            gaugeFullscreen = MyIni.Get("Inventory", "gauge_fullscreen").ToBoolean(true);
            gaugeHorizontal = MyIni.Get("Inventory", "gauge_horizontal").ToBoolean(true);
            gaugeWidth = MyIni.Get("Inventory", "gauge_width").ToSingle(80f);
            gaugeHeight = MyIni.Get("Inventory", "gauge_height").ToSingle(40f);

            item = MyIni.Get("Inventory", "item_on").ToBoolean(true);
            itemGauge = MyIni.Get("Inventory", "item_gauge_on").ToBoolean(true);
            itemSize = MyIni.Get("Inventory", "item_size").ToSingle(80f);
            itemOre = MyIni.Get("Inventory", "item_ore").ToBoolean(true);
            itemIngot = MyIni.Get("Inventory", "item_ingot").ToBoolean(true);
            itemComponent = MyIni.Get("Inventory", "item_component").ToBoolean(true);
            itemAmmo = MyIni.Get("Inventory", "item_ammo").ToBoolean(true);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Inventory", "panel", panel);
            MyIni.Set("Inventory", "filter", filter);
            MyIni.Set("Inventory", "on", enable);
            MyIni.Set("Inventory", "scale", scale);

            MyIni.Set("Inventory", "gauge_on", gauge);
            MyIni.Set("Inventory", "gauge_fullscreen", gaugeFullscreen);
            MyIni.Set("Inventory", "gauge_horizontal", gaugeHorizontal);
            MyIni.Set("Inventory", "gauge_width", gaugeWidth);
            MyIni.Set("Inventory", "gauge_height", gaugeHeight);

            MyIni.Set("Inventory", "item_on", item);
            MyIni.Set("Inventory", "item_gauge_on", itemGauge);
            MyIni.Set("Inventory", "item_size", itemSize);
            MyIni.Set("Inventory", "item_ore", itemOre);
            MyIni.Set("Inventory", "item_ingot", itemIngot);
            MyIni.Set("Inventory", "item_component", itemComponent);
            MyIni.Set("Inventory", "item_ammo", itemAmmo);
        }

        void Search()
        {
            var blockFilter = BlockFilter<IMyTerminalBlock>.Create(displayLcd.Block, filter);
            blockFilter.HasInventory = true;
            inventories = BlockSystem<IMyTerminalBlock>.SearchByFilter(program, blockFilter);
            search = false;
        }

        public void Draw(Drawing drawing)
        {
            if (!enable) return;
            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();
            Draw(surface);
        }
        void Draw(SurfaceDrawing surface)
        {
            if (!enable) return;
            if (search) Search();

            if (gauge)
            {
                DisplayGauge(surface);
            }
            else
            {
                surface.Position += new Vector2(0, topPadding);
            }

            if (!item) return;

            types.Clear();
            if (itemOre) types.Add(Item.TYPE_ORE);
            if (itemIngot) types.Add(Item.TYPE_INGOT);
            if (itemComponent) types.Add(Item.TYPE_COMPONENT);
            if (itemAmmo) types.Add(Item.TYPE_AMMO);

            lastAmount.Clear();
            foreach (var entry in itemList)
            {
                lastAmount.Add(entry.Key, entry.Value.Amount);
            }

            InventoryCount();
            DisplayByType(surface, types);
        }
        void InventoryCount()
        {
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
            long volumes = 0;
            long maxVolumes = 1;
            inventories.ForEach(block =>
            {
                for (int i = 0; i < block.InventoryCount; i++)
                {
                    var inv = block.GetInventory(i);
                    volumes += inv.CurrentVolume.RawValue;
                    maxVolumes += inv.MaxVolume.RawValue;
                }
            });

            var style = new StyleGauge()
            {
                Orientation = gaugeHorizontal ? SpriteOrientation.Horizontal : SpriteOrientation.Vertical,
                Fullscreen = gaugeFullscreen,
                Width = gaugeWidth,
                Height = gaugeHeight,
                Thresholds = program.MyProperty.ChestThresholds,
                ColorSoftening = .6f
            };

            style.Scale(scale);
            drawing.Position = drawing.DrawGauge(drawing.Position, volumes, maxVolumes, style);
            if (gaugeHorizontal)
            {
                drawing.Position += new Vector2(0, 2 * cellSpacing * scale);
            }
        }
        void DisplayByType(SurfaceDrawing drawing, List<string> types)
        {
            int count = 0;
            float height = itemSize;
            float width = 2.5f * itemSize;
            float deltaWidth = width * scale;
            float deltaHeight = height * scale;
            int limit = GetLimit(drawing, deltaHeight, cellSpacing);
            string colorDefault = program.MyProperty.Get("color", "default");
            int limitDefault = program.MyProperty.GetInt("Limit", "default");

            foreach (string type in types)
            {
                foreach (KeyValuePair<string, Item> entry in itemList.OrderByDescending(entry => entry.Value.Amount).Where(entry => entry.Value.Type == type))
                {
                    var item = entry.Value;

                    int limitBar = 0;
                    int variance = 2;
                    if (itemGauge)
                    {
                        limitBar = program.MyProperty.GetInt("Limit", item.Name, limitDefault);

                        if (lastAmount.ContainsKey(entry.Key))
                        {
                            if (lastAmount[entry.Key] < item.Amount) variance = 1;
                            if (lastAmount[entry.Key] > item.Amount) variance = 3;
                        }
                        else
                        {
                            variance = 1;
                        }
                    }

                    // Icon
                    var style = new StyleIcon()
                    {
                        Path = item.Icon,
                        Width = width,
                        Height = height,
                        Color = program.MyProperty.GetColor("color", item.Name, item.Data, colorDefault),
                        Thresholds = program.MyProperty.ItemThresholds,
                        ColorSoftening = .6f
                    };
                    style.Scale(scale);

                    var p = drawing.Position + new Vector2((cellSpacing + deltaWidth) * (count / limit), (cellSpacing + deltaHeight) * (count - (count / limit) * limit));
                    drawing.DrawGaugeIcon(p, item.Name, item.Amount, limitBar, style, itemGauge, variance);

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
                limit = (int)Math.Floor((drawing.Viewport.Height - (gaugeHeight + topPadding) * scale) / (itemSize + cellSpacing));
            }
            else
            {
                limit = (int)Math.Floor((drawing.Viewport.Height - topPadding * scale) / (itemSize + cellSpacing));
            }
            return Math.Max(limit, 1);
        }
    }
}
