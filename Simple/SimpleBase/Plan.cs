using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Plan
    {
        public string Ship;
        public Vector3D Position;
        public List<string> GPSList;

        public string GetWaypoints()
        {
            return string.Join(Environment.NewLine, GPSList);
        }
    }
}
