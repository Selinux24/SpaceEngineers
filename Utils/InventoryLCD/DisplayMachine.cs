﻿using Sandbox.ModAPI.Ingame;
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

        readonly int max_loop = 3;
        readonly int string_len = 20;
        readonly Dictionary<long, Dictionary<string, double>> last_machine_amount = new Dictionary<long, Dictionary<string, double>>();

        int panel = 0;
        bool enable = false;
        bool search = true;
        string filter = "*";
        bool machine_refinery = false;
        bool machine_assembler = false;
        BlockSystem<IMyProductionBlock> producers;

        public DisplayMachine(Program program, DisplayLcd displayLcd)
        {
            this.program = program;
            this.displayLcd = displayLcd;
        }

        public void Load(MyIni MyIni)
        {
            panel = MyIni.Get("Machine", "panel").ToInt32(0);
            enable = MyIni.Get("Machine", "on").ToBoolean(false);
            filter = MyIni.Get("Machine", "filter").ToString("*");
            machine_refinery = MyIni.Get("Machine", "refinery").ToBoolean(true);
            machine_assembler = MyIni.Get("Machine", "assembler").ToBoolean(true);
        }
        public void Save(MyIni MyIni)
        {
            MyIni.Set("Machine", "panel", panel);
            MyIni.Set("Machine", "on", enable);
            MyIni.Set("Machine", "filter", filter);
            MyIni.Set("Machine", "refinery", machine_refinery);
            MyIni.Set("Machine", "assembler", machine_assembler);
        }
        private void Search()
        {
            BlockFilter<IMyProductionBlock> block_filter = BlockFilter<IMyProductionBlock>.Create(displayLcd.Block, filter);
            producers = BlockSystem<IMyProductionBlock>.SearchByFilter(program, block_filter);

            search = false;
        }
        public void Draw(Drawing drawing)
        {
            if (!enable) return;
            var surface = drawing.GetSurfaceDrawing(panel);
            surface.Initialize();
            Draw(surface);
        }
        public void Draw(SurfaceDrawing surface)
        {
            if (!enable) return;
            if (search) Search();
            List<string> types = new List<string>();
            int limit = 0;
            if (machine_refinery)
            {
                types.Add("Refinery");
                limit += 1;
            }
            if (machine_assembler)
            {
                types.Add("Assembler");
                limit += 1;
            }
            limit = 6 / limit;
            if (types.Count > 0)
            {
                Style style = new Style()
                {
                    Width = 250,
                    Height = 80,
                    Padding = new StylePadding(0),
                };

                foreach (string type in types)
                {
                    int count = 0;
                    producers.List.Sort(new BlockComparer());
                    producers.ForEach(delegate (IMyProductionBlock block)
                    {
                        if (block.GetType().Name.Contains(type))
                        {
                            Vector2 position2 = surface.Position + new Vector2(style.Width * (count / limit), style.Height * (count - (count / limit) * limit));
                            List<Item> items = TraversalMachine(block);
                            DrawMachine(surface, position2, block, items, style);
                            count += 1;
                        }
                    });
                    surface.Position += new Vector2(0, style.Height) * limit;
                }
            }
        }
        public List<Item> TraversalMachine(IMyProductionBlock block)
        {
            int loop = 0;
            List<Item> items = new List<Item>();

            Dictionary<string, double> last_amount;
            if (last_machine_amount.ContainsKey(block.EntityId))
            {
                last_amount = last_machine_amount[block.EntityId];
            }
            else
            {
                last_amount = new Dictionary<string, double>();
                last_machine_amount.Add(block.EntityId, last_amount);
            }

            if (block is IMyAssembler)
            {
                List<MyProductionItem> productionItems = new List<MyProductionItem>();
                block.GetQueue(productionItems);
                if (productionItems.Count > 0)
                {
                    loop = 0;
                    foreach (MyProductionItem productionItem in productionItems)
                    {
                        if (loop >= max_loop) break;
                        string iName = Util.GetName(productionItem);
                        string iType = Util.GetType(productionItem);
                        string key = string.Format("{0}_{1}", iType, iName);
                        var itemDefinitionId = productionItem.BlueprintId;
                        double amount = 0;
                        double.TryParse(productionItem.Amount.ToString(), out amount);

                        int variance = 2;
                        if (last_amount.ContainsKey(key))
                        {
                            if (last_amount[key] < amount) variance = 1;
                            if (last_amount[key] > amount) variance = 3;
                            last_amount[key] = amount;
                        }
                        else
                        {
                            variance = 1;
                            last_amount.Add(key, amount);
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
                List<MyInventoryItem> inventoryItems = new List<MyInventoryItem>();
                block.InputInventory.GetItems(inventoryItems);
                if (inventoryItems.Count > 0)
                {
                    loop = 0;
                    foreach (MyInventoryItem inventoryItem in inventoryItems)
                    {
                        if (loop >= max_loop) break;
                        string iName = Util.GetName(inventoryItem);
                        string iType = Util.GetType(inventoryItem);
                        string key = string.Format("{0}_{1}", iType, iName);
                        double amount = 0;
                        double.TryParse(inventoryItem.Amount.ToString(), out amount);

                        int variance = 2;
                        if (last_amount.ContainsKey(key))
                        {
                            if (last_amount[key] < amount) variance = 1;
                            if (last_amount[key] > amount) variance = 3;
                            last_amount[key] = amount;
                        }
                        else
                        {
                            variance = 1;
                            last_amount.Add(key, amount);
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
            last_machine_amount[block.EntityId] = last_amount;
            return items;
        }
        public void DrawMachine(SurfaceDrawing surface, Vector2 position, IMyProductionBlock block, List<Item> items, Style style)
        {
            float size_icon = style.Height - 10;
            Color color_title = new Color(100, 100, 100, 128);
            Color color_text = new Color(100, 100, 100, 255);
            float RotationOrScale = 0.5f;
            float cell_spacing = 10f;

            float form_width = style.Width - 5;
            float form_height = style.Height - 5;

            string colorDefault = program.MyProperty.Get("color", "default");

            float x = 0f;

            surface.AddForm(position + new Vector2(0, 0), SpriteForm.SquareSimple, form_width, form_height, new Color(5, 5, 5, 125));

            foreach (Item item in items)
            {
                // icon
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = item.Icon,
                    Size = new Vector2(size_icon, size_icon),
                    Color = program.MyProperty.GetColor("color", item.Name, item.Data, colorDefault),
                    Position = position + new Vector2(x, size_icon / 2 + cell_spacing)

                });

                if (surface.Parent.Symbol.Keys.Contains(item.Data))
                {
                    // symbol
                    Vector2 positionSymbol = position + new Vector2(x, 20);
                    surface.AddForm(positionSymbol, SpriteForm.SquareSimple, size_icon, 15f, new Color(10, 10, 10, 200));
                    surface.AddSprite(new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = surface.Parent.Symbol[item.Data],
                        Color = color_text,
                        Position = positionSymbol,
                        RotationOrScale = RotationOrScale,
                        FontId = surface.Font,
                        Alignment = TextAlignment.LEFT
                    });
                }

                // Quantity
                Vector2 positionQuantity = position + new Vector2(x, size_icon - 12);
                Color mask_color = new Color(0, 0, 20, 200);
                if (item.Variance == 2) mask_color = new Color(20, 0, 0, 200);
                if (item.Variance == 3) mask_color = new Color(0, 20, 0, 200);
                surface.AddForm(positionQuantity, SpriteForm.SquareSimple, size_icon, 15f, mask_color);
                surface.AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = Util.GetKiloFormat(item.Amount),
                    Color = color_text,
                    Position = positionQuantity,
                    RotationOrScale = RotationOrScale,
                    FontId = surface.Font,
                    Alignment = TextAlignment.LEFT
                });
                x += style.Height;
            }

            // Element Name
            MySprite icon = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = Util.CutString(block.CustomName, string_len),
                Color = color_title,
                Position = position + new Vector2(style.Margin.X, 0),
                RotationOrScale = 0.6f,
                FontId = surface.Font,
                Alignment = TextAlignment.LEFT
            };
            surface.AddSprite(icon);
        }
    }
}
