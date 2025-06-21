
namespace IngameScript
{
    enum ShipStatus
    {
        Unknown,
        Idle,

        RouteToLoad,
        ApproachingLoad,
        WaitingForLoad,
        Loading,

        RouteToUnload,
        ApproachingUnload,
        WaitingForUnload,
        Unloading,
    }
}
