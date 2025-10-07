using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using VRageMath;

namespace VRage.Game.ModAPI.Ingame
{
    public interface IMyEntity
    {
        long EntityId { get; }
        string Name { get; set; }
        Vector3D GetPosition();
        MatrixD WorldMatrix { get; }
        IMyInventory GetInventory();
    }
    public interface IMyCubeGrid : IMyEntity
    {
        string CustomName { get; set; }
    }
    public interface IMyCubeBlock : IMyEntity
    {
        SerializableDefinitionId BlockDefinition { get; }
        IMyCubeGrid CubeGrid { get; set; }
    }

    public struct MyItemType
    {
        public string SubtypeId { get; set; }
    }
    public interface IMyInventory
    {
        MyFixedPoint MaxVolume { get; }
        MyFixedPoint CurrentVolume { get; }
        int ItemCount { get; set; }

        MyInventoryItem? GetItemAt(int index);
        void GetItems(List<MyInventoryItem> itemsSalida);
        bool IsConnectedTo(IMyInventory dstInv);
        bool TransferItemTo(IMyInventory invSalida, MyInventoryItem item, MyFixedPoint aMover);
    }
    public struct MyInventoryItem
    {
        public MyItemType Type { get; set; }
        public MyFixedPoint Amount { get; set; }
    }
}
