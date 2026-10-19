using PaymentProcessingAPI.Entities;

namespace PaymentProcessingAPI.Services.Contracts;

public sealed record PaymentResult(
    string TransactionId,
    string SenderAccount,
    string ReceiverAccount,
    decimal Amount,
    int Currency,
    PaymentStatus Status,
    DateTime CreatedAtUtc,
    bool IsDuplicateRequest);
