
namespace VRage.Game.ModAPI.Ingame.Utilities
{
    public class MyIni
    {
        public MyIniValue Get(string section, string name) { return new MyIniValue(); }
        public void Set(string section, string name, string value) { }
        public void Set(string section, string name, bool value) { }
        public void Set(string section, string name, int value) { }
        public void Set(string section, string name, float value) { }
        public void Set(string section, string name, double value) { }
        public bool ContainsSection(string section) { return false; }
        public bool TryParse(string content, out MyIniParseResult result) { return false; }
    }
    public struct MyIniValue
    {
        public bool IsEmpty { get; }
        public bool ToBoolean(bool defaultValue = false) { return defaultValue; }
        public int ToInt32(int defaultValue = 0) { return defaultValue; }
        public string ToString(string defaultValue) { return defaultValue; }
        public float ToSingle(float defaultValue = 0) { return defaultValue; }
        public double ToDouble(double defaultValue = 0) { return defaultValue; }
    }
    public struct MyIniParseResult
    {

    }
    public class MyCommandLine
    {
        public string Argument(int index) { return null; }
        public bool TryParse(string argument) { return false; }
    }
}
