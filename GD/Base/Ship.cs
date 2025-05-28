using System;
using VRageMath;

namespace IngameScript
{
    class Ship
    {
        public string Name;
        public ShipStatus ShipStatus;
        public Vector3D Position;
        public string Warehouse;
        public Vector3D WarehousePosition;
        public string Customer;
        public Vector3D CustomerPosition;
        public double Speed;
        public DateTime UpdateTime;
    }
}
