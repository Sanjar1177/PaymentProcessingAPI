using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using PaymentProcessingAPI.Data;
using PaymentProcessingAPI.Entities;
using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Services;

public sealed class PaymentService(ApplicationDbContext dbContext, ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResult> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Проверяем формат до обращения к базе, чтобы отклонять некорректный запрос сразу.
        PaymentRules.Validate(request);

        // Повтор с тем же transactionId должен вернуть уже созданный платеж, не резервируя деньги повторно.
        var existing = await FindByTransactionIdAsync(request.TransactionId, cancellationToken);
        if (existing is not null)
            return ReturnExistingOrThrow(existing, request);

        // Сначала находим счета, чтобы получить их идентификаторы для блокировки в фиксированном порядке.
        var senderSnapshot = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
            account => account.AccountNumber == request.SenderAccount, cancellationToken)
            ?? throw new AccountNotFoundException(request.SenderAccount);

        var receiverSnapshot = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
            account => account.AccountNumber == request.ReceiverAccount, cancellationToken)
            ?? throw new AccountNotFoundException(request.ReceiverAccount);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Блокируем обе строки в одном и том же порядке, чтобы параллельные платежи не зарезервировали одни и те же средства.
        var lockedAccounts = await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\" IN ({senderSnapshot.Id}, {receiverSnapshot.Id}) ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        var sender = lockedAccounts.Single(account => account.Id == senderSnapshot.Id);
        var receiver = lockedAccounts.Single(account => account.Id == receiverSnapshot.Id);

        if (!sender.IsActive || !receiver.IsActive)
            throw new InvalidPaymentException("Оба счета должны быть активны.");
        if (sender.CurrencyCode != request.Currency || receiver.CurrencyCode != request.Currency)
            throw new InvalidPaymentException("Валюта платежа должна совпадать с валютой обоих счетов.");
        PaymentRules.EnsureSufficientFunds(sender.Balance, sender.ReservedBalance, request.Amount);

        // На время ожидания ответа банка сумма недоступна для других платежей, но баланс пока не списывается.
        var payment = new Payment
        {
            TransactionId = request.TransactionId,
            SenderAccountId = sender.Id,
            ReceiverAccountId = receiver.Id,
            SenderAccount = sender,
            ReceiverAccount = receiver,
            Amount = request.Amount,
            CurrencyCode = request.Currency,
            Status = PaymentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        sender.ReservedBalance += request.Amount;
        dbContext.Payments.Add(payment);
        dbContext.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            Payment = payment,
            PaymentId = payment.Id,
            PreviousStatus = null,
            Status = PaymentStatus.Pending,
            Details = "Платеж принят. Средства зарезервированы до обработки банковским сервисом.",
            ChangedAtUtc = DateTime.UtcNow
        });
        try
        {
            // Сохраняем резерв, платеж и начальную запись истории в одной транзакции.
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Платеж {TransactionId} сохранен со статусом {Status}", payment.TransactionId, payment.Status);
            return Map(payment, isDuplicate: false);
        }
        catch (DbUpdateException ex) when (IsTransactionIdConflict(ex))
        {
            // Уникальный индекс transactionId защищает от гонки, если другой экземпляр API сохранил платеж одновременно.
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Параллельный запрос уже сохранил платеж {TransactionId}", request.TransactionId);
            var concurrentPayment = await FindByTransactionIdAsync(request.TransactionId, cancellationToken);
            if (concurrentPayment is null)
                throw;

            return ReturnExistingOrThrow(concurrentPayment, request);
        }
    }

    public async Task<PaymentResult?> GetStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new InvalidPaymentException("Не указан идентификатор транзакции.");

        // Неизвестный идентификатор преобразуется контроллером в ответ 404.
        var payment = await FindByTransactionIdAsync(transactionId, cancellationToken);
        return payment is null ? null : Map(payment, isDuplicate: false);
    }

    private Task<Payment?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken) =>
        dbContext.Payments
            .AsNoTracking()
            .Include(payment => payment.SenderAccount)
            .Include(payment => payment.ReceiverAccount)
            .SingleOrDefaultAsync(payment => payment.TransactionId == transactionId, cancellationToken);

    private static PaymentResult ReturnExistingOrThrow(Payment payment, CreatePaymentRequest request)
    {
        // Один ключ идемпотентности нельзя повторно использовать для другого набора параметров.
        PaymentRules.EnsureSameRequest(payment, request);

        return Map(payment, isDuplicate: true);
    }

    private static PaymentResult Map(Payment payment, bool isDuplicate) => new(
        payment.TransactionId,
        payment.SenderAccount.AccountNumber,
        payment.ReceiverAccount.AccountNumber,
        payment.Amount,
        payment.CurrencyCode,
        payment.Status,
        payment.CreatedAtUtc,
        isDuplicate);

    private static bool IsTransactionIdConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Payments_TransactionId"
        };
}
