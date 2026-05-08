namespace FractureGuard.Api.Services;

public class NotifierService(HttpClient httpClient, IConfiguration config) : INotifierService
{
    public async Task SendReportAsync(string sessionId, string reportContent)
    {
        var payload = new { session_id = sessionId, report = new { content = reportContent } };
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{config["NOTIFIER_URL"]}/notify")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-webhook-secret",
            config["NOTIFIER_WEBHOOK_SECRET"] ?? "local-webhook-secret");
        await httpClient.SendAsync(request);
    }
}
