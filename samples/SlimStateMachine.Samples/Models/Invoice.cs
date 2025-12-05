namespace SlimStateMachine.Samples.Models;

public enum InvoiceStatus
{
    Draft,
    Sent,
    PartiallyPaid,
    Paid,
    Overdue,
    Cancelled
}

public class Invoice
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? CancellationReason { get; set; }

    public decimal AmountDue => TotalAmount - AmountPaid;
    public bool IsFullyPaid => AmountPaid >= TotalAmount;
    public bool IsOverdue => DueDate.HasValue && DateTime.Now > DueDate.Value && !IsFullyPaid;
}
