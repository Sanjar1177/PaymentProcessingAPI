using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Services;

public interface IAccountService
{
    Task<AccountBalanceResult?> GetBalanceAsync(string accountNumber, CancellationToken cancellationToken = default);
}
