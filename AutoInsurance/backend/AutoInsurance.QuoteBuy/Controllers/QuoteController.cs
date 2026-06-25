using AutoInsurance.QuoteBuy.Application.Commands.AutoSaveDraft;
using AutoInsurance.QuoteBuy.Application.Commands.BindQuote;
using AutoInsurance.QuoteBuy.Application.Commands.CreateQuote;
using AutoInsurance.QuoteBuy.Application.Commands.SaveCoverages;
using AutoInsurance.QuoteBuy.Application.Commands.SaveDrivers;
using AutoInsurance.QuoteBuy.Application.Commands.SaveVehicles;
using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.QuoteBuy.Application.Queries.GetQuoteReview;
using AutoInsurance.QuoteBuy.Application.Queries.ResumeQuote;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsurance.QuoteBuy.Controllers;

[ApiController]
[Route("api/quote")]
public class QuoteController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuoteController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuote([FromBody] CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetReview), new { id = result.Value!.QuoteId }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPatch("{id:guid}/drivers")]
    public async Task<IActionResult> SaveDrivers(Guid id, [FromBody] List<DriverDto> drivers, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveDriversCommand(id, drivers), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpPatch("{id:guid}/vehicles")]
    public async Task<IActionResult> SaveVehicles(Guid id, [FromBody] List<VehicleDto> vehicles, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveVehiclesCommand(id, vehicles), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpPatch("{id:guid}/coverages")]
    public async Task<IActionResult> SaveCoverages(Guid id, [FromBody] List<CoverageDto> coverages, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveCoveragesCommand(id, coverages), cancellationToken);
        return result.IsSuccess
            ? Ok(new { totalAnnualPremium = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> GetReview(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetQuoteReviewQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost("{id:guid}/bind")]
    public async Task<IActionResult> BindQuote(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new BindQuoteCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("resume")]
    public async Task<IActionResult> ResumeQuote([FromBody] ResumeQuoteQuery query, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPatch("{id:guid}/draft")]
    public async Task<IActionResult> AutoSaveDraft(Guid id, [FromBody] AutoSaveDraftRequest request, CancellationToken cancellationToken)
    {
        var command = new AutoSaveDraftCommand(id, request.DraftStateJson, request.StepReached);
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}

public record AutoSaveDraftRequest(string DraftStateJson, int StepReached);
