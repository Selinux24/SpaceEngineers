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
        public const char AttributeSep = '=';

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
            double x;
            double y;
            double z;
            return new Vector3D(
                double.TryParse(trimmed[0], out x) ? x : 0,
                double.TryParse(trimmed[1], out y) ? y : 0,
                double.TryParse(trimmed[2], out z) ? z : 0);
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
            int v;
            return int.TryParse(ReadString(lines, name), out v) ? v : defaultValue;
        }
        public static long ReadLong(string[] lines, string name, long defaultValue = 0)
        {
            long v;
            return long.TryParse(ReadString(lines, name), out v) ? v : defaultValue;
        }
        public static double ReadDouble(string[] lines, string name, double defaultValue = 0)
        {
            double v;
            return double.TryParse(ReadString(lines, name), out v) ? v : defaultValue;
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
        public static List<string> ReadStringList(string[] lines, string name)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return new List<string>();
            }
            return value.Split(VariableSep).ToList();
        }

        public static bool IsFromGroup(string input, System.Text.RegularExpressions.Regex regEx)
        {
            return regEx.IsMatch(input);
        }
        public static string ExtractGroupName(string input, System.Text.RegularExpressions.Regex regEx)
        {
            var match = regEx.Match(input);
            if (match.Success)
            {
                return match.Value;
            }

            return string.Empty;
        }
    }
}
