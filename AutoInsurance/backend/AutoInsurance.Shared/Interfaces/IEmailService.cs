namespace AutoInsurance.Shared.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody, string? AttachmentUrl = null);

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
