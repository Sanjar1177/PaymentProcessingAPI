using Microsoft.Extensions.Configuration;
using PaymentProcessingAPI.Services;
using Xunit;

namespace PaymentProcessingAPI.Tests;

public sealed class DemoBankPaymentClientTests
{
    private static readonly CreateBankPaymentRequest Request = new(
        "EXT_100001", "ACC_DEBIT_001", "ACC_CREDIT_001", 150000m, 860);

    [Fact]
    public async Task SubmitAsync_ReturnsSuccessfulResultByDefault()
    {
        // Arrange
        var client = CreateClient("Success");

        // Act
        var result = await client.SubmitAsync(Request, CancellationToken.None);

        // Assert
        Assert.Equal(BankPaymentStatus.Succeeded, result.Status);
        Assert.Equal("DEMO-EXT_100001", result.ExternalReference);
    }

    [Fact]
    public async Task SubmitAsync_ReturnsDeclineWhenConfigured()
    {
        // Arrange
        var client = CreateClient("Reject");

        // Act
        var result = await client.SubmitAsync(Request, CancellationToken.None);

        // Assert
        Assert.Equal(BankPaymentStatus.Rejected, result.Status);
        Assert.Equal("DECLINED", result.ErrorCode);
    }

    [Fact]
    public async Task TimeoutAfterExecution_StatusCanBeReconciledWithoutResubmitting()
    {
        // Arrange
        var client = CreateClient("TimeoutAfterExecution");

        // Act
        var exception = await Record.ExceptionAsync(() => client.SubmitAsync(Request, CancellationToken.None));
        var result = await client.GetStatusAsync(Request.TransactionId, CancellationToken.None);

        // Assert
        Assert.IsType<TimeoutException>(exception);
        Assert.NotNull(result);
        Assert.Equal(BankPaymentStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task SubmitAsync_IsIdempotentForSameTransactionId()
    {
        // Arrange
        var client = CreateClient("Success");

        // Act
        var first = await client.SubmitAsync(Request, CancellationToken.None);
        var retry = await client.SubmitAsync(Request, CancellationToken.None);

        // Assert
        Assert.Equal(first, retry);
    }

    [Fact]
    public async Task ConcurrentSubmissionsWithSameTransactionId_ReturnSameResult()
    {
        // Arrange
        var client = CreateClient("Success");

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => client.SubmitAsync(Request, CancellationToken.None)));

        // Assert
        Assert.All(results, result => Assert.Equal(results[0], result));
    }

    private static DemoBankPaymentClient CreateClient(string mode)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BankService:DemoMode"] = mode })
            .Build();
        return new DemoBankPaymentClient(configuration);
    }
}
