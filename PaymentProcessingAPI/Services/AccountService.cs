using Microsoft.EntityFrameworkCore;
using PaymentProcessingAPI.Data;
using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Services;

public sealed class AccountService(ApplicationDbContext dbContext) : IAccountService
{
    public async Task<AccountBalanceResult?> GetBalanceAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new InvalidPaymentException("Не указан идентификатор счета.");

        return await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.AccountNumber == accountNumber)
            .Select(account => new AccountBalanceResult(account.AccountNumber, account.Balance - account.ReservedBalance, account.CurrencyCode))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
