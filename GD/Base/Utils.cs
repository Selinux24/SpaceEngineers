﻿using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    public static class Utils
    {
        const char ArgumentSep = '=';
        const char VariableSep = ';';
        const char VariablePartSep = ':';
        const char AttributeSep = '=';

        public static string ReadArgument(string[] arguments, string command, char sep = ArgumentSep)
        {
            string cmdToken = $"{command}{sep}";
            return arguments.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }

        public static string VectorToStr(Vector3D v)
        {
            return $"{v.X}{VariablePartSep}{v.Y}{VariablePartSep}{v.Z}";
        }
        public static string VectorListToStr(List<Vector3D> list)
        {
            return string.Join($"{VariableSep}", list.Select(VectorToStr));
        }
        public static string DistanceToStr(double distance)
        {
            if (distance < 1000)
            {
                return $"{distance:0.00}m";
            }
            else if (distance < 1000000)
            {
                return $"{distance / 1000:0.00}km";
            }
            else
            {
                return $"{distance / 1000:0.0}km";
            }
        }

        public static Vector3D StrToVector(string input)
        {
            var trimmed = input.Split(VariablePartSep);
            return new Vector3D(
                double.Parse(trimmed[0]),
                double.Parse(trimmed[1]),
                double.Parse(trimmed[2])
            );
        }
        public static List<Vector3D> StrToVectorList(string data)
        {
            List<Vector3D> res = new List<Vector3D>();

            if (string.IsNullOrEmpty(data))
            {
                return res;
            }

            string[] points = data.Split(VariableSep);
            for (int i = 0; i < points.Length; i++)
            {
                res.Add(StrToVector(points[i]));
            }

            return res;
        }

        public static string ReadString(string[] lines, string name, string defaultValue = "")
        {
            string cmdToken = $"{name}{AttributeSep}";
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
        public static double ReadDouble(string[] lines, string name, double defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return double.Parse(value);
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
    }
}
