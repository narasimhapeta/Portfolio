using FractureGuard.Api.Models;

namespace FractureGuard.Api.Services;

public interface IAnalysisJobService
{
    Task PublishAsync(AnalysisRequest request);
}
