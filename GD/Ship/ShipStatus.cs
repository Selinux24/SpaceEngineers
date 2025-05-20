
namespace IngameScript
{
    enum ShipStatus
    {
        Unknown,
        Idle,

        ApproachingWarehouse,
        Loading,
        RouteToCustomer,

        WaitingForUnload,

        ApproachingCustomer,
        Unloading,
        RouteToWarehouse,
    }
}
