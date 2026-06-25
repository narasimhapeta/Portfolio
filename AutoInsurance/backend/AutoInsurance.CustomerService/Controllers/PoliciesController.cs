using AutoInsurance.CustomerService.Application.Commands.ChangeCoverage;
using AutoInsurance.CustomerService.Application.Commands.RenewPolicy;
using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.CustomerService.Application.Queries.GetPolicies;
using AutoInsurance.CustomerService.Application.Queries.GetPolicyDetail;
using AutoInsurance.CustomerService.Application.Queries.GetPolicyDocuments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsurance.CustomerService.Controllers;

[ApiController]
[Route("api/policies")]
public class PoliciesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PoliciesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string B2CObjectId =>
        User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
        ?? "dev-b2c-object-id-001";

    [HttpGet]
    public async Task<IActionResult> GetPolicies(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPoliciesQuery(B2CObjectId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPolicy(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPolicyDetailQuery(id, B2CObjectId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPolicyDocumentsQuery(id, B2CObjectId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPut("{id:guid}/coverages")]
    public async Task<IActionResult> ChangeCoverages(Guid id, [FromBody] List<CoverageChangeDto> changes, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ChangeCoverageCommand(id, B2CObjectId, changes), cancellationToken);
        return result.IsSuccess ? Ok(new { endorsementId = result.Value }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/renew")]
    public async Task<IActionResult> RenewPolicy(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RenewPolicyCommand(id, B2CObjectId), cancellationToken);
        return result.IsSuccess ? Ok(new { renewalRequestId = result.Value }) : BadRequest(new { error = result.Error });
    }
}
