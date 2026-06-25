using AutoInsurance.Payment.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Payment.Application.Queries.GetPaymentHistory;

public record GetPaymentHistoryQuery(Guid PolicyId) : IRequest<Result<List<PaymentTransactionDto>>>;
