using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    public class Route
    {
        public string LoadBase;
        public string UnloadBase;
        public bool LoadBaseOnPlanet;
        public bool UnloadBaseOnPlanet;
        public readonly List<Vector3D> ToLoadBaseWaypoints;
        public readonly List<Vector3D> ToUnloadBaseWaypoints;
        public bool OneTourRoute = false;

        public Route(string loadBase, bool loadBaseOnPlanet, List<Vector3D> toLoadBase, string unloadBase, bool unloadBaseOnPlanet, List<Vector3D> toUnloadBase, bool oneTourRoute)
        {
            LoadBase = loadBase;
            LoadBaseOnPlanet = loadBaseOnPlanet;
            ToLoadBaseWaypoints = new List<Vector3D>();
            if (toLoadBase != null) ToLoadBaseWaypoints.AddRange(toLoadBase);

            UnloadBase = unloadBase;
            UnloadBaseOnPlanet = unloadBaseOnPlanet;
            ToUnloadBaseWaypoints = new List<Vector3D>();
            if (toUnloadBase != null) ToUnloadBaseWaypoints.AddRange(toUnloadBase);

            OneTourRoute = oneTourRoute;
        }

        public bool IsValid()
        {
            return
                !string.IsNullOrWhiteSpace(LoadBase) &&
                ToLoadBaseWaypoints.Count > 0 &&
                !string.IsNullOrWhiteSpace(UnloadBase) &&
                ToUnloadBaseWaypoints.Count > 0;
        }

        public string GetState()
        {
            if (!IsValid())
            {
                return "No route defined.";
            }

            string state = "";
            if (OneTourRoute) state = $"One-Tour.{Environment.NewLine}";

            return $"{state}From {LoadBase}({ToLoadBaseWaypoints.Count}wp) To {UnloadBase}({ToUnloadBaseWaypoints.Count}wp)";
        }

        public void Clear()
        {
            LoadBase = "";
            LoadBaseOnPlanet = false;
            ToLoadBaseWaypoints.Clear();
            UnloadBase = "";
            UnloadBaseOnPlanet = false;
            ToUnloadBaseWaypoints.Clear();
            OneTourRoute = false;
        }

        public List<Vector3D> GetWaypointsToLoadBaseFromPosition(Vector3D position)
        {
            int nearestIndex = -1;
            double nearestDistance = double.MaxValue;
            for (int i = 0; i < ToLoadBaseWaypoints.Count; i++)
            {
                double distance = Vector3D.Distance(position, ToLoadBaseWaypoints[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex == ToLoadBaseWaypoints.Count - 1)
            {
                //It's the last waypoint
                return new List<Vector3D>() { ToLoadBaseWaypoints[nearestIndex] };
            }

            //Get the distance to the next waypoint from position
            double nDistance = Vector3D.Distance(position, ToLoadBaseWaypoints[nearestIndex + 1]);

            //Get the distance between the nearest waypoint and the next waypoint
            double sDistante = Vector3D.Distance(ToLoadBaseWaypoints[nearestIndex], ToLoadBaseWaypoints[nearestIndex + 1]);

            //If the distance from the current position to the next waypoint is less than the segment distance, use the next waypoint. Otherwise, use the nearest waypoint.
            int wpIndex = nDistance < sDistante ? nearestIndex + 1 : nearestIndex;

            return ToLoadBaseWaypoints.GetRange(wpIndex, ToLoadBaseWaypoints.Count - wpIndex);
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');

            var loadBase = Utils.ReadString(parts, "LoadBase");
            var unloadBase = Utils.ReadString(parts, "UnloadBase");

            if (string.IsNullOrWhiteSpace(loadBase) || string.IsNullOrWhiteSpace(unloadBase))
            {
                return;
            }

            LoadBase = loadBase;
            LoadBaseOnPlanet = Utils.ReadInt(parts, "LoadBaseOnPlanet") == 1;
            ToLoadBaseWaypoints.Clear();
            ToLoadBaseWaypoints.AddRange(Utils.ReadVectorList(parts, "ToLoadBaseWaypoints"));

            UnloadBase = unloadBase;
            UnloadBaseOnPlanet = Utils.ReadInt(parts, "UnloadBaseOnPlanet") == 1;
            ToUnloadBaseWaypoints.Clear();
            ToUnloadBaseWaypoints.AddRange(Utils.ReadVectorList(parts, "ToUnloadBaseWaypoints"));

            OneTourRoute = Utils.ReadInt(parts, "OneTourRoute") == 1;
        }
        public string SaveToStorage()
        {
            if (!IsValid())
            {
                return "";
            }

            var parts = new List<string>
            {
                $"LoadBase={LoadBase}",
                $"LoadBaseOnPlanet={(LoadBaseOnPlanet ? 1 : 0)}",
                $"ToLoadBaseWaypoints={Utils.VectorListToStr(ToLoadBaseWaypoints)}",

                $"UnloadBase={UnloadBase}",
                $"UnloadBaseOnPlanet={(UnloadBaseOnPlanet ? 1 : 0)}",
                $"ToUnloadBaseWaypoints={Utils.VectorListToStr(ToUnloadBaseWaypoints)}",

                $"OneTourRoute={(OneTourRoute ? 1 : 0)}",
            };

            return string.Join("¬", parts);
        }
    }
}
