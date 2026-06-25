using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetAccount;

public class GetAccountQueryHandler : IRequestHandler<GetAccountQuery, Result<AccountDto>>
{
    private readonly IPolicyRepository _repository;

    public GetAccountQueryHandler(IPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<AccountDto>> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (account is null)
            return Result<AccountDto>.Failure("Account not found.");

        return Result<AccountDto>.Success(new AccountDto(account.Id, account.B2CObjectId, account.Email, account.PolicyId));
    }
}
