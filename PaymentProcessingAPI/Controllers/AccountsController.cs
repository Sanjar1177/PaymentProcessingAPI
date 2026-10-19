using Microsoft.AspNetCore.Mvc;
using PaymentProcessingAPI.Services;
using PaymentProcessingAPI.Services.Contracts;

namespace PaymentProcessingAPI.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(IAccountService accountService) : ControllerBase
{
    [HttpGet("{accountId}/balance")]
    [ProducesResponseType(typeof(AccountBalanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(string accountId, CancellationToken cancellationToken)
    {
        var result = await accountService.GetBalanceAsync(accountId, cancellationToken);
        return result is null
            ? NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Счет не найден." })
            : Ok(result);
    }
}
