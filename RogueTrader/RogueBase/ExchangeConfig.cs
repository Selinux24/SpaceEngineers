using System;

namespace IngameScript
{
    class ExchangeConfig
    {
        public string Name;
        public int NumWaypoints;
        public double PathDistance; //Meters, distance from the dock to the first waypoint
        public int PathType; //0 = Straight, 1 = Curve
        public System.Text.RegularExpressions.Regex RegEx => new System.Text.RegularExpressions.Regex($@"{Name}_\w+");

        public static bool Read(string cfgLine, out ExchangeConfig exchange)
        {
            exchange = new ExchangeConfig();

            if (string.IsNullOrWhiteSpace(cfgLine)) return false;

            var parts = cfgLine.Split(':');
            exchange.Name = parts[0];
            exchange.NumWaypoints = parts.Length > 1 ? int.Parse(parts[1]) : 5;
            exchange.PathDistance = parts.Length > 2 ? double.Parse(parts[2]) : 150;
            exchange.PathType = parts.Length > 3 ? int.Parse(parts[3]) : 0;

            return true;
        }
    }
}
