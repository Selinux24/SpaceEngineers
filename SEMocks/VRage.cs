using System;

namespace VRage
{
    public struct MyFixedPoint
    {
        public static MyFixedPoint Min(MyFixedPoint cantidadDisponible, MyFixedPoint cantidadRestante)
        {
            return new MyFixedPoint();
        }

        public int ToIntSafe()
        {
            throw new NotImplementedException();
        }

        public static implicit operator int(MyFixedPoint v)
        {
            return 0;
        }
        public static implicit operator MyFixedPoint(int v)
        {
            return 0;
        }
    }
}
