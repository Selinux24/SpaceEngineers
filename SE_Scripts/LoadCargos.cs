using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;

namespace LoadCargos
{
    partial class Program : MyGridProgram
    {
        readonly IMyShipConnector connector;
        readonly List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            var connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(connectors, c => c.CubeGrid == Me.CubeGrid);
            connector = connectors.FirstOrDefault();

            if (connector == null)
            {
                Echo("No hay conectores con carga en mi grid.");
                return;
            }

            // Buscar todos los cargos de esta misma grid principal
            GridTerminalSystem.GetBlocksOfType(cargos, c => c.CubeGrid == Me.CubeGrid);
            if (cargos.Count == 0)
            {
                Echo("No hay cargos disponibles.");
                return;
            }

            Echo($"Conector seleccionado: {connector.CustomName}");
            Echo($"Contenedores detectados: {cargos.Count}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (connector == null || cargos.Count == 0)
            {
                Echo("Error: sin conector o cargos.");
                return;
            }

            var connectorInventory = connector.GetInventory();
            if (connectorInventory == null || connectorInventory.ItemCount == 0)
            {
                Echo("Conector vacío.");
                return;
            }

            Echo($"Moviendo {connectorInventory.ItemCount} ítems...");
            var items = new List<MyInventoryItem>();
            connectorInventory.GetItems(items);
            for (int i = items.Count - 1; i >= 0; i--) // Invertido para evitar problemas de índices
            {
                var item = items[i];

                foreach (var cargo in cargos)
                {
                    var cargoInventory = cargo.GetInventory();
                    if (cargoInventory.CanItemsBeAdded(item.Amount, item.Type))
                    {
                        connectorInventory.TransferItemTo(cargoInventory, i);
                        break; // Item movido, pasamos al siguiente
                    }
                }
            }

            Echo("Transferencia completada.");
        }
    }
}
