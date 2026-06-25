using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Claims;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.Claims.Application.Commands.SubmitClaim;

public class SubmitClaimCommandHandler : IRequestHandler<SubmitClaimCommand, Result<Guid>>
{
    private readonly IClaimRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitClaimCommandHandler(IClaimRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(SubmitClaimCommand request, CancellationToken cancellationToken)
    {
        var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<Guid>.Failure("Policy not found.");

        if (request.IncidentDate > DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<Guid>.Failure("Incident date cannot be in the future.");

        var claim = new Claim
        {
            PolicyId = request.PolicyId,
            IncidentDate = request.IncidentDate,
            Description = request.Description,
            Status = ClaimStatus.Submitted
        };

        await _repository.AddClaimAsync(claim, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(claim.Id);
    }
}
