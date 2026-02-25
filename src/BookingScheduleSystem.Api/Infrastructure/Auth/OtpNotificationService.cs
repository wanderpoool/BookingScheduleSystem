using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Service for sending OTP notifications via email (SMTP or AWS SES) and SMS (Semaphore).
/// Email provider is selected by the "EmailProvider" config setting: "Smtp" (default) or "Ses".
/// Falls back to console logging in Development when no provider is configured.
/// </summary>
public class OtpNotificationService
{
    private readonly ILogger<OtpNotificationService> _logger;
    private readonly IAmazonSimpleEmailService? _sesClient;
    private readonly ISmsService _smsService;
    private readonly AwsNotificationOptions _awsOptions;
    private readonly SmtpEmailOptions _smtpOptions;
    private readonly string _emailProvider;
    private readonly bool _isDevelopment;

    public OtpNotificationService(
        ILogger<OtpNotificationService> logger,
        IConfiguration configuration,
        IHostEnvironment environment,
        ISmsService smsService,
        IAmazonSimpleEmailService? sesClient = null)
    {
        _logger = logger;
        _sesClient = sesClient;
        _smsService = smsService;
        _isDevelopment = environment.IsDevelopment();
        _emailProvider = configuration["EmailProvider"] ?? "Smtp";
        _awsOptions = configuration.GetSection(AwsNotificationOptions.SectionName).Get<AwsNotificationOptions>()
                      ?? new AwsNotificationOptions();
        _smtpOptions = configuration.GetSection(SmtpEmailOptions.SectionName).Get<SmtpEmailOptions>()
                       ?? new SmtpEmailOptions();
    }

    public async Task<bool> SendEmailOtpAsync(string email, string otpCode, string purpose)
    {
        if (string.Equals(_emailProvider, "Ses", StringComparison.OrdinalIgnoreCase))
            return await SendEmailViaSesAsync(email, otpCode, purpose);

        return await SendEmailViaSmtpAsync(email, otpCode, purpose);
    }

    private async Task<bool> SendEmailViaSmtpAsync(string email, string otpCode, string purpose)
    {
        try
        {
            if (_isDevelopment && string.IsNullOrEmpty(_smtpOptions.Username))
            {
                LogOtpToConsole("EMAIL-SMTP", email, otpCode, purpose);
                return true;
            }

            var htmlBody = GetEmailTemplate(otpCode, purpose);
            var subject = $"Your verification code: {otpCode}";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpOptions.SenderName, _smtpOptions.SenderEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = $"Your verification code is: {otpCode}. This code will expire in 10 minutes."
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

            _logger.LogInformation("Email OTP sent via SMTP to {Email}", MaskEmail(email));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email OTP via SMTP to {Email}", MaskEmail(email));
            if (_isDevelopment)
                LogOtpFallback("EMAIL-SMTP", email, otpCode, purpose);
            return false;
        }
    }

    private async Task<bool> SendEmailViaSesAsync(string email, string otpCode, string purpose)
    {
        try
        {
            if (_isDevelopment && _sesClient is null)
            {
                LogOtpToConsole("EMAIL-SES", email, otpCode, purpose);
                return true;
            }

            ArgumentNullException.ThrowIfNull(_sesClient);

            var htmlBody = GetEmailTemplate(otpCode, purpose);
            var subject = $"Your verification code: {otpCode}";

            var sendRequest = new SendEmailRequest
            {
                Source = _awsOptions.SenderEmail,
                Destination = new Destination { ToAddresses = [email] },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content { Charset = "UTF-8", Data = htmlBody },
                        Text = new Content { Charset = "UTF-8", Data = $"Your verification code is: {otpCode}. This code will expire in 10 minutes." }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(sendRequest);
            _logger.LogInformation("Email OTP sent via SES to {Email}, MessageId: {MessageId}",
                MaskEmail(email), response.MessageId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email OTP via SES to {Email}", MaskEmail(email));
            if (_isDevelopment)
                LogOtpFallback("EMAIL-SES", email, otpCode, purpose);
            return false;
        }
    }

    public async Task<bool> SendSmsOtpAsync(string phoneNumber, string otpCode, string purpose)
    {
        var message = $"Your verification code is: {otpCode}. Valid for 10 min. Never share this code.";
        var result = await _smsService.SendSmsAsync(phoneNumber, message);

        if (!result)
        {
            _logger.LogError("Failed to send SMS OTP to {PhoneNumber} for {Purpose}", MaskPhone(phoneNumber), purpose);
            if (_isDevelopment)
                LogOtpFallback("SMS", phoneNumber, otpCode, purpose);
        }

        return result;
    }

    private void LogOtpToConsole(string channel, string recipient, string otpCode, string purpose)
    {
        _logger.LogWarning("=== {Channel} OTP (DEV MODE) ===", channel);
        _logger.LogWarning("To: {Recipient}", recipient);
        _logger.LogWarning("Purpose: {Purpose}", purpose);
        _logger.LogWarning("OTP Code: {OtpCode}", otpCode);
        _logger.LogWarning("Expires in 10 minutes.");
        _logger.LogWarning("==========================================");
    }

    private void LogOtpFallback(string channel, string recipient, string otpCode, string purpose)
    {
        _logger.LogWarning("OTP Fallback [{Channel}] To: {Recipient}, Purpose: {Purpose}, Code: {OtpCode}",
            channel, recipient, purpose, otpCode);
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2 || parts[0].Length < 2) return "***@***";
        return parts[0][..2] + new string('*', Math.Max(parts[0].Length - 2, 1)) + "@" + parts[1];
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 4) return "***";
        return new string('*', phone.Length - 4) + phone[^4..];
    }

    private string GetEmailTemplate(string otpCode, string purpose)
    {
        var action = purpose switch
        {
            "registration" => "complete your registration",
            "login" => "sign in to your account",
            "password-reset" => "reset your password",
            _ => "verify your identity"
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2563EB; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .otp-code {{ background-color: white; border: 2px solid #2563EB; border-radius: 8px; padding: 20px; text-align: center; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #2563EB; margin: 20px 0; }}
        .warning {{ color: #F59E0B; font-size: 14px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Verification Code</h1>
        </div>
        <div class=""content"">
            <p>Hello,</p>
            <p>You requested a verification code to {action}. Please use the following code:</p>
            <div class=""otp-code"">{otpCode}</div>
            <p>This code will expire in <strong>10 minutes</strong>.</p>
            <p class=""warning"">If you didn't request this code, please ignore this email and ensure your account is secure.</p>
            <p>Best regards,<br>BookMeApp Team</p>
        </div>
    </div>
</body>
</html>";
    }
}

public sealed class AwsNotificationOptions
{
    public const string SectionName = "AwsNotification";

    public string SenderEmail { get; set; } = "noreply@bookmeapp.com";
    public string AwsRegion { get; set; } = "ap-southeast-1";
}

public sealed class SmtpEmailOptions
{
    public const string SectionName = "SmtpEmail";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SenderEmail { get; set; } = "noreply@bookmeapp.com";
    public string SenderName { get; set; } = "BookMeApp";
}
