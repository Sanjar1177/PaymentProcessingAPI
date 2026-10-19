namespace PaymentProcessingAPI.Entities;

public sealed class PaymentStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public PaymentStatus? PreviousStatus { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Details { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public Payment Payment { get; set; } = null!;
}
