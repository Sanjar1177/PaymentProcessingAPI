namespace PaymentProcessingAPI.Entities;

public sealed class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Payment Payment { get; set; } = null!;
    public Account Account { get; set; } = null!;
}

public enum LedgerEntryType
{
    Debit,
    Credit
}
