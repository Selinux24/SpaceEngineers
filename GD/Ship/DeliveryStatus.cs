
namespace IngameScript
{
    enum DeliveryStatus
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
