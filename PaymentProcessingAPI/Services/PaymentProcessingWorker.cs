using Microsoft.EntityFrameworkCore;
using PaymentProcessingAPI.Data;
using PaymentProcessingAPI.Entities;

namespace PaymentProcessingAPI.Services;

public sealed class PaymentProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<PaymentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var paymentIds = await dbContext.Payments.AsNoTracking()
                    .Where(payment => payment.Status == PaymentStatus.Pending || payment.Status == PaymentStatus.Unknown ||
                                      (payment.Status == PaymentStatus.Processing && payment.UpdatedAtUtc < DateTime.UtcNow.AddMinutes(-2)))
                    .OrderBy(payment => payment.CreatedAtUtc)
                    .Select(payment => payment.Id)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var paymentId in paymentIds)
                {
                    await using var itemScope = scopeFactory.CreateAsyncScope();
                    try
                    {
                        await itemScope.ServiceProvider.GetRequiredService<PaymentProcessor>().ProcessAsync(paymentId, stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(exception, "Не удалось обработать платеж {PaymentId}; он будет повторно проверен", paymentId);
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Ошибка фоновой обработки платежей");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
