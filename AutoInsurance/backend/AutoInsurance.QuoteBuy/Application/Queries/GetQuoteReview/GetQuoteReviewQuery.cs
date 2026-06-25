using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Queries.GetQuoteReview;

public record GetQuoteReviewQuery(Guid QuoteId) : IRequest<Result<QuoteReviewDto>>;
