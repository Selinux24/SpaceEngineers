using System;

namespace VRageMath
{
    public static class MathHelper
    {
        public static double Clamp(double v, double v1, double v2)
        {
            throw new NotImplementedException();
        }
    }

    public struct BoundingBoxD
    {
        public Vector3D Extents { get; set; }
        public Vector3D Center { get; set; }
        public double Perimeter { get; set; }
    }
    public struct MatrixD
    {
        public Vector3D Forward { get; }
        public Vector3D Backward { get; set; }
        public Vector3D Up { get; set; }

        public static MatrixD Invert(MatrixD worldMatrix)
        {
            throw new NotImplementedException();
        }
        public static MatrixD Transpose(MatrixD worldMatrix)
        {
            throw new NotImplementedException();
        }
    }
    public struct Color : IEquatable<Color>
    {
        public static Color White;
        public static Color Black;
        public static Color Green;
        public static Color Gray;
        public static Color DimGray;
        public static Color LightGreen;
        public static Color LightGray;

        public uint PackedValue;

        public byte A { get; set; }
        public byte B { get; set; }
        public byte G { get; set; }
        public byte R { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte Z { get; set; }

        public static Color operator *(Color a, double b) => new Color();
        public static Color operator /(Color a, double b) => new Color();

        public bool Equals(Color other)
        {
            return true;
        }

        public Color(int r, int g, int b)
        {
            PackedValue = 0;

            X = R = (byte)r;
            Y = G = (byte)g;
            Z = B = (byte)b;
            A = 255;
        }
        public Color(int r, int g, int b, int a)
        {
            PackedValue = 0;

            X = R = (byte)r;
            Y = G = (byte)g;
            Z = B = (byte)b;
            A = (byte)a;
        }
    }

    public struct Vector2 : IEquatable<Vector2>
    {
        public Vector2(float x, float y) : this()
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }
        public float Y { get; set; }

        public static bool operator ==(Vector2 lhs, Vector2 rhs) { return false; }
        public static bool operator !=(Vector2 lhs, Vector2 rhs) { return false; }

        public static Vector2 operator +(Vector2 v) => new Vector2();
        public static Vector2 operator -(Vector2 v) => new Vector2();

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2();
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2();

        public static Vector2 operator *(Vector2 a, double b) => new Vector2();
        public static Vector2 operator /(Vector2 a, double b) => new Vector2();

        public bool Equals(Vector2 other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public struct Vector3I : IEquatable<Vector3I>, IComparable<Vector3I>
    {
        public static Vector3D Zero { get; set; }
        public static Vector3I Up { get; }
        public static Vector3I Down { get; }
        public static Vector3I Left { get; }
        public static Vector3I Right { get; }
        public static Vector3I Forward { get; }
        public static Vector3I Backward { get; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public static bool operator ==(Vector3I lhs, Vector3I rhs) { return false; }
        public static bool operator !=(Vector3I lhs, Vector3I rhs) { return false; }

        public static Vector3I operator +(Vector3I v) => new Vector3I();
        public static Vector3I operator -(Vector3I v) => new Vector3I();

        public static Vector3I operator +(Vector3I a, Vector3I b) => new Vector3I();
        public static Vector3I operator -(Vector3I a, Vector3I b) => new Vector3I();

        public static Vector3I operator *(Vector3I a, double b) => new Vector3I();
        public static Vector3I operator /(Vector3I a, double b) => new Vector3I();

        public Vector3I(int x, int y, int z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Vector3I other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public int CompareTo(Vector3I other)
        {
            throw new NotImplementedException();
        }
    }
    public struct Vector3D : IEquatable<Vector3D>
    {
        public Vector3D(double x, double y, double z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3D Zero { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public static Vector3D operator +(Vector3D v) => new Vector3D();
        public static Vector3D operator -(Vector3D v) => new Vector3D();

        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D();
        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D();

        public static Vector3D operator *(Vector3D a, double b) => new Vector3D();
        public static Vector3D operator /(Vector3D a, double b) => new Vector3D();

        public static Vector3D Normalize(Vector3D v)
        {
            throw new NotImplementedException();
        }
        public bool Equals(Vector3D other)
        {
            throw new NotImplementedException();
        }
        public int Length()
        {
            throw new NotImplementedException();
        }
        public static Vector3D Cross(Vector3D v1, Vector3D v2)
        {
            throw new NotImplementedException();
        }
        public static Vector3D TransformNormal(Vector3D axis, MatrixD m)
        {
            throw new NotImplementedException();
        }
        public void Normalize()
        {
            throw new NotImplementedException();
        }
        public static double Dot(Vector3D v1, Vector3D v2)
        {
            throw new NotImplementedException();
        }
        public double Dot(Vector3D v)
        {
            throw new NotImplementedException();
        }
        public static double Distance(Vector3D p1, Vector3D p2)
        {
            throw new NotImplementedException();
        }

        public static Vector3D Lerp(Vector3D approachStart, Vector3D targetDock, double t)
        {
            throw new NotImplementedException();
        }
    }
    public struct RectangleF : IEquatable<RectangleF>
    {
        public Vector2 Position;
        public Vector2 Size;

        public float Bottom { get; }
        public Vector2 Center { get; }
        public float Height { get; set; }
        public float Right { get; }
        public float Width { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public RectangleF(Vector2 position, Vector2 size)
        {
            Position = position;
            Size = size;

            Bottom = position.Y + Size.Y;
            Center = position + Size * 0.5f;
            Height = position.Y;
            Right = position.X + Size.X;
            Width = Size.X - position.X;
            X = position.X;
            Y = position.Y;
        }
        public RectangleF(float x, float y, float width, float height) : this(new Vector2(x, y), new Vector2(width, height))
        {

        }

        public bool Equals(RectangleF other)
        {
            throw new NotImplementedException();
        }
    }
}
