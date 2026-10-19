using Microsoft.AspNetCore.Mvc;
using PaymentProcessingAPI.Services;
using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await paymentService.CreateAsync(request, cancellationToken);
        return AcceptedAtAction(nameof(GetStatus), new { transactionId = result.TransactionId }, result);
    }

    [HttpGet("{transactionId}")]
    [ProducesResponseType(typeof(PaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string transactionId, CancellationToken cancellationToken)
    {
        var result = await paymentService.GetStatusAsync(transactionId, cancellationToken);
        return result is null
            ? NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Платеж не найден." })
            : Ok(result);
    }
}
