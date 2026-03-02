using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using MailKit.Security;
using MimeKit;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public sealed class BookingEmailNotificationService
{
    private readonly ILogger<BookingEmailNotificationService> _logger;
    private readonly BookingActionTokenService _tokenService;
    private readonly SmtpEmailOptions _smtpOptions;
    private readonly string _emailProvider;
    private readonly string _baseUrl;
    private readonly bool _isDevelopment;

    public BookingEmailNotificationService(
        ILogger<BookingEmailNotificationService> logger,
        IConfiguration configuration,
        IHostEnvironment environment,
        BookingActionTokenService tokenService)
    {
        _logger = logger;
        _tokenService = tokenService;
        _isDevelopment = environment.IsDevelopment();
        _emailProvider = configuration["EmailProvider"] ?? "Smtp";
        _baseUrl = configuration["App:BaseUrl"] ?? "http://localhost:5059";
        _smtpOptions = configuration.GetSection(SmtpEmailOptions.SectionName).Get<SmtpEmailOptions>()
                       ?? new SmtpEmailOptions();
    }

    public async Task SendBookingApprovalEmailAsync(
        User provider,
        Booking booking,
        Schedule schedule,
        User customer)
    {
        var approveToken = _tokenService.GenerateToken(booking.Id, "approve");
        var rejectToken = _tokenService.GenerateToken(booking.Id, "reject");
        var approveUrl = $"{_baseUrl}/api/bookings/action?token={approveToken}";
        var rejectUrl = $"{_baseUrl}/api/bookings/action?token={rejectToken}";

        var (contactLabel, contactValue) = GetCustomerContact(customer);

        var subject = $"New Booking Request: {schedule.Title}";
        var htmlBody = GetApprovalEmailTemplate(
            provider.FirstName,
            customer.FirstName,
            customer.LastName,
            contactLabel,
            contactValue,
            schedule.Title,
            schedule.StartTime,
            schedule.EndTime,
            booking.Notes,
            booking.ReferenceNumber,
            approveUrl,
            rejectUrl);

        var refLabel = !string.IsNullOrEmpty(booking.ReferenceNumber) ? $" (Ref: {booking.ReferenceNumber})" : "";
        var textBody = $"New booking request{refLabel} from {customer.FirstName} {customer.LastName} for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt}. Approve: {approveUrl} Reject: {rejectUrl}";

        await SendEmailAsync(provider.Email, subject, htmlBody, textBody);
    }

    public async Task SendBookingConfirmedEmailAsync(
        User provider,
        Booking booking,
        Schedule schedule,
        User customer)
    {
        var (contactLabel, contactValue) = GetCustomerContact(customer);

        var subject = $"Booking Auto-Confirmed: {schedule.Title}";
        var htmlBody = GetConfirmedEmailTemplate(
            provider.FirstName,
            customer.FirstName,
            customer.LastName,
            contactLabel,
            contactValue,
            schedule.Title,
            schedule.StartTime,
            schedule.EndTime,
            booking.Notes,
            booking.ReferenceNumber);

        var refLabel = !string.IsNullOrEmpty(booking.ReferenceNumber) ? $" (Ref: {booking.ReferenceNumber})" : "";
        var textBody = $"Booking auto-confirmed{refLabel} for {customer.FirstName} {customer.LastName} - \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt}.";

        await SendEmailAsync(provider.Email, subject, htmlBody, textBody);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        try
        {
            if (_isDevelopment && string.IsNullOrEmpty(_smtpOptions.Username))
            {
                _logger.LogWarning("=== BOOKING EMAIL (DEV MODE) ===");
                _logger.LogWarning("To: {Email}", toEmail);
                _logger.LogWarning("Subject: {Subject}", subject);
                _logger.LogWarning("Body (text): {TextBody}", textBody);
                _logger.LogWarning("================================");
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

            _logger.LogInformation("Booking email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking email to {Email}: {Subject}", toEmail, subject);
        }
    }

    private static (string Label, string Value) GetCustomerContact(User customer)
    {
        if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            // If user has a real email (not a phone stored in email field), prefer email
            var hasRealEmail = !string.IsNullOrWhiteSpace(customer.Email)
                && customer.Email.Contains('@');
            return hasRealEmail
                ? ("Email", customer.Email)
                : ("Phone", customer.PhoneNumber);
        }
        return ("Email", customer.Email);
    }

    private static string GetApprovalEmailTemplate(
        string providerFirstName,
        string customerFirstName,
        string customerLastName,
        string contactLabel,
        string contactValue,
        string scheduleTitle,
        DateTime startTime,
        DateTime endTime,
        string? notes,
        string? referenceNumber,
        string approveUrl,
        string rejectUrl)
    {
        var notesSection = string.IsNullOrWhiteSpace(notes)
            ? ""
            : $@"<tr><td style=""padding: 8px 12px; color: #6b7280;"">Notes</td><td style=""padding: 8px 12px;"">{System.Net.WebUtility.HtmlEncode(notes)}</td></tr>";

        var refSection = string.IsNullOrEmpty(referenceNumber)
            ? ""
            : $@"<tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">Booking Ref</td><td style=""padding: 8px 12px; font-family: monospace; font-weight: bold;"">{System.Net.WebUtility.HtmlEncode(referenceNumber)}</td></tr>";

        return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0;"">
    <div style=""max-width: 600px; margin: 0 auto; padding: 20px;"">
        <div style=""background-color: #2563EB; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
            <h1 style=""margin: 0; font-size: 22px;"">New Booking Request</h1>
        </div>
        <div style=""background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;"">
            <p>Hi {System.Net.WebUtility.HtmlEncode(providerFirstName)},</p>
            <p>You have a new booking request that needs your attention:</p>
            <table style=""width: 100%; border-collapse: collapse; margin: 20px 0; background: white; border-radius: 8px; overflow: hidden; border: 1px solid #e5e7eb;"">
                {refSection}
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Customer</td><td style=""padding: 8px 12px; font-weight: bold;"">{System.Net.WebUtility.HtmlEncode(customerFirstName)} {System.Net.WebUtility.HtmlEncode(customerLastName)}</td></tr>
                <tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">{contactLabel}</td><td style=""padding: 8px 12px;"">{System.Net.WebUtility.HtmlEncode(contactValue)}</td></tr>
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Service</td><td style=""padding: 8px 12px;"">{System.Net.WebUtility.HtmlEncode(scheduleTitle)}</td></tr>
                <tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">Date</td><td style=""padding: 8px 12px;"">{startTime:MMM dd, yyyy}</td></tr>
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Time</td><td style=""padding: 8px 12px;"">{startTime:h:mm tt} - {endTime:h:mm tt}</td></tr>
                {notesSection}
            </table>
            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""{approveUrl}"" style=""display: block; background-color: #16a34a; color: white; padding: 14px 32px; text-decoration: none; border-radius: 6px; font-weight: bold; margin: 0 auto 12px auto; max-width: 220px;"">Accept Booking</a>
                <a href=""{rejectUrl}"" style=""display: block; background-color: #dc2626; color: white; padding: 14px 32px; text-decoration: none; border-radius: 6px; font-weight: bold; margin: 0 auto; max-width: 220px;"">Decline Booking</a>
            </div>
            <p style=""color: #9ca3af; font-size: 13px; text-align: center;"">These links expire in 3 hours. After expiration, use the app to manage this booking.</p>
            <p style=""margin-top: 20px;"">Best regards,<br>BookMeApp Team</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GetConfirmedEmailTemplate(
        string providerFirstName,
        string customerFirstName,
        string customerLastName,
        string contactLabel,
        string contactValue,
        string scheduleTitle,
        DateTime startTime,
        DateTime endTime,
        string? notes,
        string? referenceNumber)
    {
        var notesSection = string.IsNullOrWhiteSpace(notes)
            ? ""
            : $@"<tr><td style=""padding: 8px 12px; color: #6b7280;"">Notes</td><td style=""padding: 8px 12px;"">{System.Net.WebUtility.HtmlEncode(notes)}</td></tr>";

        var refSection = string.IsNullOrEmpty(referenceNumber)
            ? ""
            : $@"<tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">Booking Ref</td><td style=""padding: 8px 12px; font-family: monospace; font-weight: bold;"">{System.Net.WebUtility.HtmlEncode(referenceNumber)}</td></tr>";

        return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0;"">
    <div style=""max-width: 600px; margin: 0 auto; padding: 20px;"">
        <div style=""background-color: #16a34a; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
            <h1 style=""margin: 0; font-size: 22px;"">Booking Auto-Confirmed</h1>
        </div>
        <div style=""background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;"">
            <p>Hi {System.Net.WebUtility.HtmlEncode(providerFirstName)},</p>
            <p>A new booking has been automatically confirmed based on your auto-accept setting:</p>
            <table style=""width: 100%; border-collapse: collapse; margin: 20px 0; background: white; border-radius: 8px; overflow: hidden; border: 1px solid #e5e7eb;"">
                {refSection}
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Customer</td><td style=""padding: 8px 12px; font-weight: bold;"">{System.Net.WebUtility.HtmlEncode(customerFirstName)} {System.Net.WebUtility.HtmlEncode(customerLastName)}</td></tr>
                <tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">{contactLabel}</td><td style=""padding: 8px 12px;"">{System.Net.WebUtility.HtmlEncode(contactValue)}</td></tr>
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Service</td><td style=""padding: 8px 12px;"">{System.Net.WebUtility.HtmlEncode(scheduleTitle)}</td></tr>
                <tr style=""background: #f9fafb;""><td style=""padding: 8px 12px; color: #6b7280;"">Date</td><td style=""padding: 8px 12px;"">{startTime:MMM dd, yyyy}</td></tr>
                <tr><td style=""padding: 8px 12px; color: #6b7280;"">Time</td><td style=""padding: 8px 12px;"">{startTime:h:mm tt} - {endTime:h:mm tt}</td></tr>
                {notesSection}
            </table>
            <p style=""color: #6b7280; font-size: 14px;"">No action needed — this booking is already confirmed. You can manage it from your dashboard.</p>
            <p style=""margin-top: 20px;"">Best regards,<br>BookMeApp Team</p>
        </div>
    </div>
</body>
</html>";
    }
}
