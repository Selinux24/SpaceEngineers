﻿using System.Collections.Generic;

namespace VRage.Game.ModAPI.Ingame
{
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
