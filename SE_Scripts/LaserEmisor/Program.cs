using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace SE_Scripts.LaserEmisor
{
    partial class Program : MyGridProgram
    {
        readonly string nombreAntena = "Laser Emisor";
        readonly string gpsReceptor = "GPS:Laser Receptor:-50027.34:-87333.41:-43673.9:";
        readonly string temporizadorEnvio = "Timer_Envio";
        readonly string temporizadorError = "Timer_Error";

        IMyLaserAntenna antena;

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "ENVIAR")
            {
                antena = GridTerminalSystem.GetBlockWithName(nombreAntena) as IMyLaserAntenna;
                if (antena == null)
                {
                    ActivarTemporizador(temporizadorError);
                    Echo("No se encontró la antena.");
                    return;
                }

                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            if (antena == null)
            {
                return;
            }

            // Conectar si no está aún
            if (antena.Status != MyLaserAntennaStatus.Connected)
            {
                Echo($"{antena.Status} Conectando a GPS...");
                if (antena.Status != MyLaserAntennaStatus.SearchingTargetForAntenna && antena.Status != MyLaserAntennaStatus.RotatingToTarget && antena.Status != MyLaserAntennaStatus.Connecting)
                {
                    antena.Enabled = true;
                    antena.SetTargetCoords(gpsReceptor);
                    antena.Connect();
                }
                return;
            }

            ActivarTemporizador(temporizadorEnvio);
            Runtime.UpdateFrequency = UpdateFrequency.None;
            Echo("Comando enviado.");
        }

        void ActivarTemporizador(string nombre)
        {
            var timer = GridTerminalSystem.GetBlockWithName(nombre) as IMyTimerBlock;
            timer?.StartCountdown();
        }
    }
}
