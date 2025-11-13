using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    class DisplayMachine
    {
        const string SECTION = "Machine";
        const string SECTION_MACHINE_COLOR = "MachineColor";

        readonly Program program;
        readonly DisplayLcd displayLcd;

        readonly Dictionary<long, Dictionary<string, double>> lastMachineAmount = new Dictionary<long, Dictionary<string, double>>();
        readonly List<string> types = new List<string>();
        readonly BlockSystem<IMyProductionBlock> producers = new BlockSystem<IMyProductionBlock>();
        readonly List<Item> items = new List<Item>();
        readonly List<MyProductionItem> productionItems = new List<MyProductionItem>();
        readonly List<MyInventoryItem> inventoryItems = new List<MyInventoryItem>();
        readonly Color colorTitle = new Color(100, 100, 100, 128);
        readonly Color colorText = new Color(100, 100, 100, 255);
        readonly Color colorAscending = new Color(0, 0, 20, 200);
        readonly Color colorNoVariance = new Color(20, 0, 0, 200);
        readonly Color colorDescending = new Color(0, 20, 0, 200);
        readonly Dictionary<string, string> symbol = new Dictionary<string, string>();

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        string filter = "*";
        float padding = 0f;
        float margin = 0f;
        bool machineRefinery = false;
        bool machineAssembler = false;
        int rows = 6;
        int columns = 0;
        float width = 250;
        float height = 120;
        int stringLen = 20;
        int maxItems = 3;
        Style style = new Style();
        ColorDefaults colorDefaults;

        string cDefault;
        float sizeIcon;
        float rotationOrScale;
        float cellSpacing;
        float formWidth;
        float formHeight;

        public DisplayMachine(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;

            symbol.Add("Cobalt", "Co");
            symbol.Add("Nickel", "Ni");
            symbol.Add("Magnesium", "Mg");
            symbol.Add("Platinum", "Pt");
            symbol.Add("Iron", "Fe");
            symbol.Add("Gold", "Au");
            symbol.Add("Silicon", "Si");
            symbol.Add("Silver", "Ag");
            symbol.Add("Stone", "Stone");
            symbol.Add("Uranium", "U");
            symbol.Add("Ice", "Ice");
        }

        public void Load(MyIni ini)
        {
            if (!ini.ContainsSection(SECTION)) return;

            panel = ini.Get(SECTION, "panel").ToInt32(0);
            enable = ini.Get(SECTION, "on").ToBoolean(false);
            filter = ini.Get(SECTION, "filter").ToString("*");
            scale = ini.Get(SECTION, "scale").ToSingle(1f);
            padding = ini.Get(SECTION, "padding").ToSingle(0f);
            margin = ini.Get(SECTION, "margin").ToSingle(0f);

            machineRefinery = ini.Get(SECTION, "refinery").ToBoolean(true);
            machineAssembler = ini.Get(SECTION, "assembler").ToBoolean(true);
            rows = ini.Get(SECTION, "rows").ToInt32(6);
            width = ini.Get(SECTION, "width").ToSingle(250f);
            height = ini.Get(SECTION, "height").ToSingle(120f);
            stringLen = ini.Get(SECTION, "string_len").ToInt32(20);
            maxItems = ini.Get(SECTION, "max_items").ToInt32(3);

            colorDefaults = new ColorDefaults(ini, SECTION_MACHINE_COLOR);
            colorDefaults.Load();
            cDefault = colorDefaults.GetDefault();

            types.Clear();
            columns = 0;
            if (machineRefinery)
            {
                types.Add("Refinery");
                columns += 1;
            }
            if (machineAssembler)
            {
                types.Add("Assembler");
                columns += 1;
            }
            columns = rows / Math.Max(1, columns);

            style = new Style()
            {
                Width = width,
                Height = height,
                Padding = new StylePadding(padding),
                Margin = new StyleMargin(margin),
            };
            style.Scale(scale);

            sizeIcon = style.Height - (10f * scale);
            rotationOrScale = 0.5f * scale;
            cellSpacing = 10f * scale;

            formWidth = style.Width - (5f * scale);
            formHeight = style.Height - (5f * scale);

            var blockFilter = BlockFilter<IMyProductionBlock>.Create(displayLcd.Block, filter);
            BlockSystem<IMyProductionBlock>.SearchByFilter(program, producers, blockFilter);
            producers.List.Sort((a, b) => a.CustomName.CompareTo(b.CustomName));
        }
        public void Save(MyIni ini)
        {
            ini.Set(SECTION, "panel", panel);
            ini.Set(SECTION, "on", enable);
            ini.Set(SECTION, "filter", filter);
            ini.Set(SECTION, "scale", scale);
            ini.Set(SECTION, "padding", padding);
            ini.Set(SECTION, "margin", margin);

            ini.Set(SECTION, "refinery", machineRefinery);
            ini.Set(SECTION, "assembler", machineAssembler);
            ini.Set(SECTION, "rows", rows);
            ini.Set(SECTION, "width", width);
            ini.Set(SECTION, "height", height);
            ini.Set(SECTION, "string_len", stringLen);
            ini.Set(SECTION, "max_items", maxItems);

            colorDefaults?.Save();
        }

        public void Draw(Drawing drawing)
        {
            if (!enable) return;
            if (types.Count == 0) return;

            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();

            foreach (string type in types)
            {
                int count = 0;
                producers.ForEach(block =>
                {
                    if (!block.GetType().Name.Contains(type)) return;

                    var p = surface.Position + new Vector2(style.Width * (count / columns), style.Height * (count - (count / columns) * columns));
                    DrawMachine(surface, p, block);
                    count++;
                });
                surface.Position += new Vector2(0, style.Height) * columns;
            }
        }

        void DrawMachine(SurfaceDrawing surface, Vector2 position, IMyProductionBlock block)
        {
            TraversalMachine(block);

            surface.DrawForm(SpriteForm.SquareSimple, position, formWidth, formHeight, new Color(5, 5, 5, 125));

            // Element Name
            surface.AddSprite(new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = Util.CutString(block.CustomName, stringLen),
                Color = colorTitle,
                Position = position + new Vector2(style.Margin.X, 0),
                RotationOrScale = 0.6f * scale,
                FontId = SurfaceDrawing.Font,
                Alignment = TextAlignment.LEFT
            });

            float x = 0f;
            foreach (var item in items)
            {
                // Icon
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = item.Icon,
                    Size = new Vector2(sizeIcon, sizeIcon),
                    Color = colorDefaults.GetColor(item.Name, item.Data, cDefault),
                    Position = position + new Vector2(x, sizeIcon * 0.5f + cellSpacing),
                });

                if (symbol.Keys.Contains(item.Data))
                {
                    // Symbol
                    var positionSymbol = position + new Vector2(x, 20 * scale);
                    surface.AddSprite(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = symbol[item.Data],
                        Color = colorText,
                        Position = positionSymbol,
                        RotationOrScale = rotationOrScale,
                        FontId = SurfaceDrawing.Font,
                        Alignment = TextAlignment.LEFT
                    });
                }

                // Quantity
                var positionQuantity = position + new Vector2(x, sizeIcon - (12 * scale));
                var color = GetVarianceColor(item.Variance);
                surface.DrawForm(SpriteForm.SquareSimple, positionQuantity, sizeIcon, 15f * scale, color);
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = Util.GetKiloFormat(item.Amount),
                    Color = colorText,
                    Position = positionQuantity,
                    RotationOrScale = rotationOrScale,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });
                x += style.Height;
            }
        }
        void TraversalMachine(IMyProductionBlock block)
        {
            Dictionary<string, double> lastAmount;
            if (lastMachineAmount.ContainsKey(block.EntityId))
            {
                lastAmount = lastMachineAmount[block.EntityId];
            }
            else
            {
                lastAmount = new Dictionary<string, double>();
                lastMachineAmount.Add(block.EntityId, lastAmount);
            }

            items.Clear();
            if (block is IMyAssembler)
            {
                ProcessAssembler(block, lastAmount);
            }
            else
            {
                ProcessRefinery(block, lastAmount);
            }
            lastMachineAmount[block.EntityId] = lastAmount;
        }
        void ProcessAssembler(IMyProductionBlock block, Dictionary<string, double> lastAmount)
        {
            productionItems.Clear();
            block.GetQueue(productionItems);
            if (productionItems.Count == 0) return;

            int loop = 0;
            foreach (var item in productionItems)
            {
                if (loop >= maxItems) break;

                string type = Util.GetType(item);
                string name = Util.GetName(item);
                string data = Util.GetData(item.BlueprintId);
                string key = $"{type}_{name}";
                double amount = (double)item.Amount;
                var variance = CalculateVariance(lastAmount, key, amount);

                items.Add(new Item()
                {
                    Type = type,
                    Name = name,
                    Data = data,
                    Amount = amount,
                    Variance = variance
                });
                loop++;
            }
        }
        void ProcessRefinery(IMyProductionBlock block, Dictionary<string, double> lastAmount)
        {
            inventoryItems.Clear();
            block.InputInventory.GetItems(inventoryItems);
            if (inventoryItems.Count == 0) return;

            int loop = 0;
            foreach (var item in inventoryItems)
            {
                if (loop >= maxItems) break;

                string type = Util.GetType(item);
                string name = Util.GetName(item);
                string data = item.Type.SubtypeId;
                string key = $"{type}_{name}";
                double amount = (double)item.Amount;
                var variance = CalculateVariance(lastAmount, key, amount);

                items.Add(new Item()
                {
                    Type = type,
                    Name = name,
                    Data = data,
                    Amount = amount,
                    Variance = variance
                });
                loop++;
            }
        }
        static Variances CalculateVariance(Dictionary<string, double> lastAmount, string key, double amount)
        {
            if (!lastAmount.ContainsKey(key))
            {
                lastAmount.Add(key, amount);
                return Variances.Ascending;
            }

            var variance = Variances.NoVariance;
            if (lastAmount[key] < amount) variance = Variances.Ascending;
            if (lastAmount[key] > amount) variance = Variances.Descending;
            lastAmount[key] = amount;

            return variance;
        }
        Color GetVarianceColor(Variances variance)
        {
            if (variance == Variances.NoVariance) return colorNoVariance;
            if (variance == Variances.Descending) return colorDescending;
            return colorAscending;
        }
    }
}
