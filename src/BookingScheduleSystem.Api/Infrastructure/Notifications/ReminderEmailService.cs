using BookingScheduleSystem.Api.Infrastructure.Auth;
using MailKit.Security;
using MimeKit;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

/// <summary>
/// Sends reminder emails ~1 hour before confirmed appointments.
/// Separate from BookingEmailNotificationService which handles approval-token emails.
/// </summary>
public sealed class ReminderEmailService
{
    private readonly ILogger<ReminderEmailService> _logger;
    private readonly SmtpEmailOptions _smtpOptions;
    private readonly bool _isDevelopment;

    public ReminderEmailService(
        ILogger<ReminderEmailService> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
        _smtpOptions = configuration.GetSection(SmtpEmailOptions.SectionName).Get<SmtpEmailOptions>()
                       ?? new SmtpEmailOptions();
    }

    public async Task SendCustomerReminderAsync(
        string email,
        string customerFirstName,
        string scheduleTitle,
        DateTime startTime,
        DateTime endTime,
        string? providerFirstName,
        string? referenceNumber)
    {
        var subject = $"Reminder: {scheduleTitle} — today at {startTime:h:mm tt}";
        var withProvider = string.IsNullOrEmpty(providerFirstName)
            ? ""
            : $" with {System.Net.WebUtility.HtmlEncode(providerFirstName)}";

        var htmlBody = GetReminderTemplate(
            customerFirstName,
            scheduleTitle,
            startTime,
            endTime,
            referenceNumber,
            $"This is a friendly reminder that your appointment{withProvider} is coming up soon.",
            "Customer");

        var textBody = $"Reminder: Your appointment for \"{scheduleTitle}\" is today at {startTime:h:mm tt}.";
        await SendEmailAsync(email, subject, htmlBody, textBody);
    }

    public async Task SendProviderReminderAsync(
        string email,
        string providerFirstName,
        string scheduleTitle,
        DateTime startTime,
        DateTime endTime,
        string customerFirstName,
        string customerLastName,
        string? referenceNumber)
    {
        var subject = $"Reminder: {scheduleTitle} — today at {startTime:h:mm tt}";
        var htmlBody = GetReminderTemplate(
            providerFirstName,
            scheduleTitle,
            startTime,
            endTime,
            referenceNumber,
            $"You have an upcoming appointment with {System.Net.WebUtility.HtmlEncode(customerFirstName)} {System.Net.WebUtility.HtmlEncode(customerLastName)}.",
            "Provider");

        var textBody = $"Reminder: You have an appointment for \"{scheduleTitle}\" with {customerFirstName} {customerLastName} today at {startTime:h:mm tt}.";
        await SendEmailAsync(email, subject, htmlBody, textBody);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        try
        {
            if (_isDevelopment && string.IsNullOrEmpty(_smtpOptions.Username))
            {
                _logger.LogWarning("=== REMINDER EMAIL (DEV MODE) ===");
                _logger.LogWarning("To: {Email}", toEmail);
                _logger.LogWarning("Subject: {Subject}", subject);
                _logger.LogWarning("Body (text): {TextBody}", textBody);
                _logger.LogWarning("=================================");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpOptions.SenderName, _smtpOptions.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
            var socketOptions = _smtpOptions.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
            await smtpClient.ConnectAsync(_smtpOptions.Host, _smtpOptions.Port, socketOptions);
            await smtpClient.AuthenticateAsync(_smtpOptions.Username, _smtpOptions.Password);
            await smtpClient.SendAsync(message);
            await smtpClient.DisconnectAsync(true);

            _logger.LogInformation("Reminder email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reminder email to {Email}: {Subject}", toEmail, subject);
        }
    }

    private static string GetReminderTemplate(
        string recipientFirstName,
        string scheduleTitle,
        DateTime startTime,
        DateTime endTime,
        string? referenceNumber,
        string contextMessage,
        string recipientRole)
    {
        var refSection = string.IsNullOrEmpty(referenceNumber)
            ? ""
            : $@"<tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">Booking Ref</td><td style=""padding: 8px 12px; font-family: monospace; font-weight: bold;"">{System.Net.WebUtility.HtmlEncode(referenceNumber)}</td></tr>";

        return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0;"">
    <div style=""max-width: 600px; margin: 0 auto; padding: 20px;"">
        <div style=""background-color: #2563EB; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
            <h1 style=""margin: 0; font-size: 22px;"">Appointment Reminder</h1>
        </div>
        <div style=""background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;"">
            <p>Hi {System.Net.WebUtility.HtmlEncode(recipientFirstName)},</p>
            <p>{contextMessage}</p>
            <table style=""width: 100%; border-collapse: collapse; margin: 20px 0; background: white; border-radius: 8px; overflow: hidden; border: 1px solid #e5e7eb;"">
                {refSection}
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Service</td><td style=""padding: 8px 12px; font-weight: bold;"">{System.Net.WebUtility.HtmlEncode(scheduleTitle)}</td></tr>
                <tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">Date</td><td style=""padding: 8px 12px;"">{startTime:MMM dd, yyyy}</td></tr>
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Time</td><td style=""padding: 8px 12px;"">{startTime:h:mm tt} - {endTime:h:mm tt}</td></tr>
            </table>
            <p style=""color: #9ca3af; font-size: 13px;"">If you need to make changes, please visit your dashboard or contact us.</p>
            <p style=""margin-top: 20px;"">Best regards,<br>BookMeApp Team</p>
        </div>
    </div>
</body>
</html>";
    }
}
