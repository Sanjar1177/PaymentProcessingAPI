namespace PaymentProcessingAPI.Services;

public enum BankPaymentStatus
{
    Succeeded,
    Rejected
}

public sealed record BankPaymentResult(BankPaymentStatus Status, string? ExternalReference, string? ErrorCode, string? ErrorMessage);

public interface IBankPaymentClient
{
    Task<BankPaymentResult?> GetStatusAsync(string transactionId, CancellationToken cancellationToken);
    Task<BankPaymentResult> SubmitAsync(CreateBankPaymentRequest request, CancellationToken cancellationToken);
}

public sealed record CreateBankPaymentRequest(
    string TransactionId,
    string SenderAccount,
    string ReceiverAccount,
    decimal Amount,
    int Currency);
