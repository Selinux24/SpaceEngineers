using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class SurfaceDrawing
    {
        public const string Font = EnumFont.Monospace;

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

            DrawForm(SpriteForm.SquareSimple, Vector2.Zero, viewport.Width, viewport.Height, Color.Black);
        }

        public void AddSprite(MySprite sprite)
        {
            frame.Add(sprite);
        }

        public void DrawForm(SpriteForm form, Vector2 position, float width, float height, Color color)
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
        public void DrawGaugeIcon(StyleIcon styleIcon, Vector2 position, string name, double amount, int limit, bool showGauge, bool showSymbol, Variances variance)
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

            DrawForm(SpriteForm.SquareSimple, p, styleIcon.Width, styleIcon.Height, new Color(5, 5, 5, 125));

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
                DrawGauge(style, p + new Vector2(width + styleIcon.Margin.X, deltaTitle + deltaQuantity + styleIcon.Margin.Y), (float)amount, limit);
            }

            if (!showSymbol) return;

            float symbolSize = 20f * fontSizeQuantity;
            float offset = 25f * fontSizeQuantity;

            if (variance == Variances.Ascending)
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

            if (variance == Variances.Descending)
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
        public Vector2 DrawGauge(StyleGauge style, Vector2 position, float amount, float limit)
        {
            float w = style.Width;
            float h = style.Height;

            if (style.Fullscreen && style.Orientation.Equals(SpriteOrientation.Horizontal)) w = viewport.Width;
            if (style.Fullscreen && style.Orientation.Equals(SpriteOrientation.Vertical)) h = viewport.Height;

            var p = position + new Vector2(style.Padding.X, style.Padding.Y);
            w += -2 * style.Padding.X;
            h += -2 * style.Padding.Y;

            // Gauge
            DrawForm(SpriteForm.SquareSimple, p, w, h, style.Color);

            float w2 = w - 2 * style.Margin.X;
            float h2 = h - 2 * style.Margin.Y;

            // Gauge interior
            DrawForm(SpriteForm.SquareSimple, p + new Vector2(style.Margin.X, style.Margin.Y), w2, h2, style.ColorInt);

            // Gauge quantity
            float pct = Math.Min(1f, amount / limit);
            var thr = style.Thresholds.GetGaugeThreshold(pct);
            var color = thr.Color * style.ColorSoftening;

            if (style.Orientation.Equals(SpriteOrientation.Horizontal))
            {
                float l = w2 * pct;
                DrawForm(SpriteForm.SquareSimple, p + new Vector2(style.Margin.X, style.Margin.Y), l, h2, color);
            }
            else
            {
                float l = h2 * pct;
                DrawForm(SpriteForm.SquareSimple, p + new Vector2(style.Margin.X, h2 - l + style.Margin.Y), w2, l, color);
            }

            if (style.Percent)
            {
                string data = $"{pct:P0}";
                if (pct < 0.999 && style.Round) data = $"{pct:P1}";

                // Tag
                AddSprite(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = data,
                    Size = new Vector2(w, w),
                    Color = Color.Black,
                    Position = p + new Vector2(2 * style.Margin.X, style.Margin.Y),
                    RotationOrScale = Math.Max(0.3f, (float)Math.Round((h - 2 * style.Margin.Y) / 32f, 1)),

                    FontId = EnumFont.Monospace,
                    Alignment = TextAlignment.LEFT
                });
            }

            if (style.Orientation == SpriteOrientation.Horizontal)
            {
                return position + new Vector2(0, h + 2 * style.Margin.Y);
            }
            else
            {
                return position + new Vector2(w + 2 * style.Margin.X, 0);
            }
        }
    }
}
