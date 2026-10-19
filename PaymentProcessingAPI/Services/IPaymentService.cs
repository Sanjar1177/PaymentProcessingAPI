using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Services;

public interface IPaymentService
{
    Task<PaymentResult> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentResult?> GetStatusAsync(string transactionId, CancellationToken cancellationToken = default);
}
