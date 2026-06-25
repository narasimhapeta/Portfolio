using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Claims;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.Claims.Application.Commands.UpdateClaimStatus;

public class UpdateClaimStatusCommandHandler : IRequestHandler<UpdateClaimStatusCommand, Result>
{
    private static readonly HashSet<string> ValidStatuses =
    [
        ClaimStatus.Submitted, ClaimStatus.UnderReview,
        ClaimStatus.Approved, ClaimStatus.Denied, ClaimStatus.Closed
    ];

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new()
    {
        [ClaimStatus.Submitted] = [ClaimStatus.UnderReview, ClaimStatus.Denied],
        [ClaimStatus.UnderReview] = [ClaimStatus.Approved, ClaimStatus.Denied],
        [ClaimStatus.Approved] = [ClaimStatus.Closed],
        [ClaimStatus.Denied] = [ClaimStatus.Closed],
        [ClaimStatus.Closed] = []
    };

    private readonly IClaimRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateClaimStatusCommandHandler(IClaimRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateClaimStatusCommand request, CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(request.NewStatus))
            return Result.Failure($"Invalid status. Valid: {string.Join(", ", ValidStatuses)}");

        var claim = await _repository.GetClaimAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return Result.Failure("Claim not found.");

        if (!AllowedTransitions[claim.Status].Contains(request.NewStatus))
            return Result.Failure($"Cannot transition from '{claim.Status}' to '{request.NewStatus}'.");

        claim.Status = request.NewStatus;
        _repository.UpdateClaim(claim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
