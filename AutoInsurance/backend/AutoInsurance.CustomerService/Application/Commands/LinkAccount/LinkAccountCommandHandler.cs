using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Commands.LinkAccount;

public class LinkAccountCommandHandler : IRequestHandler<LinkAccountCommand, Result<Guid>>
{
    private readonly IPolicyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public LinkAccountCommandHandler(IPolicyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(LinkAccountCommand request, CancellationToken cancellationToken)
    {
        var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<Guid>.Failure("Policy not found.");

        var existing = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (existing is not null)
            return Result<Guid>.Success(existing.Id);

        var account = new UserAccount
        {
            B2CObjectId = request.B2CObjectId,
            PolicyId = request.PolicyId,
            Email = request.Email
        };

        await _repository.AddUserAccountAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(account.Id);
    }
}
