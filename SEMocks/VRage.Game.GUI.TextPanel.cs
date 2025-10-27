using System;
using VRageMath;

namespace VRage.Game.GUI.TextPanel
{
    public enum ContentType
    {
        TEXT_AND_IMAGE,
        SCRIPT,
    }
    public enum SpriteType
    {
        CLIP_RECT,
        TEXT,
        TEXTURE,
    }
    public enum TextAlignment
    {
        CENTER,
        LEFT,
        RIGHT,
    }

    public struct MySprite : IEquatable<MySprite>
    {
        public SpriteType Type;
        public string Data;
        public Vector2? Size;
        public Color? Color;
        public Vector2? Position;
        public float RotationOrScale;
        public string FontId;
        public TextAlignment Alignment;

        public bool Equals(MySprite other)
        {
            throw new NotImplementedException();
        }
    }

    public struct MySpriteDrawFrame : IDisposable
    {
        public MySpriteDrawFrame(Action<MySpriteDrawFrame> submitFrameCallback) { }

        public void Add(MySprite sprite)
        {
            throw new NotImplementedException();
        }

        public ClearClipToken Clip(int x, int y, int width, int height)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public struct ClearClipToken : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
