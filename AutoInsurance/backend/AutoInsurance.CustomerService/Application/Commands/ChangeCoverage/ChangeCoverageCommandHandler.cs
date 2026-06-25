using System.Text.Json;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Commands.ChangeCoverage;

public class ChangeCoverageCommandHandler : IRequestHandler<ChangeCoverageCommand, Result<Guid>>
{
    private readonly IPolicyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCoverageCommandHandler(IPolicyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(ChangeCoverageCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (account is null || account.PolicyId != request.PolicyId)
            return Result<Guid>.Failure("Policy not found or access denied.");

        var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<Guid>.Failure("Policy not found.");

        if (policy.Status != PolicyStatus.Active)
            return Result<Guid>.Failure("Coverage changes can only be made on active policies.");

        var endorsement = new Endorsement
        {
            PolicyId = request.PolicyId,
            Type = EndorsementType.CoverageChange,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = EndorsementStatus.Pending,
            ChangeJson = JsonSerializer.Serialize(request.Changes)
        };

        await _repository.AddEndorsementAsync(endorsement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(endorsement.Id);
    }
}
