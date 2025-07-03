
namespace IngameScript
{
    enum DeliveryStatus
    {
        Unknown,
        Idle,

        RouteToLoad,
        WaitingForLoad,
        ApproachingLoad,
        Loading,

        RouteToUnload,
        WaitingForUnload,
        ApproachingUnload,
        Unloading,
    }
}
