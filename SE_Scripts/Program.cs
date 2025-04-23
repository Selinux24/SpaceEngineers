
namespace Test
{
    static class Program
    {
        static Base.Program warehouse;
        static Base.Program base1;
        static Nave.Program ship1;
        static Nave.Program ship2;

        public static void Main(string[] args)
        {
            warehouse = new Base.Program();
            base1 = new Base.Program();
            ship1 = new Nave.Program();
            ship2 = new Nave.Program();

        }
    }
}