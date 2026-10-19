using System.Collections.Concurrent;

namespace PaymentProcessingAPI.Services;

public sealed class DemoBankPaymentClient(IConfiguration configuration) : IBankPaymentClient
{
    private readonly ConcurrentDictionary<string, BankPaymentResult> _results = new(StringComparer.Ordinal);

    public Task<BankPaymentResult?> GetStatusAsync(string transactionId, CancellationToken cancellationToken)
    {
        _results.TryGetValue(transactionId, out var result);
        return Task.FromResult(result);
    }

    public Task<BankPaymentResult> SubmitAsync(CreateBankPaymentRequest request, CancellationToken cancellationToken)
    {
        var mode = configuration["BankService:DemoMode"] ?? "Success";
        var result = _results.GetOrAdd(request.TransactionId, _ => mode.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            ? new BankPaymentResult(BankPaymentStatus.Rejected, null, "DECLINED", "Демо-банк отклонил платеж.")
            : new BankPaymentResult(BankPaymentStatus.Succeeded, $"DEMO-{request.TransactionId}", null, null));

        if (mode.Equals("TimeoutAfterExecution", StringComparison.OrdinalIgnoreCase))
            throw new TimeoutException("Демо-банк выполнил платеж, но ответ не был получен вовремя.");

        return Task.FromResult(result);
    }
}
