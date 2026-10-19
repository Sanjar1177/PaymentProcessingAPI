namespace PaymentProcessingAPI.Entities;

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public int AttemptNumber { get; set; }
    public PaymentAttemptStatus Status { get; set; } = PaymentAttemptStatus.Started;
    public string? ExternalReference { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public Payment Payment { get; set; } = null!;
}

public enum PaymentAttemptStatus
{
    Started,
    Succeeded,
    Rejected,
    TimedOut,
    Unknown
}
