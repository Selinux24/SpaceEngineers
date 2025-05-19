using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class TripData
    {
        public int OrderId;
        public string OrderWarehouse;
        public Vector3D OrderWarehouseParking;
        public string OrderCustomer;
        public Vector3D OrderCustomerParking;

        public string ExchangeName;
        public Vector3D ExchangeForward;
        public Vector3D ExchangeUp;
        public readonly List<Vector3D> ExchangeApproachingWaypoints = new List<Vector3D>();
        public readonly List<Vector3D> ExchangeDepartingWaypoints = new List<Vector3D>();

        public Vector3D AlignFwd;
        public Vector3D AlignUp;
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public string OnLastWaypoint;

        public string DestinationName;
        public Vector3D DestinationPosition;
        public string OnDestinationArrival;

        public void LoadFromStorage(string[] lines)
        {
            OrderId = Utils.ReadInt(lines, "OrderId", -1);
            OrderWarehouse = Utils.ReadString(lines, "OrderWarehouse");
            OrderWarehouseParking = Utils.ReadVector(lines, "OrderWarehouseParking");
            OrderCustomer = Utils.ReadString(lines, "OrderCustomer");
            OrderCustomerParking = Utils.ReadVector(lines, "OrderCustomerParking");

            ExchangeName = Utils.ReadString(lines, "ExchangeName");
            ExchangeForward = Utils.ReadVector(lines, "ExchangeForward");
            ExchangeUp = Utils.ReadVector(lines, "ExchangeUp");
            ExchangeApproachingWaypoints.Clear();
            ExchangeApproachingWaypoints.AddRange(Utils.ReadVectorList(lines, "ExchangeApproachingWaypoints"));
            ExchangeDepartingWaypoints.Clear();
            ExchangeDepartingWaypoints.AddRange(Utils.ReadVectorList(lines, "ExchangeDepartingWaypoints"));

            AlignFwd = Utils.ReadVector(lines, "AlignFwd");
            AlignUp = Utils.ReadVector(lines, "AlignUp");
            Waypoints.Clear();
            Waypoints.AddRange(Utils.ReadVectorList(lines, "Waypoints"));
            OnLastWaypoint = Utils.ReadString(lines, "OnLastWaypoint");

            DestinationName = Utils.ReadString(lines, "DestinationName");
            DestinationPosition = Utils.ReadVector(lines, "DestinationPosition");
            OnDestinationArrival = Utils.ReadString(lines, "OnDestinationArrival");
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"OrderId={OrderId}",
                $"OrderWarehouse={OrderWarehouse}",
                $"OrderWarehouseParking={Utils.VectorToStr(OrderWarehouseParking)}",
                $"OrderCustomer={OrderCustomer}",
                $"OrderCustomerParking={Utils.VectorToStr(OrderCustomerParking)}",

                $"ExchangeName={ExchangeName}",
                $"ExchangeForward={Utils.VectorToStr(ExchangeForward)}",
                $"ExchangeUp={Utils.VectorToStr(ExchangeUp)}",
                $"ExchangeApproachingWaypoints={Utils.VectorListToStr(ExchangeApproachingWaypoints)}",
                $"ExchangeDepartingWaypoints={Utils.VectorListToStr(ExchangeDepartingWaypoints)}",

                $"AlignFwd={Utils.VectorToStr(AlignFwd)}",
                $"AlignUp={Utils.VectorToStr(AlignUp)}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"OnLastWaypoint={OnLastWaypoint}",

                $"DestinationName={DestinationName}",
                $"DestinationPosition={Utils.VectorToStr(DestinationPosition)}",
                $"OnDestinationArrival={OnDestinationArrival}",
            };

            return string.Join(Environment.NewLine, parts);
        }

        public void SetOrder(int orderId, string warehouse, Vector3D warehouseParking, string customer, Vector3D customerParking)
        {
            OrderId = orderId;
            OrderWarehouse = warehouse;
            OrderWarehouseParking = warehouseParking;
            OrderCustomer = customer;
            OrderCustomerParking = customerParking;
        }
        public void ClearOrder()
        {
            OrderId = -1;
            OrderWarehouse = null;
            OrderWarehouseParking = new Vector3D();
            OrderCustomer = null;
            OrderCustomerParking = new Vector3D();
        }

        public void SetExchange(string name, Vector3D forward, Vector3D up, List<Vector3D> waypoints)
        {
            ExchangeName = name;
            ExchangeForward = forward;
            ExchangeUp = up;
            ExchangeApproachingWaypoints.Clear();
            ExchangeApproachingWaypoints.AddRange(waypoints);
            ExchangeDepartingWaypoints.Clear();
            ExchangeDepartingWaypoints.AddRange(waypoints);
            ExchangeDepartingWaypoints.Reverse();
        }
        public void ClearExchange()
        {
            ExchangeName = null;
            ExchangeForward = Vector3D.Zero;
            ExchangeUp = Vector3D.Zero;
            ExchangeApproachingWaypoints.Clear();
            ExchangeDepartingWaypoints.Clear();
        }

        public void NavigateFromExchange(string onLastWaypoint)
        {
            AlignFwd = ExchangeForward;
            AlignUp = ExchangeUp;
            Waypoints.Clear();
            Waypoints.AddRange(ExchangeDepartingWaypoints);
            OnLastWaypoint = onLastWaypoint;
        }
        public void NavigateToExchange(string onLastWaypoint)
        {
            AlignFwd = ExchangeForward;
            AlignUp = ExchangeUp;
            Waypoints.Clear();
            Waypoints.AddRange(ExchangeApproachingWaypoints);
            OnLastWaypoint = onLastWaypoint;
        }
        public void NavigateToWarehouse(string onDestinationArrival)
        {
            DestinationName = OrderWarehouse;
            DestinationPosition = OrderWarehouseParking;
            OnDestinationArrival = onDestinationArrival;
        }
        public void NavigateToCustomer(string onDestinationArrival)
        {
            DestinationName = OrderCustomer;
            DestinationPosition = OrderCustomerParking;
            OnDestinationArrival = onDestinationArrival;
        }
    }
}
