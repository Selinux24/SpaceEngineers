using System;
using System.Collections.Generic;
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

        public static string ReadConfig(string customData, string name)
        {
            string[] lines = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }

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
    }
}
