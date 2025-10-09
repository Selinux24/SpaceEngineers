using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineers.Game.ModAPI.Ingame
{
    public interface IMyTimerBlock : IMyTerminalBlock
    {
        bool IsCountingDown { get; }
        void ApplyAction(string v);
        void StartCountdown();
        void Trigger();
    }
    public interface IMySoundBlock : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        float LoopPeriod { get; set; }
        void Play();
        void Stop();
    }
}
