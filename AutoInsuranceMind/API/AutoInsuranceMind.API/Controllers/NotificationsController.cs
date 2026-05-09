using AutoInsuranceMind.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsuranceMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("renewal-reminder")]
    public async Task<IActionResult> SendRenewalReminder([FromQuery] string customerId = "cust-001")
    {
        await _notificationService.SendPolicyRenewalReminderAsync(customerId);
        return Ok(new { success = true, message = "Renewal reminder sent (check server logs)" });
    }

    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
    {
        await _notificationService.SendEmailNotificationAsync(request.Email, request.Subject, request.Body);
        return Ok(new { success = true, message = "Email sent (check server logs)" });
    }
}

public class EmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
