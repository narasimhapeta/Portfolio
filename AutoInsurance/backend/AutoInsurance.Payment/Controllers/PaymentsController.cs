using AutoInsurance.Payment.Application.Commands.ConfirmPayment;
using AutoInsurance.Payment.Application.Commands.InitiatePayment;
using AutoInsurance.Payment.Application.Commands.SetBillingSchedule;
using AutoInsurance.Payment.Application.Queries.GetPaymentHistory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsurance.Payment.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmPaymentCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{policyId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid policyId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPaymentHistoryQuery(policyId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost("{policyId:guid}/schedule")]
    public async Task<IActionResult> SetSchedule(Guid policyId, [FromBody] SetScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetBillingScheduleCommand(policyId, request.Frequency), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}

public record SetScheduleRequest(string Frequency);
