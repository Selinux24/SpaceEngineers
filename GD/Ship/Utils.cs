using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    static class Utils
    {
        public static string ReadArgument(string argument, string command)
        {
            string cmdToken = $"{command}|";
            return argument.StartsWith(cmdToken) ? argument.Replace(cmdToken, "") : "";
        }

        public static string VectorToStr(Vector3D v)
        {
            return $"{v.X}:{v.Y}:{v.Z}";
        }
        public static Vector3D StrToVector(string input)
        {
            var trimmed = input.Split(':');
            return new Vector3D(
                double.Parse(trimmed[0]),
                double.Parse(trimmed[1]),
                double.Parse(trimmed[2])
            );
        }
        public static List<Vector3D> StrToVectorList(string data)
        {
            List<Vector3D> wp = new List<Vector3D>();

            if (string.IsNullOrEmpty(data))
            {
                return wp;
            }

            string[] points = data.Split(';');
            for (int i = 0; i < points.Length; i++)
            {
                wp.Add(StrToVector(points[i]));
            }

            return wp;
        }
        public static string VectorListToStr(List<Vector3D> list)
        {
            return string.Join(";", list.Select(VectorToStr));
        }

        public static string ReadString(string[] lines, string name, string defaultValue = null)
        {
            string cmdToken = $"{name}=";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return value;
        }
        public static int ReadInt(string[] lines, string name, int defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return int.Parse(value);
        }
        public static Vector3D ReadVector(string[] lines, string name)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return Vector3D.Zero;
            }

            return StrToVector(value);
        }
        public static List<Vector3D> ReadVectorList(string[] lines, string name)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return new List<Vector3D>();
            }

            return StrToVectorList(value);
        }

        public static double AngleBetweenVectors(Vector3D v1, Vector3D v2)
        {
            v1.Normalize();
            v2.Normalize();
            double dot = Vector3D.Dot(v1, v2);
            dot = MathHelper.Clamp(dot, -1.0, 1.0);
            return Math.Acos(dot);
        }
    }
}
