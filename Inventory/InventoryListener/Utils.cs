using System.Linq;

namespace IngameScript
{
    class Utils
    {
        public const char AttributeSep = '=';

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
        public static long ReadLong(string[] lines, string name, long defaultValue = 0)
        {
            string value = ReadString(lines, name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return long.Parse(value);
        }
    }
}
