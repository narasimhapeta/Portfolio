using AutoInsurance.Claims.Application.Commands.SubmitClaim;
using AutoInsurance.Claims.Application.Commands.UpdateClaimStatus;
using AutoInsurance.Claims.Application.Commands.UploadClaimDocument;
using AutoInsurance.Claims.Application.Queries.GetClaimDetail;
using AutoInsurance.Claims.Application.Queries.GetClaims;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsurance.Claims.Controllers;

[ApiController]
[Route("api/claims")]
public class ClaimsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClaimsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitClaimCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(new { claimId = result.Value }) : BadRequest(new { error = result.Error });
    }

    [HttpGet]
    public async Task<IActionResult> GetByPolicy([FromQuery] Guid policyId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetClaimsQuery(policyId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{claimId:guid}")]
    public async Task<IActionResult> GetDetail(Guid claimId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetClaimDetailQuery(claimId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost("{claimId:guid}/documents")]
    public async Task<IActionResult> UploadDocument(Guid claimId, [FromBody] UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UploadClaimDocumentCommand(claimId, request.DocumentType, request.Base64Content, request.FileName), cancellationToken);
        return result.IsSuccess ? Ok(new { documentId = result.Value }) : BadRequest(new { error = result.Error });
    }

    [HttpPatch("{claimId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid claimId, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateClaimStatusCommand(claimId, request.Status), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}

public record UploadDocumentRequest(string DocumentType, string Base64Content, string FileName);
public record UpdateStatusRequest(string Status);
