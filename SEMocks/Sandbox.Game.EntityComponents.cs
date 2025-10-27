
using System.Collections.Generic;
using VRage.Game;

namespace Sandbox.Game.EntityComponents
{
    public interface IMyComponentBase
    {

    }
    public interface IMyEntityComponentBase
    {

    }
    public interface IMyResourceSinkComponent
    {

    }

    public class MyResourceSinkComponentBase
    {

    }
    public class MyResourceSourceComponentBase
    {

    }

    public class MyResourceSinkComponent : MyResourceSinkComponentBase, IMyComponentBase, IMyEntityComponentBase, IMyResourceSinkComponent
    {
        public List<MyDefinitionId> AcceptedResources { get; }
        public float RequiredInputByType(MyDefinitionId def) { return 0f; }
        public float CurrentInputByType(MyDefinitionId def) { return 0f; }
    }
    public class MyResourceSourceComponent : MyResourceSourceComponentBase, IMyComponentBase, IMyEntityComponentBase
    {

    }
}
