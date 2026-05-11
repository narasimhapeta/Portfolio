// ClaimsService.Core/Repositories/IClaimRepository.cs
using ClaimsService.Core.Models;

namespace ClaimsService.Core.Repositories;

public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(string id, string customerId);
    Task<Claim?> GetByIdCrossPartitionAsync(string id);
    Task<IEnumerable<Claim>> GetAllAsync(string? status = null);
    Task<Claim> CreateAsync(Claim claim);
    Task<Claim> UpdateAsync(Claim claim);
}
