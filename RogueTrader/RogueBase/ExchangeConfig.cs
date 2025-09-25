
namespace IngameScript
{
    class ExchangeConfig
    {
        public string Name;
        public int NumWaypoints;
        public double PathDistance; //Meters, distance from the dock to the first waypoint
        public System.Text.RegularExpressions.Regex RegEx => new System.Text.RegularExpressions.Regex($@"{Name}_\w+");

        public static bool Read(string cfgLine, out ExchangeConfig exchange)
        {
            exchange = new ExchangeConfig();

            if (string.IsNullOrWhiteSpace(cfgLine)) return false;

            var parts = cfgLine.Split(':');
            exchange.Name = parts[0];
            exchange.NumWaypoints = parts.Length > 0 ? int.Parse(parts[1]) : 5;
            exchange.PathDistance = parts.Length > 1 ? double.Parse(parts[2]) : 150;

            return true;
        }
    }
}
