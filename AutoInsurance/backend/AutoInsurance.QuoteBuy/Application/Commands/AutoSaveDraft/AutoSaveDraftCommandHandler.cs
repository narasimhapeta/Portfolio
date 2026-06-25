using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.AutoSaveDraft;

public class AutoSaveDraftCommandHandler : IRequestHandler<AutoSaveDraftCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AutoSaveDraftCommandHandler(IQuoteRepository quoteRepository, IUnitOfWork unitOfWork)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AutoSaveDraftCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetWithDraftAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result.Failure("Quote not found.");

        if (quote.Draft is null)
        {
            quote.Draft = new Domain.Quote.QuoteDraft
            {
                QuoteId = quote.Id,
                StepReached = request.StepReached,
                DraftStateJson = request.DraftStateJson,
                UpdatedAt = DateTime.UtcNow
            };
        }
        else
        {
            quote.Draft.DraftStateJson = request.DraftStateJson;
            quote.Draft.StepReached = Math.Max(quote.Draft.StepReached, request.StepReached);
            quote.Draft.UpdatedAt = DateTime.UtcNow;
        }

        _quoteRepository.Update(quote);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
