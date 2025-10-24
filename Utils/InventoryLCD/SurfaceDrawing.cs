using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class SurfaceDrawing
    {
        public const string Font = "Monospace";

        readonly IMyTextSurface surface;
        
        MySpriteDrawFrame frame;
        bool initialized = false;
        RectangleF viewport;

        public Drawing Parent { get; }
        public float Width => viewport.Width;
        public float Height => viewport.Height;
        public Vector2 Position { get; set; }

        public SurfaceDrawing(Drawing parent, IMyTextSurface surface)
        {
            Parent = parent;
            this.surface = surface;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";
            surface.ScriptBackgroundColor = Color.Black;

            viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);

            Position = viewport.Position;

            frame = surface.DrawFrame();
            frame.Clip((int)viewport.X, (int)viewport.Y, (int)viewport.Width, (int)viewport.Height);
        }
        public void Dispose()
        {
            if (!initialized) return;

            frame.Dispose();
        }
        public void Clean()
        {
            if (!initialized) return;

            DrawForm(new Vector2(), SpriteForm.SquareSimple, viewport.Width, viewport.Height, Color.Black);
        }

        public void AddSprite(MySprite sprite)
        {
            frame.Add(sprite);
        }
       
        public void DrawForm(Vector2 position, SpriteForm form, float width, float height, Color color)
        {
            AddSprite(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = form.ToString(),
                Size = new Vector2(width, height),
                Color = color,
                Position = position + new Vector2(0, height / 2)
            });
        }
        public void DrawGaugeIcon(Vector2 position, string name, double amount, int limit, StyleIcon styleIcon, bool showGauge, bool showSymbol, int variance)
        {
            var p = position + new Vector2(styleIcon.Padding.X, styleIcon.Padding.Y);

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

            DrawForm(p, SpriteForm.SquareSimple, styleIcon.Width, styleIcon.Height, new Color(5, 5, 5, 125));

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

            if (showGauge)
            {
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
            }

            if (!showSymbol) return;

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
        public Vector2 DrawGauge(Vector2 position, float amount, float limit, StyleGauge style)
        {
            float width = style.Width;
            float height = style.Height;

            if (style.Fullscreen && style.Orientation.Equals(SpriteOrientation.Horizontal)) width = viewport.Width;
            if (style.Fullscreen && style.Orientation.Equals(SpriteOrientation.Vertical)) height = viewport.Height;

            width += -2 * style.Padding.X;
            height += -2 * style.Padding.X;
            var p = position + new Vector2(style.Padding.X, style.Padding.Y);

            // Gauge
            DrawForm(p, SpriteForm.SquareSimple, width, height, style.Color);

            // Gauge interior
            var colorInt = new Color(20, 20, 20, 255);
            DrawForm(p + new Vector2(style.Margin.X, style.Margin.Y), SpriteForm.SquareSimple, width - 2 * style.Margin.X, height - 2 * style.Margin.Y, colorInt);

            // Gauge quantity
            float percent = Math.Min(1f, amount / limit);
            var threshold = style.Thresholds.GetGaugeThreshold(percent);
            float w2 = width - 2 * style.Margin.X;
            float h2 = height - 2 * style.Margin.Y;
            var color = threshold.Color * style.ColorSoftening;

            if (style.Orientation.Equals(SpriteOrientation.Horizontal))
            {
                float length = w2 * percent;
                DrawForm(p + new Vector2(style.Margin.X, style.Margin.Y), SpriteForm.SquareSimple, length, h2, color);
            }
            else
            {
                float length = h2 * percent;
                DrawForm(p + new Vector2(style.Margin.X, h2 - length + style.Margin.Y), SpriteForm.SquareSimple, w2, length, color);
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

            if (style.Orientation == SpriteOrientation.Horizontal)
            {
                return position + new Vector2(0, height + 2 * style.Margin.Y);
            }
            else
            {
                return position + new Vector2(width + 2 * style.Margin.X, 0);
            }
        }
    }
}
