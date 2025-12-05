namespace SlimStateMachine.Samples.Models;

public enum OrderStatus
{
    Created,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Returned,
    Cancelled
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<string> Items { get; set; } = [];
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public bool PaymentReceived { get; set; }
    public bool InStock { get; set; } = true;
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string? CancellationReason { get; set; }
}
