using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace SE_Scripts.Inventory
{
    partial class Program : MyGridProgram
    {
        // Nombre de los contenedores donde se va a validar que exiten los componentes pedidos. Debe incluir contenedor de salida y el conector del Shuttle
        const string inventoryCargoName = "Cargo Container Components";
        // Nombre del contenedor donde se añaden los componentes a enviar
        const string outputCargoName = "Cargo Container Components MK3HQ Exports";

        // Listener global
        readonly IMyBroadcastListener bl;

        public Program()
        {
            string canal = Me.CustomData.Split(',')[0].Trim();

            bl = IGC.RegisterBroadcastListener(canal);
            bl.SetMessageCallback("procesarMensaje");
            Echo($"Listener registrado en el canal {canal}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) != 0 && argument == "procesarMensaje")
            {
                while (bl.HasPendingMessage)
                {
                    var mensaje = bl.AcceptMessage();
                    string contenido = mensaje.Data.ToString();

                    Echo("Mensaje recibido:");
                    Echo(contenido);

                    var requestedItems = ReadItems(contenido);
                    Prepare(requestedItems);
                }
            }
        }
        Dictionary<string, int> ReadItems(string contenido)
        {
            Dictionary<string, int> requestedItems = new Dictionary<string, int>();

            var partes = contenido.Split(';');
            foreach (var parte in partes)
            {
                string[] parts = parte.Split('=');
                if (parts.Length == 2)
                {
                    string item = parts[0].Trim();
                    int amount = (int)decimal.Parse(parts[1].Trim());
                    requestedItems.Add(item, amount);
                }
            }

            return requestedItems;
        }
        void Prepare(Dictionary<string, int> requestedItems)
        {

            IMyCargoContainer salida = GridTerminalSystem.GetBlockWithName(outputCargoName) as IMyCargoContainer;
            if (salida == null)
            {
                Echo("Contenedor de salida no encontrado");
                return;
            }
            var invSalida = salida.GetInventory();
            var itemsSalida = new List<MyInventoryItem>();
            invSalida.GetItems(itemsSalida);

            // Obtener todos los contenedores de entrada
            List<IMyCargoContainer> contenedores = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(contenedores, c => c.CustomName.Contains(inventoryCargoName));

            // Recorrer cada item solicitado
            foreach (var par in requestedItems)
            {
                string tipo = par.Key;
                int cantidadRestante = par.Value;

                int index = itemsSalida.FindIndex(i => i.Type.ToString().Contains(tipo));
                if (index >= 0)
                {
                    int c = (int)itemsSalida[index].Amount;
                    cantidadRestante -= c;
                }

                // Buscar ese item en los contenedores
                foreach (var cont in contenedores)
                {
                    if (cantidadRestante <= 0) break;

                    var inv = cont.GetInventory();
                    var items = new List<MyInventoryItem>();
                    inv.GetItems(items);

                    foreach (var item in items)
                    {
                        if (!item.Type.ToString().Contains(tipo)) continue;

                        VRage.MyFixedPoint cantidadDisponible = item.Amount;
                        VRage.MyFixedPoint aMover = VRage.MyFixedPoint.Min(cantidadDisponible, cantidadRestante);

                        bool movido = inv.TransferItemTo(invSalida, item, aMover);
                        if (movido) { cantidadRestante -= (int)aMover; Echo($"Movido {(int)aMover} de {item.Type}"); }
                        if (cantidadRestante <= 0) break;
                    }
                }

                Echo($"{tipo}: {(cantidadRestante > 0 ? $"FALTAN {cantidadRestante}" : "OK")}");
            }

            string timerName = Me.CustomData.Split(',')[1].Trim();
            IMyTimerBlock timer = GridTerminalSystem.GetBlockWithName(timerName) as IMyTimerBlock;
            if (timer != null)
            {
                timer.ApplyAction("TriggerNow");
                Echo($"{timerName} triggered");
            }
        }
    }
}
