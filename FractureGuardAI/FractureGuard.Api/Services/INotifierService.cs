namespace FractureGuard.Api.Services;

public interface INotifierService
{
    Task SendReportAsync(string sessionId, string reportContent);
}
