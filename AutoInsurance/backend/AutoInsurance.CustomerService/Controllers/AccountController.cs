using AutoInsurance.CustomerService.Application.Commands.LinkAccount;
using AutoInsurance.CustomerService.Application.Queries.GetAccount;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsurance.CustomerService.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string B2CObjectId =>
        User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
        ?? "dev-b2c-object-id-001";

    [HttpGet]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAccountQuery(B2CObjectId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost("link")]
    public async Task<IActionResult> LinkAccount([FromBody] LinkAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LinkAccountCommand(B2CObjectId, request.PolicyId, request.Email), cancellationToken);
        return result.IsSuccess ? Ok(new { accountId = result.Value }) : BadRequest(new { error = result.Error });
    }
}

public record LinkAccountRequest(Guid PolicyId, string Email);
