namespace PaymentProcessingAPI.Services;

public class PaymentServiceException(string message) : Exception(message);

public sealed class InvalidPaymentException(string message) : PaymentServiceException(message);

public sealed class AccountNotFoundException(string accountNumber)
    : PaymentServiceException($"Счет «{accountNumber}» не найден.");

public sealed class IdempotencyConflictException(string transactionId)
    : PaymentServiceException($"Идентификатор транзакции «{transactionId}» уже использован с другими параметрами платежа.");
