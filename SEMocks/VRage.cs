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
}
