namespace PaymentProcessingAPI.Services.Contracts;

public sealed record AccountBalanceResult(string AccountNumber, decimal Balance, int Currency);
