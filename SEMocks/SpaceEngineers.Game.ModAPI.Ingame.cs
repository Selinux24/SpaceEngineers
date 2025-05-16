using Sandbox.ModAPI.Ingame;

namespace SpaceEngineers.Game.ModAPI.Ingame
{
    public interface IMyTimerBlock : IMyTerminalBlock
    {
        void ApplyAction(string v);
        void StartCountdown();
    }
}
