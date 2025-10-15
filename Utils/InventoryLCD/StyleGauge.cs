
namespace IngameScript
{
    class StyleGauge : Style
    {
        public SpriteOrientation Orientation { get; set; } = SpriteOrientation.Horizontal;
        public bool Fullscreen { get; set; } = false;
        public bool Percent { get; set; } = true;
        public bool Round { get; set; } = true;
        public GaugeThresholds Thresholds { get; set; } = new GaugeThresholds();
    }
}
