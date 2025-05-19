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
        public string ExchangeForward;
        public string ExchangeUp;
        public string ExchangeApproachingWaypoints;
        public string ExchangeDepartingWaypoints;

        public string AlignFwd;
        public string AlignUp;
        public string Waypoints;
        public string OnLastWaypoint;

        public string DestinationName;
        public Vector3D DestinationPosition;
        public string OnDestinationArrival;

        public void LoadFromStore(string[] lines)
        {
            OrderId = Utils.ReadInt(lines, "OrderId", -1);
            OrderWarehouse = Utils.ReadString(lines, "OrderWarehouse");
            OrderWarehouseParking = Utils.ReadVector(lines, "OrderWarehouseParking");
            OrderCustomer = Utils.ReadString(lines, "OrderCustomer");
            OrderCustomerParking = Utils.ReadVector(lines, "OrderCustomerParking");

            ExchangeName = Utils.ReadString(lines, "ExchangeName");
            ExchangeForward = Utils.ReadString(lines, "ExchangeForward");
            ExchangeUp = Utils.ReadString(lines, "ExchangeUp");
            ExchangeApproachingWaypoints = Utils.ReadString(lines, "ExchangeApproachingWaypoints");
            ExchangeDepartingWaypoints = Utils.ReadString(lines, "ExchangeDepartingWaypoints");

            AlignFwd = Utils.ReadString(lines, "AlignFwd");
            AlignUp = Utils.ReadString(lines, "AlignUp");
            Waypoints = Utils.ReadString(lines, "Waypoints");
            OnLastWaypoint = Utils.ReadString(lines, "OnLastWaypoint");

            DestinationName = Utils.ReadString(lines, "DestinationName");
            DestinationPosition = Utils.ReadVector(lines, "DestinationPosition");
            OnDestinationArrival = Utils.ReadString(lines, "OnDestinationArrival");
        }
        public string SaveToStore()
        {
            Dictionary<string, string> datos = new Dictionary<string, string>();

            datos["OrderId"] = OrderId.ToString();
            datos["OrderWarehouse"] = OrderWarehouse;
            datos["OrderWarehouseParking"] = Utils.VectorToStr(OrderWarehouseParking);
            datos["OrderCustomer"] = OrderCustomer;
            datos["OrderCustomerParking"] = Utils.VectorToStr(OrderCustomerParking);

            datos["ExchangeName"] = ExchangeName;
            datos["ExchangeForward"] = ExchangeForward;
            datos["ExchangeUp"] = ExchangeUp;
            datos["ExchangeApproachingWaypoints"] = ExchangeApproachingWaypoints;
            datos["ExchangeDepartingWaypoints"] = ExchangeDepartingWaypoints;

            datos["AlignFwd"] = AlignFwd;
            datos["AlignUp"] = AlignUp;
            datos["Waypoints"] = Waypoints;
            datos["OnLastWaypoint"] = OnLastWaypoint;

            datos["DestinationName"] = DestinationName;
            datos["DestinationPosition"] = Utils.VectorToStr(DestinationPosition);
            datos["OnDestinationArrival"] = OnDestinationArrival;

            var lineas = new List<string>();
            foreach (var kvp in datos)
            {
                lineas.Add($"{kvp.Key}={kvp.Value}");
            }

            return string.Join(Environment.NewLine, lineas);
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

        public void SetExchange(string name, string forward, string up, string waypoints)
        {
            ExchangeName = name;
            ExchangeForward = forward;
            ExchangeUp = up;
            ExchangeApproachingWaypoints = waypoints;
            ExchangeDepartingWaypoints = Utils.ReverseString(waypoints);
        }
        public void ClearExchange()
        {
            ExchangeName = null;
            ExchangeForward = null;
            ExchangeUp = null;
            ExchangeApproachingWaypoints = null;
            ExchangeDepartingWaypoints = null;
        }

        public void NavigateFromExchange(string onLastWaypoint)
        {
            AlignFwd = ExchangeForward;
            AlignUp = ExchangeUp;
            Waypoints = ExchangeDepartingWaypoints;
            OnLastWaypoint = onLastWaypoint;
        }
        public void NavigateToExchange(string onLastWaypoint)
        {
            AlignFwd = ExchangeForward;
            AlignUp = ExchangeUp;
            Waypoints = ExchangeApproachingWaypoints;
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
