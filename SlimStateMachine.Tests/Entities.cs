namespace SlimStateMachine.Tests;

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    Cancelled
}

public class Invoice
{
    public int Id { get; set; }
    public InvoiceStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingAmount => TotalAmount - AmountPaid;
    public bool RequiresApproval { get; set; } = false;
    public string? CancellationReason { get; set; }
    public string? CustomerName { get; set; }
}

/// <summary>
/// Entity with read-only Status property for testing error handling.
/// </summary>
public class ReadOnlyInvoice
{
    public int Id { get; set; }
    public InvoiceStatus Status { get; } = InvoiceStatus.Draft; // Read-only
}

/// <summary>
/// Second entity/enum pair for testing state machine isolation.
/// </summary>
public enum OrderStatus
{
    Created,
    Processing,
    Shipped,
    Delivered
}

public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string? CustomerName { get; set; }
}