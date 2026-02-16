using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Service for sending OTP notifications via AWS SES (email) and AWS SNS (SMS).
/// Falls back to console logging in Development environment.
/// </summary>
public class OtpNotificationService
{
    private readonly ILogger<OtpNotificationService> _logger;
    private readonly IAmazonSimpleEmailService? _sesClient;
    private readonly IAmazonSimpleNotificationService? _snsClient;
    private readonly AwsNotificationOptions _options;
    private readonly bool _isDevelopment;

    public OtpNotificationService(
        ILogger<OtpNotificationService> logger,
        IConfiguration configuration,
        IHostEnvironment environment,
        IAmazonSimpleEmailService? sesClient = null,
        IAmazonSimpleNotificationService? snsClient = null)
    {
        _logger = logger;
        _sesClient = sesClient;
        _snsClient = snsClient;
        _isDevelopment = environment.IsDevelopment();
        _options = configuration.GetSection(AwsNotificationOptions.SectionName).Get<AwsNotificationOptions>()
                   ?? new AwsNotificationOptions();
    }

    public async Task<bool> SendEmailOtpAsync(string email, string otpCode, string purpose)
    {
        try
        {
            if (_isDevelopment && _sesClient is null)
            {
                LogOtpToConsole("EMAIL", email, otpCode, purpose);
                return true;
            }

            ArgumentNullException.ThrowIfNull(_sesClient);

            var htmlBody = GetEmailTemplate(otpCode, purpose);
            var subject = $"Your verification code: {otpCode}";

            var sendRequest = new SendEmailRequest
            {
                Source = _options.SenderEmail,
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
            _logger.LogInformation("Email OTP sent to {Email}, SES MessageId: {MessageId}",
                MaskEmail(email), response.MessageId);
            LogOtpFallback("EMAIL", email, otpCode, purpose);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email OTP to {Email}", MaskEmail(email));
            LogOtpFallback("EMAIL", email, otpCode, purpose);
            return false;
        }
    }

    public async Task<bool> SendSmsOtpAsync(string phoneNumber, string otpCode, string purpose)
    {
        try
        {
            if (_isDevelopment && _snsClient is null)
            {
                LogOtpToConsole("SMS", phoneNumber, otpCode, purpose);
                return true;
            }

            ArgumentNullException.ThrowIfNull(_snsClient);

            var message = $"[BookMeApp] Your verification code is: {otpCode}. This code will expire in 10 minutes.";

            var publishRequest = new PublishRequest
            {
                PhoneNumber = phoneNumber,
                Message = message,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["AWS.SNS.SMS.SenderID"] = new()
                    {
                        StringValue = _options.SmsSenderId,
                        DataType = "String"
                    },
                    ["AWS.SNS.SMS.SMSType"] = new()
                    {
                        StringValue = "Transactional",
                        DataType = "String"
                    }
                }
            };

            var response = await _snsClient.PublishAsync(publishRequest);
            _logger.LogInformation("SMS OTP sent to {PhoneNumber}, SNS MessageId: {MessageId}",
                MaskPhone(phoneNumber), response.MessageId);
            LogOtpFallback("SMS", phoneNumber, otpCode, purpose);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS OTP to {PhoneNumber}", MaskPhone(phoneNumber));
            LogOtpFallback("SMS", phoneNumber, otpCode, purpose);
            return false;
        }
    }

    private void LogOtpToConsole(string channel, string recipient, string otpCode, string purpose)
    {
        _logger.LogWarning("=== {Channel} OTP (DEV MODE - No AWS) ===", channel);
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
    public string SmsSenderId { get; set; } = "BookMeApp";
    public string AwsRegion { get; set; } = "ap-southeast-1";
}
