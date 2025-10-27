
namespace VRage
{
    public struct MyFixedPoint
    {
        public static MyFixedPoint Min(MyFixedPoint cantidadDisponible, MyFixedPoint cantidadRestante)
        {
            return new MyFixedPoint();
        }

        public long RawValue;
        public int ToIntSafe()
        {
            return 0;
        }

        public override int GetHashCode()
        {
            return 0;
        }
        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                var rhs = obj as MyFixedPoint?;
                if (rhs.HasValue)
                    return this == rhs.Value;
            }

            return false;
        }

        public static explicit operator MyFixedPoint(float d)
        {
            return new MyFixedPoint();
        }
        public static explicit operator MyFixedPoint(double d)
        {
            return new MyFixedPoint();
        }
        public static explicit operator MyFixedPoint(decimal d)
        {
            return new MyFixedPoint();
        }
        public static implicit operator MyFixedPoint(int i)
        {
            return new MyFixedPoint();
        }

        public static explicit operator decimal(MyFixedPoint fp)
        {
            return 0;
        }
        public static explicit operator float(MyFixedPoint fp)
        {
            return 0;
        }
        public static explicit operator double(MyFixedPoint fp)
        {
            return 0;
        }
        public static explicit operator int(MyFixedPoint fp)
        {
            return 0;
        }

        public static MyFixedPoint operator -(MyFixedPoint a)
        {
            return new MyFixedPoint();
        }

        public static bool operator <(MyFixedPoint a, MyFixedPoint b)
        {
            return true;
        }
        public static bool operator >(MyFixedPoint a, MyFixedPoint b)
        {
            return true;
        }

        public static bool operator <=(MyFixedPoint a, MyFixedPoint b)
        {
            return true;
        }
        public static bool operator >=(MyFixedPoint a, MyFixedPoint b)
        {
            return true;
        }

        public static bool operator ==(MyFixedPoint a, MyFixedPoint b)
        {
            return true;
        }
        public static bool operator !=(MyFixedPoint a, MyFixedPoint b)
        {
            return true;
        }

        public static MyFixedPoint operator +(MyFixedPoint a, MyFixedPoint b)
        {
            return new MyFixedPoint();
        }
        public static MyFixedPoint operator -(MyFixedPoint a, MyFixedPoint b)
        {
            return new MyFixedPoint();
        }

        public static MyFixedPoint operator *(MyFixedPoint a, MyFixedPoint b)
        {
            return new MyFixedPoint();
        }
        public static MyFixedPoint operator *(MyFixedPoint a, float b)
        {
            return new MyFixedPoint();
        }
        public static MyFixedPoint operator *(float a, MyFixedPoint b)
        {
            return new MyFixedPoint();
        }
        public static MyFixedPoint operator *(MyFixedPoint a, int b)
        {
            return new MyFixedPoint();
        }
        public static MyFixedPoint operator *(int a, MyFixedPoint b)
        {
            return new MyFixedPoint();
        }
    }

    public struct MyTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        public MyTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
    }
}
