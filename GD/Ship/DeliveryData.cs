using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace IngameScript
{
    class DeliveryData
    {
        double speed;
        Vector3D position;

        public DeliveryStatus Status = DeliveryStatus.Idle;

        public int OrderId;
        public string OrderWarehouse;
        public Vector3D OrderWarehouseParking;
        public string OrderCustomer;
        public Vector3D OrderCustomerParking;

        public ExchangeInfo Exchange = new ExchangeInfo();

        public Vector3D AlignFwd;
        public Vector3D AlignUp;
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public string OnLastWaypoint;

        public string DestinationName;
        public Vector3D DestinationPosition;
        public string OnDestinationArrival;

        public bool Active
        {
            get
            {
                return OrderId > 0;
            }
        }

        public void LoadFromStorage(string data)
        {
            string[] lines = data.Split('¬');
            if (lines.Length == 0) return;

            Status = (DeliveryStatus)Utils.ReadInt(lines, "Status");

            OrderId = Utils.ReadInt(lines, "OrderId", -1);
            OrderWarehouse = Utils.ReadString(lines, "OrderWarehouse");
            OrderWarehouseParking = Utils.ReadVector(lines, "OrderWarehouseParking");
            OrderCustomer = Utils.ReadString(lines, "OrderCustomer");
            OrderCustomerParking = Utils.ReadVector(lines, "OrderCustomerParking");

            Exchange = new ExchangeInfo(
                Utils.ReadString(lines, "ExchangeName"),
                Utils.ReadVector(lines, "ExchangeForward"),
                Utils.ReadVector(lines, "ExchangeUp"),
                Utils.ReadVectorList(lines, "ExchangeApproachingWaypoints"));

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
                $"Status={(int)Status}",

                $"OrderId={OrderId}",
                $"OrderWarehouse={OrderWarehouse}",
                $"OrderWarehouseParking={Utils.VectorToStr(OrderWarehouseParking)}",
                $"OrderCustomer={OrderCustomer}",
                $"OrderCustomerParking={Utils.VectorToStr(OrderCustomerParking)}",

                $"ExchangeName={Exchange.Exchange}",
                $"ExchangeForward={Utils.VectorToStr(Exchange.Forward)}",
                $"ExchangeUp={Utils.VectorToStr(Exchange.Up)}",
                $"ExchangeApproachingWaypoints={Utils.VectorListToStr(Exchange.ApproachingWaypoints)}",
                $"ExchangeDepartingWaypoints={Utils.VectorListToStr(Exchange.DepartingWaypoints)}",

                $"AlignFwd={Utils.VectorToStr(AlignFwd)}",
                $"AlignUp={Utils.VectorToStr(AlignUp)}",
                $"Waypoints={Utils.VectorListToStr(Waypoints)}",
                $"OnLastWaypoint={OnLastWaypoint}",

                $"DestinationName={DestinationName}",
                $"DestinationPosition={Utils.VectorToStr(DestinationPosition)}",
                $"OnDestinationArrival={OnDestinationArrival}",
            };

            return string.Join($"{'¬'}", parts);
        }
        public void Clear()
        {
            ClearOrder();
            ClearExchange();
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

        public void SetExchange(ExchangeInfo info)
        {
            Exchange = info;
        }
        public void ClearExchange()
        {
            Exchange = new ExchangeInfo();
        }

        public void PrepareNavigationFromExchange(string onLastWaypoint)
        {
            AlignFwd = Exchange.Forward;
            AlignUp = Exchange.Up;
            Waypoints.Clear();
            Waypoints.AddRange(Exchange.DepartingWaypoints);
            OnLastWaypoint = onLastWaypoint;
        }
        public void PrepareNavigationToExchange(string onLastWaypoint)
        {
            AlignFwd = Exchange.Forward;
            AlignUp = Exchange.Up;
            Waypoints.Clear();
            Waypoints.AddRange(Exchange.ApproachingWaypoints);
            OnLastWaypoint = onLastWaypoint;
        }

        public void PrepareNavigationToWarehouse(string onDestinationArrival)
        {
            DestinationName = OrderWarehouse;
            DestinationPosition = OrderWarehouseParking;
            OnDestinationArrival = onDestinationArrival;
        }
        public void PrepareNavigationToCustomer(string onDestinationArrival)
        {
            DestinationName = OrderCustomer;
            DestinationPosition = OrderCustomerParking;
            OnDestinationArrival = onDestinationArrival;
        }

        public void UpdateSpeedAndPosition(double speed, Vector3D position)
        {
            this.speed = speed;
            this.position = position;
        }
        public string GetDeliveryState()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Status}");

            if (Status == DeliveryStatus.RouteToUnload || Status == DeliveryStatus.RouteToLoad)
            {
                string origin = Status == DeliveryStatus.RouteToUnload ? OrderWarehouse : OrderCustomer;
                string destination = Status == DeliveryStatus.RouteToUnload ? OrderCustomer : OrderWarehouse;
                Vector3D originPosition = Status == DeliveryStatus.RouteToUnload ? OrderWarehouseParking : OrderCustomerParking;
                Vector3D destinationPosition = Status == DeliveryStatus.RouteToUnload ? OrderCustomerParking : OrderWarehouseParking;
                double distanceToOrigin = Vector3D.Distance(position, originPosition);
                double distanceToDestination = Vector3D.Distance(position, destinationPosition);
                TimeSpan time = speed > 0.01 ? TimeSpan.FromSeconds(distanceToDestination / speed) : TimeSpan.Zero;

                sb.AppendLine($"On route from [{origin}] to [{destination}]");
                sb.AppendLine($"Distance from origin: {Utils.DistanceToStr(distanceToOrigin)}.");
                sb.AppendLine($"Distance to destination: {Utils.DistanceToStr(distanceToDestination)}.");
                sb.AppendLine($"Estimated arrival: {time}.");
            }

            return sb.ToString();
        }
    }
}
