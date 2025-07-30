using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class ArrivalData
    {
        readonly Config config;
        int tickCount = 0;
        Vector3D position;

        public Vector3D Destination = Vector3D.Zero;
        public double Distance = 0;
        public string Command = null;
        public bool HasPosition = false;
        public string StateMsg;

        public ArrivalData(Config config)
        {
            this.config = config;
        }

        public bool Tick()
        {
            if (++tickCount < config.ArrivalTicks)
            {
                return false;
            }
            tickCount = 0;
            return true;
        }

        public void Initialize(Vector3D destination, double distance, string commad)
        {
            Destination = destination;
            Distance = distance;
            Command = commad;
            HasPosition = true;
        }
        public void Clear()
        {
            Destination = Vector3D.Zero;
            Distance = 0;
            Command = null;
            HasPosition = false;
        }

        public bool Arrived(Vector3D position)
        {
            this.position = position;

            double distance = Vector3D.Distance(position, Destination);
            if (distance <= Distance)
            {
                StateMsg = "Destination reached.";
                return true;
            }

            StateMsg = $"Distance to destination: {Utils.DistanceToStr(distance)}";
            return false;
        }

        public string GetArrivalState()
        {
            return $"Distance to position: {Utils.DistanceToStr(Vector3D.Distance(position, Destination))}";
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');
            if (parts.Length == 0) return;

            Destination = Utils.ReadVector(parts, "Destination");
            Distance = Utils.ReadDouble(parts, "Distance");
            Command = Utils.ReadString(parts, "Command");
            HasPosition = Utils.ReadInt(parts, "HasPosition") == 1;
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"Destination={Utils.VectorToStr(Destination)}",
                $"Distance={Distance}",
                $"Command={Command}",
                $"HasPosition={(HasPosition ? 1 : 0)}",
            };

            return string.Join("¬", parts);
        }
    }
}
