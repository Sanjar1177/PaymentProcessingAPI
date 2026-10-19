using System.Data;
using Microsoft.EntityFrameworkCore;
using PaymentProcessingAPI.Data;
using PaymentProcessingAPI.Entities;

namespace PaymentProcessingAPI.Services;

public sealed class PaymentProcessor(
    ApplicationDbContext dbContext,
    IBankPaymentClient bankClient,
    ILogger<PaymentProcessor> logger)
{
    public async Task ProcessAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        // Захватываем платеж в базе и переводим его в Processing до сетевого вызова.
        // Это не дает двум экземплярам API одновременно начать обработку одной записи.
        var attempt = await ClaimAsync(paymentId, cancellationToken);
        if (attempt is null)
            return;

        // Читаем только данные, нужные банковскому запросу. Сетевой вызов выполняется вне транзакции БД,
        // чтобы медленный банк не удерживал блокировки счетов и платежа.
        var paymentInfo = await dbContext.Payments.AsNoTracking()
            .Where(payment => payment.Id == paymentId)
            .Select(payment => new
            {
                payment.TransactionId,
                Sender = payment.SenderAccount.AccountNumber,
                Receiver = payment.ReceiverAccount.AccountNumber,
                payment.Amount,
                Currency = payment.CurrencyCode,
                PreviousStatus = payment.Status
            })
            .SingleAsync(cancellationToken);

        try
        {
            BankPaymentResult? result = null;

            // Сначала выясняем, не выполнил ли банк операцию при прошлой попытке, ответ на которую потерялся.
            // Если банк пока не знает такой операции, отправляем запрос с тем же transactionId как ключом идемпотентности.
            if (paymentInfo.PreviousStatus is PaymentStatus.Unknown or PaymentStatus.Processing)
                result = await bankClient.GetStatusAsync(paymentInfo.TransactionId, cancellationToken);

            result ??= await bankClient.SubmitAsync(new CreateBankPaymentRequest(
                paymentInfo.TransactionId, paymentInfo.Sender, paymentInfo.Receiver, paymentInfo.Amount, paymentInfo.Currency), cancellationToken);
            await CompleteAsync(paymentId, attempt.Value, result, cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or HttpRequestException or TaskCanceledException)
        {
            // Timeout не доказывает, что банк не выполнил перевод. Поэтому резерв сохраняем и помечаем результат неизвестным.
            logger.LogWarning(exception, "Не удалось однозначно получить результат платежа {TransactionId}; средства остаются зарезервированными", paymentInfo.TransactionId);
            await MarkUnknownAsync(paymentId, attempt.Value, exception.Message, CancellationToken.None);
        }
    }

    private async Task<int?> ClaimAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        // Блокировка строки сериализует обработчики в нескольких экземплярах API.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var payment = await dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM \"Payments\" WHERE \"Id\" = {paymentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (payment is null)
            return null;

        // Зависший Processing можно восстановить после перезапуска процесса. Повторный запрос безопасен,
        // потому что банковская сторона должна дедуплицировать операции по transactionId.
        var staleCutoff = DateTime.UtcNow.AddMinutes(-2);
        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Unknown) &&
            !(payment.Status == PaymentStatus.Processing && payment.UpdatedAtUtc < staleCutoff))
            return null;

        var previousStatus = payment.Status;
        var now = DateTime.UtcNow;

        // Незавершенная попытка означает, что процесс мог остановиться после отправки запроса, но до сохранения ответа.
        // Фиксируем ее как неизвестную, а новую попытку используем для сверки с банком.
        foreach (var unfinished in await dbContext.PaymentAttempts
                     .Where(item => item.PaymentId == paymentId && item.Status == PaymentAttemptStatus.Started)
                     .ToListAsync(cancellationToken))
        {
            unfinished.Status = PaymentAttemptStatus.Unknown;
            unfinished.ErrorCode = "RESULT_UNKNOWN";
            unfinished.ErrorMessage = "Результат предыдущей попытки не был сохранен.";
            unfinished.CompletedAtUtc = now;
        }

        var attemptNumber = (await dbContext.PaymentAttempts.Where(item => item.PaymentId == paymentId)
            .Select(item => (int?)item.AttemptNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        dbContext.PaymentAttempts.Add(new PaymentAttempt
        {
            PaymentId = paymentId,
            AttemptNumber = attemptNumber,
            Status = PaymentAttemptStatus.Started,
            StartedAtUtc = now
        });
        payment.Status = PaymentStatus.Processing;
        payment.UpdatedAtUtc = now;
        dbContext.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            PaymentId = paymentId,
            PreviousStatus = previousStatus,
            Status = PaymentStatus.Processing,
            Details = "Запрос отправлен банковскому сервису.",
            ChangedAtUtc = now
        });
        // Статус Processing, запись попытки и история должны сохраниться вместе до обращения к банку.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return attemptNumber;
    }

    private async Task CompleteAsync(Guid paymentId, int attemptNumber, BankPaymentResult result, CancellationToken cancellationToken)
    {
        // Повторно блокируем платеж и счета, чтобы итог операции применялся атомарно и только один раз.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var payment = await dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM \"Payments\" WHERE \"Id\" = {paymentId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Failed)
            return;

        // Блокируем счета в том же порядке, что и PaymentService, чтобы избежать взаимных блокировок.
        var accounts = await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\" IN ({payment.SenderAccountId}, {payment.ReceiverAccountId}) ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        var sender = accounts.Single(account => account.Id == payment.SenderAccountId);
        var receiver = accounts.Single(account => account.Id == payment.ReceiverAccountId);
        var attempt = await dbContext.PaymentAttempts.SingleAsync(item => item.PaymentId == paymentId && item.AttemptNumber == attemptNumber, cancellationToken);
        var oldStatus = payment.Status;
        var now = DateTime.UtcNow;

        attempt.CompletedAtUtc = now;
        attempt.ExternalReference = result.ExternalReference;
        attempt.ErrorCode = result.ErrorCode;
        attempt.ErrorMessage = result.ErrorMessage;

        if (result.Status == BankPaymentStatus.Succeeded)
        {
            // Подтвержденный перевод превращаем из резерва в списание и зачисление.
            // Уникальные ограничения на проводки дополнительно защищают от повторного применения результата.
            sender.ReservedBalance -= payment.Amount;
            sender.Balance -= payment.Amount;
            receiver.Balance += payment.Amount;
            payment.Status = PaymentStatus.Succeeded;
            payment.ExternalReference = result.ExternalReference;
            dbContext.LedgerEntries.AddRange(
                new LedgerEntry { PaymentId = payment.Id, AccountId = sender.Id, EntryType = LedgerEntryType.Debit, Amount = payment.Amount, CreatedAtUtc = now },
                new LedgerEntry { PaymentId = payment.Id, AccountId = receiver.Id, EntryType = LedgerEntryType.Credit, Amount = payment.Amount, CreatedAtUtc = now });
            attempt.Status = PaymentAttemptStatus.Succeeded;
        }
        else
        {
            // При отказе деньги не списывались: достаточно освободить резерв отправителя.
            sender.ReservedBalance -= payment.Amount;
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = result.ErrorMessage ?? "Банковский сервис отклонил платеж.";
            attempt.Status = PaymentAttemptStatus.Rejected;
        }

        payment.UpdatedAtUtc = now;
        dbContext.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            PaymentId = payment.Id,
            PreviousStatus = oldStatus,
            Status = payment.Status,
            Details = result.Status == BankPaymentStatus.Succeeded
                ? "Банковский сервис подтвердил платеж. Средства списаны и зачислены."
                : payment.FailureReason,
            ChangedAtUtc = now
        });
        // Балансы, статус платежа, история и проводки коммитятся одной транзакцией.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Обработка платежа {TransactionId} завершена со статусом {Status}", payment.TransactionId, payment.Status);
    }

    private async Task MarkUnknownAsync(Guid paymentId, int attemptNumber, string errorMessage, CancellationToken cancellationToken)
    {
        // Сохраняем неопределенный результат без изменения балансов: сверка может подтвердить как успех, так и отказ.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var payment = await dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM \"Payments\" WHERE \"Id\" = {paymentId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Failed)
            return;

        var now = DateTime.UtcNow;
        var oldStatus = payment.Status;
        payment.Status = PaymentStatus.Unknown;
        payment.UpdatedAtUtc = now;
        payment.FailureReason = "Ответ банковского сервиса не получен. Выполняется сверка результата.";
        var attempt = await dbContext.PaymentAttempts.SingleAsync(item => item.PaymentId == paymentId && item.AttemptNumber == attemptNumber, cancellationToken);
        attempt.Status = PaymentAttemptStatus.TimedOut;
        attempt.ErrorCode = "TIMEOUT";
        attempt.ErrorMessage = errorMessage;
        attempt.CompletedAtUtc = now;
        dbContext.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            PaymentId = paymentId,
            PreviousStatus = oldStatus,
            Status = PaymentStatus.Unknown,
            Details = "Ответ не получен. Средства остаются зарезервированными до сверки с банковским сервисом.",
            ChangedAtUtc = now
        });
        // В истории остается причина ожидания, а резерв удерживает деньги от повторного расходования.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
