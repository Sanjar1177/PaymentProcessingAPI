namespace PaymentProcessingAPI.Entities;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public int CurrencyCode { get; set; }
    public decimal Balance { get; set; }
    public decimal ReservedBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> SentPayments { get; set; } = new List<Payment>();
    public ICollection<Payment> ReceivedPayments { get; set; } = new List<Payment>();
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}
