namespace PaymentProcessingAPI.Services.Contracts;

public sealed record CreatePaymentRequest(
    string TransactionId,
    string SenderAccount,
    string ReceiverAccount,
    decimal Amount,
    int Currency);
