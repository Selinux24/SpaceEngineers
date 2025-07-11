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
        public readonly List<Vector3D> OrderToWarehouse = new List<Vector3D>();
        public string OrderCustomer;
        public readonly List<Vector3D> OrderToCustomer = new List<Vector3D>();

        public readonly ExchangeInfo Exchange = new ExchangeInfo();

        public Vector3D AlignFwd;
        public Vector3D AlignUp;
        public readonly List<Vector3D> Waypoints = new List<Vector3D>();
        public string OnLastWaypoint;

        public string DestinationName;
        public readonly List<Vector3D> DestinationWaypoints = new List<Vector3D>();
        public int CurrentDestinationWaypoint;
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
            OrderToWarehouse.Clear();
            OrderToWarehouse.AddRange(Utils.ReadVectorList(lines, "OrderToWarehouse"));
            OrderCustomer = Utils.ReadString(lines, "OrderCustomer");
            OrderToCustomer.Clear();
            OrderToCustomer.AddRange(Utils.ReadVectorList(lines, "OrderToCustomer"));

            Exchange.Initialize(
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
            DestinationWaypoints.Clear();
            DestinationWaypoints.AddRange(Utils.ReadVectorList(lines, "DestinationWaypoints"));
            OnDestinationArrival = Utils.ReadString(lines, "OnDestinationArrival");
        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                $"Status={(int)Status}",

                $"OrderId={OrderId}",
                $"OrderWarehouse={OrderWarehouse}",
                $"OrderToWarehouse={Utils.VectorListToStr(OrderToWarehouse)}",
                $"OrderCustomer={OrderCustomer}",
                $"OrderToCustomer={Utils.VectorListToStr(OrderToCustomer)}",

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
                $"DestinationWaypoints={Utils.VectorListToStr(DestinationWaypoints)}",
                $"OnDestinationArrival={OnDestinationArrival}",
            };

            return string.Join($"{'¬'}", parts);
        }
        public void Clear()
        {
            ClearOrder();
            ClearExchange();
        }

        public void SetOrder(int orderId, string warehouse, List<Vector3D> toWarehouse, string customer, List<Vector3D> toCustomer)
        {
            OrderId = orderId;
            OrderWarehouse = warehouse;
            OrderToWarehouse.Clear();
            OrderToWarehouse.AddRange(toWarehouse);
            OrderCustomer = customer;
            OrderToCustomer.Clear();
            OrderToCustomer.AddRange(toCustomer);
        }
        public void ClearOrder()
        {
            OrderId = -1;
            OrderWarehouse = null;
            OrderToWarehouse.Clear();
            OrderCustomer = null;
            OrderToCustomer.Clear();
        }

        public void SetExchange(ExchangeInfo info)
        {
            Exchange.Initialize(info);
        }
        public void ClearExchange()
        {
            Exchange.Clear();
        }

        public void PrepareNavigationFromExchange(string onLastWaypoint)
        {
            AlignFwd = Exchange.Forward;
            AlignUp = Exchange.Up;
            Waypoints.Clear();
            Waypoints.AddRange(Exchange.DepartingWaypoints);
            CurrentDestinationWaypoint = 0;
            OnLastWaypoint = onLastWaypoint;
        }
        public void PrepareNavigationToExchange(string onLastWaypoint)
        {
            AlignFwd = Exchange.Forward;
            AlignUp = Exchange.Up;
            Waypoints.Clear();
            Waypoints.AddRange(Exchange.ApproachingWaypoints);
            CurrentDestinationWaypoint = 0;
            OnLastWaypoint = onLastWaypoint;
        }

        public void PrepareNavigationToWarehouse(string onDestinationArrival)
        {
            DestinationName = OrderWarehouse;
            DestinationWaypoints.Clear();
            DestinationWaypoints.AddRange(OrderToWarehouse);
            OnDestinationArrival = onDestinationArrival;
        }
        public void PrepareNavigationToCustomer(string onDestinationArrival)
        {
            DestinationName = OrderCustomer;
            DestinationWaypoints.Clear();
            DestinationWaypoints.AddRange(OrderToCustomer);
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
                var firstWaypoint = Waypoints.Count > 0 ? Waypoints[0] : Vector3D.Zero;
                var lastWaypoint = Waypoints.Count > 1 ? Waypoints[Waypoints.Count - 1] : Vector3D.Zero;

                string origin = Status == DeliveryStatus.RouteToUnload ? OrderWarehouse : OrderCustomer;
                string destination = Status == DeliveryStatus.RouteToUnload ? OrderCustomer : OrderWarehouse;
                Vector3D originPosition = Status == DeliveryStatus.RouteToUnload ? firstWaypoint : lastWaypoint;
                Vector3D destinationPosition = Status == DeliveryStatus.RouteToUnload ? lastWaypoint : firstWaypoint;
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
