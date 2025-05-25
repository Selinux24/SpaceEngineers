
namespace IngameScript
{
    enum ShipStatus
    {
        Unknown,
        Idle,

        RouteToWarehouse,
        ApproachingWarehouse,
        WaitingForLoad,
        Loading,

        RouteToCustomer,
        ApproachingCustomer,
        WaitingForUnload,
        Unloading,
    }
}
