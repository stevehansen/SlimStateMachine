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
}