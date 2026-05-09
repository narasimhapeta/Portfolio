using AutoInsuranceMind.API.Data;

namespace AutoInsuranceMind.API.Services;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailNotificationAsync(string email, string subject, string body)
    {
        // Mock: log to console — replace with Azure Communication Services in production
        _logger.LogInformation("[EMAIL] To: {Email} | Subject: {Subject}", email, subject);
        _logger.LogInformation("[EMAIL] Body: {Body}", body);
        await Task.Delay(50);
    }

    public async Task SendSmsNotificationAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("[SMS] To: {Phone} | Message: {Message}", phoneNumber, message);
        await Task.Delay(50);
    }

    public async Task SendPolicyRenewalReminderAsync(string customerId)
    {
        var customer = MockDataStore.Customers.FirstOrDefault(c => c.Id == customerId);
        if (customer == null) return;

        var policies = MockDataStore.Policies
            .Where(p => p.CustomerId == customerId && p.Status == "active")
            .ToList();

        foreach (var policy in policies)
        {
            var daysUntilExpiry = (policy.EndDate - DateTime.UtcNow).Days;
            var subject = $"Policy Renewal Reminder - {policy.PolicyNumber}";
            var body = $"Dear {customer.Name}, your policy {policy.PolicyNumber} expires in {daysUntilExpiry} days. " +
                       $"Please log in to renew your coverage and avoid a lapse. Premium: ${policy.Premium:F2}/year.";

            await SendEmailNotificationAsync(customer.Email, subject, body);

            if (!string.IsNullOrEmpty(customer.PhoneNumber))
                await SendSmsNotificationAsync(customer.PhoneNumber, $"Policy {policy.PolicyNumber} expires in {daysUntilExpiry} days. Renew now.");
        }
    }

    public async Task SendCoverageUpdateNotificationAsync(string customerId, string policyNumber, string coverageType)
    {
        var customer = MockDataStore.Customers.FirstOrDefault(c => c.Id == customerId);
        if (customer == null) return;

        var subject = $"Coverage Updated - {policyNumber}";
        var body = $"Dear {customer.Name}, your {coverageType} coverage on policy {policyNumber} has been updated successfully.";

        await SendEmailNotificationAsync(customer.Email, subject, body);
    }
}
