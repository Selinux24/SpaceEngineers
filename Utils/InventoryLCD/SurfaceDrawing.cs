using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class SurfaceDrawing
    {
        private MySpriteDrawFrame frame;
        private bool initialized = false;

        public IMyTextSurface Surface { get; set; }
        public RectangleF Viewport { get; set; }
        public string Font { get; } = "Monospace";
        public Drawing Parent { get; }
        public Vector2 Position { get; set; }

        public SurfaceDrawing(Drawing parent, IMyTextSurface surface)
        {
            Parent = parent;
            Surface = surface;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            Surface.ContentType = ContentType.SCRIPT;
            Surface.Script = "";
            Surface.ScriptBackgroundColor = Color.Black;

            Viewport = new RectangleF((Surface.TextureSize - Surface.SurfaceSize) / 2f, Surface.SurfaceSize);

            Position = Viewport.Position;

            frame = Surface.DrawFrame();
            frame.Clip((int)Viewport.X, (int)Viewport.Y, (int)Viewport.Width, (int)Viewport.Height);
        }
        public void Dispose()
        {
            if (!initialized) return;

            frame.Dispose();
        }
        public void Clean()
        {
            if (!initialized) return;

            AddForm(new Vector2(), SpriteForm.SquareSimple, Viewport.Width, Viewport.Height, Color.Black);
        }

        public void AddSprite(MySprite sprite)
        {
            frame.Add(sprite);
        }

        public void AddForm(Vector2 position, SpriteForm form, float width, float height, Color color)
        {
            var sprite = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = form.ToString(),
                Size = new Vector2(width, height),
                Color = color,
                Position = position + new Vector2(0, height / 2)
            };

            AddSprite(sprite);
        }

        public void DrawGaugeIcon(Vector2 position, string name, double amount, int limit, StyleIcon styleIcon, bool showGauge, int variance)
        {
            Vector2 p = position + new Vector2(styleIcon.Padding.X, styleIcon.Padding.Y);

            float factor = 2.5f;

            float width = (styleIcon.Width - 3 * styleIcon.Margin.X) / factor;
            float height = styleIcon.Height - 3 * styleIcon.Margin.Y;
            string fontTitle = EnumFont.BuildInfo;
            float fontSizeTitle = Math.Max(0.3f, (float)Math.Round(height / 4f / 32f, 1));
            float deltaTitle = fontSizeTitle * 20f;

            string fontQuantity = EnumFont.BuildInfo;
            float fontSizeQuantity = Math.Max(0.3f, (float)Math.Round(height / 2.25f / 32f, 1));
            float deltaQuantity = fontSizeQuantity * 32f;

            float iconSize = styleIcon.Height - styleIcon.Margin.Y - deltaTitle;

            float globalSoftening = 0.7f;

            AddForm(p, SpriteForm.SquareSimple, styleIcon.Width, styleIcon.Height, new Color(5, 5, 5, 125));

            // Add Icon
            AddSprite(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = styleIcon.Path,
                Size = new Vector2(iconSize, iconSize),
                Color = styleIcon.Color * globalSoftening,
                Position = p + new Vector2(0, deltaTitle + iconSize / 2)
            });

            // Element Name
            AddSprite(new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = name,
                Color = Color.DimGray,
                Position = p,
                RotationOrScale = fontSizeTitle,
                FontId = fontTitle,
                Alignment = TextAlignment.LEFT
            });

            // Quantity
            AddSprite(new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = Util.GetKiloFormat(amount),
                Color = Color.LightGray * globalSoftening,
                Position = p + new Vector2(width + styleIcon.Margin.X, deltaTitle + styleIcon.Margin.Y),
                RotationOrScale = fontSizeQuantity,
                FontId = fontQuantity
            });

            if (!showGauge) return;

            // Add Gauge
            var style = new StyleGauge()
            {
                Orientation = SpriteOrientation.Horizontal,
                Fullscreen = false,
                Width = width * (factor - 1f),
                Height = height / 3,
                Padding = new StylePadding(0),
                Thresholds = styleIcon.Thresholds,
                ColorSoftening = styleIcon.ColorSoftening
            };
            DrawGauge(p + new Vector2(width + styleIcon.Margin.X, deltaTitle + deltaQuantity + styleIcon.Margin.Y), (float)amount, limit, style);

            float symbolSize = 20f * fontSizeQuantity;
            float offset = 25f * fontSizeQuantity;
            if (variance == 1)
            {
                Color green = new Color(0, 100, 0, 255);

                AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = SpriteForm.Triangle.ToString(),
                    Size = new Vector2(symbolSize, symbolSize),
                    Color = green * styleIcon.ColorSoftening,
                    Position = p + new Vector2(factor * width - offset, symbolSize - styleIcon.Margin.Y),
                    RotationOrScale = 0
                });
            }

            if (variance == 3)
            {
                Color red = new Color(100, 0, 0, 255);

                AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = SpriteForm.Triangle.ToString(),
                    Size = new Vector2(symbolSize, symbolSize),
                    Color = red * styleIcon.ColorSoftening,
                    Position = p + new Vector2(factor * width - offset, symbolSize + styleIcon.Margin.Y),
                    RotationOrScale = (float)Math.PI
                });
            }
        }
        public Vector2 DrawGauge(Vector2 position, float amount, float limit, StyleGauge style, bool invert)
        {
            return DrawGauge(position, amount, limit, style, invert);
        }
        public Vector2 DrawGauge(Vector2 position, float amount, float limit, StyleGauge style)
        {
            float width = style.Width;
            float height = style.Height;

            if (style.Fullscreen && style.Orientation.Equals(SpriteOrientation.Horizontal)) width = Viewport.Width;
            if (style.Fullscreen && style.Orientation.Equals(SpriteOrientation.Vertical)) height = Viewport.Height;

            width += -2 * style.Padding.X;
            height += -2 * style.Padding.X;
            var p = position + new Vector2(style.Padding.X, style.Padding.Y);

            // Gauge
            AddForm(p, SpriteForm.SquareSimple, width, height, style.Color);
            // Gauge Interior
            var color_interior = new Color(20, 20, 20, 255);
            AddForm(p + new Vector2(style.Margin.X, style.Margin.Y), SpriteForm.SquareSimple, width - 2 * style.Margin.X, height - 2 * style.Margin.Y, color_interior);

            // Gauge quantity
            float percent = Math.Min(1f, amount / limit);
            var threshold = style.Thresholds.GetGaugeThreshold(percent);
            Color color = threshold.Color * style.ColorSoftening;

            if (style.Orientation.Equals(SpriteOrientation.Horizontal))
            {
                float width2 = width - 2 * style.Margin.X;
                float height2 = height - 2 * style.Margin.Y;
                float length = width2 * percent;
                AddForm(p + new Vector2(style.Margin.X, style.Margin.Y), SpriteForm.SquareSimple, length, height2, color);
            }
            else
            {
                float width2 = width - 2 * style.Margin.X;
                float height2 = height - 2 * style.Margin.Y;
                float length = height2 * percent;
                AddForm(p + new Vector2(style.Margin.X, height2 - length + style.Margin.Y), SpriteForm.SquareSimple, width2, length, color);
            }

            if (style.Percent)
            {
                string data = $"{percent:P0}";
                if (percent < 0.999 && style.Round) data = $"{percent:P1}";

                // Tag
                AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = data,
                    Size = new Vector2(width, width),
                    Color = Color.Black,
                    Position = p + new Vector2(2 * style.Margin.X, style.Margin.Y),
                    RotationOrScale = Math.Max(0.3f, (float)Math.Round((height - 2 * style.Margin.Y) / 32f, 1)),

                    FontId = EnumFont.Monospace,
                    Alignment = TextAlignment.LEFT
                });
            }

            if (style.Orientation.Equals(SpriteOrientation.Horizontal))
            {
                return position + new Vector2(0, height + 2 * style.Margin.Y);
            }
            else
            {
                return position + new Vector2(width + 2 * style.Margin.X, 0);
            }
        }
        public void Test(MyGridProgram program)
        {
            Initialize();
            MySprite icon;
            //Gets a list of available sprites
            var names = new List<string>();
            Surface.GetSprites(names);
            int count = -1;
            float width = 35;
            bool auto = false;
            if (auto)
            {
                float delta = 100 - 4 * (Viewport.Width - 100) * Viewport.Height / names.Count;
                width = (-10 + (float)Math.Sqrt(Math.Abs(delta))) / 2f;
            }
            float height = width + 10f;
            int limit = (int)Math.Floor(Viewport.Height / height);
            Vector2 position = new Vector2(0, 0);
            program.Echo($"Count names: {names.Count}");
            program.Echo($"limit: {limit}");
            var customData = new StringBuilder();
            foreach (string name in names)
            {
                count++;
                customData.AppendLine($"{count}:{name}");
                Vector2 position2 = position + new Vector2(width * (count / limit), height * (count - (count / limit) * limit));
                icon = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = name,
                    Size = new Vector2(width, width),
                    Color = Color.White,
                    Position = position2 + new Vector2(0, height / 2 + 10 / 2),

                };
                frame.Add(icon);
                icon = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = count.ToString(),
                    Size = new Vector2(width, width),
                    RotationOrScale = 0.4f,
                    Color = Color.Gray,
                    Position = position2 + new Vector2(0, 0),
                    FontId = EnumFont.BuildInfo
                };
                frame.Add(icon);
            }
            Parent.TerminalBlock.CustomData = customData.ToString();
        }
    }
}
