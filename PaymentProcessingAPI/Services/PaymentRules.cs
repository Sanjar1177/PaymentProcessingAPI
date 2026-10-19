using PaymentProcessingAPI.Entities;
using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Services;

public static class PaymentRules
{
    public static void Validate(CreatePaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId) || request.TransactionId.Length > 100)
            throw new InvalidPaymentException("Не указан идентификатор транзакции или его длина превышает 100 символов.");
        if (string.IsNullOrWhiteSpace(request.SenderAccount) || request.SenderAccount.Length > 64)
            throw new InvalidPaymentException("Не указан счет отправителя или его длина превышает 64 символа.");
        if (string.IsNullOrWhiteSpace(request.ReceiverAccount) || request.ReceiverAccount.Length > 64)
            throw new InvalidPaymentException("Не указан счет получателя или его длина превышает 64 символа.");
        if (string.Equals(request.SenderAccount, request.ReceiverAccount, StringComparison.Ordinal))
            throw new InvalidPaymentException("Счета отправителя и получателя должны различаться.");
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new InvalidPaymentException("Сумма должна быть положительной и содержать не более двух знаков после запятой.");
        if (request.Currency is < 1 or > 999)
            throw new InvalidPaymentException("Код валюты должен быть трехзначным числом.");
    }

    public static decimal GetAvailableBalance(decimal balance, decimal reservedBalance) => balance - reservedBalance;

    public static void EnsureSufficientFunds(decimal balance, decimal reservedBalance, decimal amount)
    {
        if (GetAvailableBalance(balance, reservedBalance) < amount)
            throw new InvalidPaymentException("На счете отправителя недостаточно доступных средств.");
    }

    public static bool MatchesExistingPayment(Payment payment, CreatePaymentRequest request) =>
        payment.SenderAccount.AccountNumber == request.SenderAccount &&
        payment.ReceiverAccount.AccountNumber == request.ReceiverAccount &&
        payment.Amount == request.Amount &&
        payment.CurrencyCode == request.Currency;

    public static void EnsureSameRequest(Payment payment, CreatePaymentRequest request)
    {
        if (!MatchesExistingPayment(payment, request))
            throw new IdempotencyConflictException(request.TransactionId);
    }
}
