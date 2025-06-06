﻿using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        readonly TimeSpan QueryInterval = TimeSpan.FromMinutes(10);

        DateTime lastQuery = DateTime.MinValue;

        public Program()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                Me.CustomData =
                    "Channel=name\n" +
                    "CargoContainerName=name\n" +
                    "Inventory=item1:quantity1;itemN:quantityN;";

                Echo("CustomData not set.");
                return;
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Echo("Working...");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var time = DateTime.Now - lastQuery;
            if (time < QueryInterval)
            {
                Echo($"Waiting for next query: {QueryInterval - time:hh\\:mm\\:ss}");
                return;
            }
            lastQuery = DateTime.Now;

            string channel = ReadConfig(Me.CustomData, "Channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                Echo("Channel not set.");
                return;
            }

            string cargoContainerName = ReadConfig(Me.CustomData, "CargoContainerName");
            if (string.IsNullOrWhiteSpace(cargoContainerName))
            {
                Echo("CargoContainerName not set.");
                return;
            }

            string inventory = ReadConfig(Me.CustomData, "Inventory");
            if (string.IsNullOrWhiteSpace(inventory))
            {
                Echo("Inventory not set.");
                return;
            }

            var cargoContainers = GetBlocksOfType<IMyCargoContainer>(cargoContainerName);
            if (cargoContainers.Count == 0)
            {
                Echo("Cargo Containers Not Found.");
                return;
            }

            var required = ReadItemsFromCustomData(inventory);

            var current = GetCurrentItemsInStores(cargoContainers);

            string message = WriteMessage(required, current);
            if (message.Length > 0)
            {
                IGC.SendBroadcastMessage(channel, message.ToString());
                Echo($"Sending from {channel}: {message}");
            }
        }

        List<T> GetBlocksOfType<T>(string name) where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(name));
            return blocks;
        }

        static string ReadConfig(string customData, string name)
        {
            string[] config = customData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            string cmdToken = $"{name}=";
            return config.FirstOrDefault(l => l.StartsWith(cmdToken))?.Replace(cmdToken, "") ?? "";
        }
        static Dictionary<string, int> ReadItemsFromCustomData(string customData)
        {
            Dictionary<string, int> required = new Dictionary<string, int>();

            string[] lines = customData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split(':');
                if (parts.Length == 2)
                {
                    string item = parts[0].Trim();
                    int amount = int.Parse(parts[1].Trim());
                    required[item] = amount;
                }
            }

            return required;
        }
        static Dictionary<string, MyFixedPoint> GetCurrentItemsInStores(List<IMyCargoContainer> cargos)
        {
            Dictionary<string, MyFixedPoint> current = new Dictionary<string, MyFixedPoint>();
            foreach (var cargo in cargos)
            {
                var inventory = cargo.GetInventory();
                for (int i = 0; i < inventory.ItemCount; i++)
                {
                    var item = inventory.GetItemAt(i).Value;
                    string t = item.Type.SubtypeId;
                    if (!current.ContainsKey(t)) current[t] = 0;
                    current[t] += item.Amount;
                }
            }

            return current;
        }
        static string WriteMessage(Dictionary<string, int> required, Dictionary<string, MyFixedPoint> current)
        {
            StringBuilder message = new StringBuilder();

            foreach (var req in required)
            {
                var c = current.ContainsKey(req.Key) ? current[req.Key] : 0;
                if (c < req.Value)
                {
                    message.Append($"{req.Key}={req.Value - c};");
                }
            }

            return message.ToString();
        }
    }
}
