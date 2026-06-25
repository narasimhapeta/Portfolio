using AutoInsurance.DocumentGeneration.Application.Commands.GenerateDocument;
using AutoInsurance.DocumentGeneration.Application.Queries.GetDocuments;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsurance.DocumentGeneration.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateDocumentCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{policyId:guid}")]
    public async Task<IActionResult> GetDocuments(Guid policyId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDocumentsQuery(policyId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
