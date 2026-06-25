using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.AutoSaveDraft;

public record AutoSaveDraftCommand(Guid QuoteId, string DraftStateJson, int StepReached) : IRequest<Result>;
