using Sandbox.ModAPI.Ingame;
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

    public struct Color : IEquatable<Color>
    {
        public uint PackedValue;

        public bool Equals(Color other)
        {
            return true;
        }

        public Color(int r, int g, int b)
        {
            PackedValue = 0;
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
}
