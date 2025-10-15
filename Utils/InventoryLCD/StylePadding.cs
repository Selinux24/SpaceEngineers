
namespace IngameScript
{
    class StylePadding
    {
        public float X { get; set; } = 2;
        public float Y { get; set; } = 2;

        public StylePadding(float value)
        {
            X = value;
            Y = value;
        }

        public virtual void Scale(float scale)
        {
            X *= scale;
            Y *= scale;
        }
    }
}
