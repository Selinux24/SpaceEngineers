
namespace IngameScript
{
    enum ShipStatus
    {
        Unknown,
        Idle,

        WaitingDock,
        Docking,
        Loading,
        Unloading,

        WaitingUndock,
        Undocking,

        OnRoute,
    }
}
