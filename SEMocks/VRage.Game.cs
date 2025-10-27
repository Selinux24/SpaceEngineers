using System;

namespace VRage.Game
{
    public struct MyDefinitionId : IEquatable<MyDefinitionId>
    {
        public MyStringHash SubtypeId;
        public MyObjectBuilderType TypeId;

        public string SubtypeName { get; }

        public MyDefinitionId(MyObjectBuilderType type)
        {
            SubtypeId = new MyStringHash();
            TypeId = type;
            SubtypeName = null;
        }

        public MyDefinitionId(MyObjectBuilderType type, string subtypeName)
        {
            SubtypeId = new MyStringHash();
            TypeId = type;
            SubtypeName = subtypeName;
        }
        public MyDefinitionId(MyObjectBuilderType type, MyStringHash subtypeId)
        {
            SubtypeId = subtypeId;
            TypeId = type;
            SubtypeName = null;
        }
        public MyDefinitionId(MyRuntimeObjectBuilderId type, MyStringHash subtypeId)
        {
            SubtypeId = subtypeId;
            TypeId = (MyObjectBuilderType)type;
            SubtypeName = null;
        }

        public bool Equals(MyDefinitionId other)
        {
            throw new NotImplementedException();
        }

        public static bool TryParse(string v, string typeName, out MyDefinitionId itemDefinitionId)
        {
            throw new NotImplementedException();
        }
    }

    public struct MyStringHash
    {

    }
    public class MyObjectBuilderType : IEquatable<MyObjectBuilderType>
    {
        public static implicit operator MyObjectBuilderType(Type t) { return null; }
        public static implicit operator Type(MyObjectBuilderType t) { return null; }
        public static explicit operator MyRuntimeObjectBuilderId(MyObjectBuilderType t) { return null; }
        public static explicit operator MyObjectBuilderType(MyRuntimeObjectBuilderId id) { return null; }
        public static bool operator ==(MyObjectBuilderType lhs, MyObjectBuilderType rhs) { return false; }
        public static bool operator !=(MyObjectBuilderType lhs, MyObjectBuilderType rhs) { return false; }
        public static bool operator ==(Type lhs, MyObjectBuilderType rhs) { return false; }
        public static bool operator !=(Type lhs, MyObjectBuilderType rhs) { return false; }
        public static bool operator ==(MyObjectBuilderType lhs, Type rhs) { return false; }
        public static bool operator !=(MyObjectBuilderType lhs, Type rhs) { return false; }

        public MyObjectBuilderType(Type type)
        {

        }

        public bool Equals(MyObjectBuilderType other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class MyRuntimeObjectBuilderId
    {

    }
}
