using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    static class Utils
    {
        public static string ReadArgument(string[] lines, string command)
        {
            string cmdToken = $"{command}=";
            return lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
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
            string cmdToken = $"{name}=";
            string value = lines.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return int.Parse(value);
        }
        public static string VectorToStr(Vector3D v)
        {
            return $"{v.X}:{v.Y}:{v.Z}";
        }
        public static string VectorListToStr(List<Vector3D> list)
        {
            return string.Join(";", list.Select(VectorToStr));
        }
        public static Vector3D StrToVector(string str)
        {
            string[] coords = str.Split(':');
            if (coords.Length == 3)
            {
                return new Vector3D(double.Parse(coords[0]), double.Parse(coords[1]), double.Parse(coords[2]));
            }
            return new Vector3D();
        }
        public static ShipStatus StrToShipStatus(string str)
        {
            if (str == "Idle") return ShipStatus.Idle;
            if (str == "ApproachingWarehouse") return ShipStatus.ApproachingWarehouse;
            if (str == "Loading") return ShipStatus.Loading;
            if (str == "RouteToCustomer") return ShipStatus.RouteToCustomer;
            if (str == "WaitingForUnload") return ShipStatus.WaitingForUnload;
            if (str == "ApproachingCustomer") return ShipStatus.ApproachingCustomer;
            if (str == "Unloading") return ShipStatus.Unloading;
            if (str == "RouteToWarehouse") return ShipStatus.RouteToWarehouse;
            return ShipStatus.Unknown;
        }
    }
}
