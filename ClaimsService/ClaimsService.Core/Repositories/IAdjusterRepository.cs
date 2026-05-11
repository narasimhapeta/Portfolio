// ClaimsService.Core/Repositories/IAdjusterRepository.cs
using ClaimsService.Core.Models;

namespace ClaimsService.Core.Repositories;

public interface IAdjusterRepository
{
    Task<Adjuster?> GetByIdAsync(string id);
    Task<IEnumerable<Adjuster>> GetAllAsync();
    Task UpsertAsync(Adjuster adjuster);
}
