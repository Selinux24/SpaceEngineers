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
        readonly Program program;
        readonly DisplayLcd displayLcd;

        readonly int maxLoop = 3;
        readonly int stringLen = 20;
        readonly Dictionary<long, Dictionary<string, double>> lastMachineAmount = new Dictionary<long, Dictionary<string, double>>();
        readonly List<string> types = new List<string>();
        readonly BlockSystem<IMyProductionBlock> producers = new BlockSystem<IMyProductionBlock>();
        readonly List<Item> items = new List<Item>();

        int panel = 0;
        bool enable = false;
        float scale = 1f;
        string filter = "*";
        bool machineRefinery = false;
        bool machineAssembler = false;
        int rows = 6;
        int columns = 0;
        Style style = new Style();

        public DisplayMachine(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni ini)
        {
            panel = ini.Get("Machine", "panel").ToInt32(0);
            enable = ini.Get("Machine", "on").ToBoolean(false);
            filter = ini.Get("Machine", "filter").ToString("*");
            scale = ini.Get("Machine", "scale").ToSingle(1f);
            machineRefinery = ini.Get("Machine", "refinery").ToBoolean(true);
            machineAssembler = ini.Get("Machine", "assembler").ToBoolean(true);
            rows = ini.Get("Machine", "rows").ToInt32(6);

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
            columns = rows / columns;

            style = new Style()
            {
                Width = 250,
                Height = 80,
                Padding = new StylePadding(0),
            };
            style.Scale(scale);

            var blockFilter = BlockFilter<IMyProductionBlock>.Create(displayLcd.Block, filter);
            BlockSystem<IMyProductionBlock>.SearchByFilter(program, producers, blockFilter);
            producers.List.Sort((a,b) => a.CustomName.CompareTo(b.CustomName));
        }
        public void Save(MyIni ini)
        {
            ini.Set("Machine", "panel", panel);
            ini.Set("Machine", "on", enable);
            ini.Set("Machine", "filter", filter);
            ini.Set("Machine", "scale", scale);
            ini.Set("Machine", "refinery", machineRefinery);
            ini.Set("Machine", "assembler", machineAssembler);
            ini.Set("Machine", "rows", rows);
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
                    count += 1;
                });
                surface.Position += new Vector2(0, style.Height) * columns;
            }
        }

        void DrawMachine(SurfaceDrawing surface, Vector2 position, IMyProductionBlock block)
        {
            TraversalMachine(block);

            float sizeIcon = style.Height - (10 * scale);
            Color colorTitle = new Color(100, 100, 100, 128);
            Color colorText = new Color(100, 100, 100, 255);
            float RotationOrScale = 0.5f * scale;
            float cellSpacing = 10f * scale;

            float formWidth = style.Width - (5 * scale);
            float formHeight = style.Height - (5 * scale);

            string colorDefault = program.Config.Get("color", "default");

            surface.DrawForm(position, SpriteForm.SquareSimple, formWidth, formHeight, new Color(5, 5, 5, 125));

            float x = 0f;
            foreach (var item in items)
            {
                // icon
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = item.Icon,
                    Size = new Vector2(sizeIcon, sizeIcon),
                    Color = program.Config.GetColor("color", item.Name, item.Data, colorDefault),
                    Position = position + new Vector2(x, sizeIcon / 2 + cellSpacing),
                });

                if (surface.Parent.Symbol.Keys.Contains(item.Data))
                {
                    // symbol
                    var positionSymbol = position + new Vector2(x, 20);
                    surface.DrawForm(positionSymbol, SpriteForm.SquareSimple, sizeIcon, 15f, new Color(10, 10, 10, 200));
                    surface.AddSprite(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = surface.Parent.Symbol[item.Data],
                        Color = colorText,
                        Position = positionSymbol,
                        RotationOrScale = RotationOrScale,
                        FontId = SurfaceDrawing.Font,
                        Alignment = TextAlignment.LEFT
                    });
                }

                // quantity
                var positionQuantity = position + new Vector2(x, sizeIcon - 12);
                Color maskColor = new Color(0, 0, 20, 200);
                if (item.Variance == 2) maskColor = new Color(20, 0, 0, 200);
                if (item.Variance == 3) maskColor = new Color(0, 20, 0, 200);
                surface.DrawForm(positionQuantity, SpriteForm.SquareSimple, sizeIcon, 15f, maskColor);
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = Util.GetKiloFormat(item.Amount),
                    Color = colorText,
                    Position = positionQuantity,
                    RotationOrScale = RotationOrScale,
                    FontId = SurfaceDrawing.Font,
                    Alignment = TextAlignment.LEFT
                });
                x += style.Height;
            }

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
        }
        void TraversalMachine(IMyProductionBlock block)
        {
            int loop = 0;
            items.Clear();

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

            if (block is IMyAssembler)
            {
                var productionItems = new List<MyProductionItem>();
                block.GetQueue(productionItems);
                if (productionItems.Count > 0)
                {
                    loop = 0;
                    foreach (var productionItem in productionItems)
                    {
                        if (loop >= maxLoop) break;
                        string iName = Util.GetName(productionItem);
                        string iType = Util.GetType(productionItem);
                        string key = string.Format("{0}_{1}", iType, iName);
                        var itemDefinitionId = productionItem.BlueprintId;
                        double amount = 0;
                        double.TryParse(productionItem.Amount.ToString(), out amount);

                        int variance = 2;
                        if (lastAmount.ContainsKey(key))
                        {
                            if (lastAmount[key] < amount) variance = 1;
                            if (lastAmount[key] > amount) variance = 3;
                            lastAmount[key] = amount;
                        }
                        else
                        {
                            variance = 1;
                            lastAmount.Add(key, amount);
                        }

                        items.Add(new Item()
                        {
                            Name = iName,
                            Data = iName,
                            Type = iType,
                            Amount = amount,
                            Variance = variance
                        });
                        loop++;
                    }
                }
            }
            else
            {
                var inventoryItems = new List<MyInventoryItem>();
                block.InputInventory.GetItems(inventoryItems);
                if (inventoryItems.Count > 0)
                {
                    loop = 0;
                    foreach (var inventoryItem in inventoryItems)
                    {
                        if (loop >= maxLoop) break;
                        string iName = Util.GetName(inventoryItem);
                        string iType = Util.GetType(inventoryItem);
                        string key = string.Format("{0}_{1}", iType, iName);
                        double amount = 0;
                        double.TryParse(inventoryItem.Amount.ToString(), out amount);

                        int variance = 2;
                        if (lastAmount.ContainsKey(key))
                        {
                            if (lastAmount[key] < amount) variance = 1;
                            if (lastAmount[key] > amount) variance = 3;
                            lastAmount[key] = amount;
                        }
                        else
                        {
                            variance = 1;
                            lastAmount.Add(key, amount);
                        }

                        items.Add(new Item()
                        {
                            Name = iName,
                            Data = iName,
                            Type = iType,
                            Amount = amount,
                            Variance = variance
                        });
                        loop++;
                    }
                }
            }
            lastMachineAmount[block.EntityId] = lastAmount;
        }
    }
}
