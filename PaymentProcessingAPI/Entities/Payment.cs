namespace PaymentProcessingAPI.Entities;

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransactionId { get; set; } = string.Empty;
    public Guid SenderAccountId { get; set; }
    public Guid ReceiverAccountId { get; set; }
    public decimal Amount { get; set; }
    public int CurrencyCode { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ExternalReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Account SenderAccount { get; set; } = null!;
    public Account ReceiverAccount { get; set; } = null!;
    public ICollection<PaymentAttempt> Attempts { get; set; } = new List<PaymentAttempt>();
    public ICollection<PaymentStatusHistory> StatusHistory { get; set; } = new List<PaymentStatusHistory>();
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Unknown
}
