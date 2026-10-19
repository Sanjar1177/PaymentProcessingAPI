using PaymentProcessingAPI.Entities;
using PaymentProcessingAPI.Services;
using PaymentProcessingAPI.Services.Contracts;
using Xunit;

namespace PaymentProcessingAPI.Tests;

public sealed class PaymentRulesTests
{
    private static readonly CreatePaymentRequest ValidRequest = new(
        "EXT_100001", "ACC_DEBIT_001", "ACC_CREDIT_001", 150000m, 860);

    [Fact]
    public void Validate_AcceptsValidPayment()
    {
        // Arrange
        var request = ValidRequest;

        // Act
        var exception = Record.Exception(() => PaymentRules.Validate(request));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.123)]
    public void Validate_RejectsInvalidAmount(decimal amount)
    {
        // Arrange
        var request = ValidRequest with { Amount = amount };

        // Act
        var action = () => PaymentRules.Validate(request);

        // Assert
        Assert.Throws<InvalidPaymentException>(action);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Validate_RejectsCurrencyOutsideThreeDigitRange(int currency)
    {
        // Arrange
        var request = ValidRequest with { Currency = currency };

        // Act
        var action = () => PaymentRules.Validate(request);

        // Assert
        Assert.Throws<InvalidPaymentException>(action);
    }

    [Fact]
    public void Validate_RejectsSameSenderAndReceiver()
    {
        // Arrange
        var request = ValidRequest with { ReceiverAccount = ValidRequest.SenderAccount };

        // Act
        var action = () => PaymentRules.Validate(request);

        // Assert
        Assert.Throws<InvalidPaymentException>(action);
    }

    [Fact]
    public void GetAvailableBalance_SubtractsReservedAmount()
    {
        // Arrange
        const decimal balance = 200000m;
        const decimal reservedBalance = 75000m;

        // Act
        var availableBalance = PaymentRules.GetAvailableBalance(balance, reservedBalance);

        // Assert
        Assert.Equal(125000m, availableBalance);
        Assert.True(availableBalance < ValidRequest.Amount);
    }

    [Fact]
    public void EnsureSufficientFunds_RejectsPaymentWhenAvailableAmountIsTooLow()
    {
        // Arrange
        const decimal balance = 200000m;
        const decimal reservedBalance = 75000m;

        // Act
        var exception = Assert.Throws<InvalidPaymentException>(
            () => PaymentRules.EnsureSufficientFunds(balance, reservedBalance, ValidRequest.Amount));

        // Assert
        Assert.Equal("На счете отправителя недостаточно доступных средств.", exception.Message);
    }

    [Fact]
    public void EnsureSufficientFunds_AllowsPaymentWhenAvailableAmountIsEnough()
    {
        // Arrange
        const decimal balance = 300000m;
        const decimal reservedBalance = 75000m;

        // Act
        var exception = Record.Exception(
            () => PaymentRules.EnsureSufficientFunds(balance, reservedBalance, ValidRequest.Amount));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSameRequest_AllowsRetryWithSamePaymentDetails()
    {
        // Arrange
        var existing = ExistingPayment();

        // Act
        var exception = Record.Exception(() => PaymentRules.EnsureSameRequest(existing, ValidRequest));

        // Assert
        Assert.Null(exception);
        Assert.True(PaymentRules.MatchesExistingPayment(existing, ValidRequest));
    }

    [Theory]
    [InlineData("ACC_DEBIT_002", "ACC_CREDIT_001", 150000, 860)]
    [InlineData("ACC_DEBIT_001", "ACC_CREDIT_002", 150000, 860)]
    [InlineData("ACC_DEBIT_001", "ACC_CREDIT_001", 150001, 860)]
    [InlineData("ACC_DEBIT_001", "ACC_CREDIT_001", 150000, 643)]
    public void EnsureSameRequest_RejectsRetryWithDifferentDetails(
        string sender, string receiver, decimal amount, int currency)
    {
        // Arrange
        var request = ValidRequest with
        {
            SenderAccount = sender,
            ReceiverAccount = receiver,
            Amount = amount,
            Currency = currency
        };
        var existing = ExistingPayment();

        // Act
        var action = () => PaymentRules.EnsureSameRequest(existing, request);

        // Assert
        Assert.Throws<IdempotencyConflictException>(action);
    }

    private static Payment ExistingPayment() => new()
    {
        TransactionId = ValidRequest.TransactionId,
        SenderAccount = new Account { AccountNumber = ValidRequest.SenderAccount },
        ReceiverAccount = new Account { AccountNumber = ValidRequest.ReceiverAccount },
        Amount = ValidRequest.Amount,
        CurrencyCode = ValidRequest.Currency
    };
}
