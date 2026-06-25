using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Commands.RenewPolicy;

public class RenewPolicyCommandHandler : IRequestHandler<RenewPolicyCommand, Result<Guid>>
{
    private readonly IPolicyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RenewPolicyCommandHandler(IPolicyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RenewPolicyCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (account is null || account.PolicyId != request.PolicyId)
            return Result<Guid>.Failure("Policy not found or access denied.");

        var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<Guid>.Failure("Policy not found.");

        if (policy.Status != PolicyStatus.Active)
            return Result<Guid>.Failure("Only active policies can be renewed.");

        var hasPending = await _repository.HasPendingRenewalAsync(request.PolicyId, cancellationToken);
        if (hasPending)
            return Result<Guid>.Failure("A renewal is already pending for this policy.");

        var renewal = new RenewalRequest
        {
            PolicyId = request.PolicyId,
            NewEffectiveDate = policy.ExpirationDate.AddDays(1),
            Status = RenewalStatus.Pending
        };

        await _repository.AddRenewalRequestAsync(renewal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(renewal.Id);
    }
}
