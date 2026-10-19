using Microsoft.EntityFrameworkCore;

namespace PaymentProcessingAPI.Data;

public static class DefaultAccountSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var accounts = new[]
        {
            new { AccountNumber = "ACC_DEBIT_001", CurrencyCode = 860, Balance = 500000m },
            new { AccountNumber = "ACC_CREDIT_001", CurrencyCode = 860, Balance = 100000m }
        };

        foreach (var account in accounts)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Accounts"
                    ("Id", "AccountNumber", "CurrencyCode", "Balance", "ReservedBalance", "IsActive", "CreatedAtUtc")
                VALUES
                    ({Guid.NewGuid()}, {account.AccountNumber}, {account.CurrencyCode}, {account.Balance}, 0, TRUE, {DateTime.UtcNow})
                ON CONFLICT ("AccountNumber") DO NOTHING
                """, cancellationToken);
        }
    }
}
