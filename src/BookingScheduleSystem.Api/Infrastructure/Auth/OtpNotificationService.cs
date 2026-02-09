namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Service for sending OTP notifications via email and SMS
/// In production, integrate with SendGrid, Twilio, or similar services
/// </summary>
public class OtpNotificationService
{
    private readonly ILogger<OtpNotificationService> _logger;

    public OtpNotificationService(ILogger<OtpNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendEmailOtpAsync(string email, string otpCode, string purpose)
    {
        try
        {
            // TODO: Integrate with email service (SendGrid, Mailgun, etc.)
            // For now, just log the OTP (NEVER do this in production!)
            _logger.LogInformation("=== EMAIL OTP ===");
            _logger.LogInformation("To: {Email}", email);
            _logger.LogInformation("Purpose: {Purpose}", purpose);
            _logger.LogInformation("OTP Code: {OtpCode}", otpCode);
            _logger.LogInformation("This OTP will expire in 10 minutes.");
            _logger.LogInformation("================");

            // Simulate email sending delay
            await Task.Delay(500);

            // In production, replace with actual email sending:
            /*
            var message = new SendGridMessage();
            message.SetFrom("noreply@yourdomain.com", "Booking System");
            message.AddTo(email);
            message.SetSubject($"Your verification code: {otpCode}");
            message.AddContent(MimeType.Html, GetEmailTemplate(otpCode, purpose));

            var client = new SendGridClient(apiKey);
            var response = await client.SendEmailAsync(message);
            return response.IsSuccessStatusCode;
            */

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email OTP to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendSmsOtpAsync(string phoneNumber, string otpCode, string purpose)
    {
        try
        {
            // TODO: Integrate with SMS service (Twilio, Semaphore, Vonage, etc.)
            // For now, just log the OTP (NEVER do this in production!)
            _logger.LogInformation("=== SMS OTP ===");
            _logger.LogInformation("To: {PhoneNumber}", phoneNumber);
            _logger.LogInformation("Purpose: {Purpose}", purpose);
            _logger.LogInformation("OTP Code: {OtpCode}", otpCode);
            _logger.LogInformation("This OTP will expire in 10 minutes.");
            _logger.LogInformation("===============");

            // Simulate SMS sending delay
            await Task.Delay(500);

            // In production, replace with actual SMS sending (e.g., Twilio):
            /*
            var twilioClient = new TwilioRestClient(accountSid, authToken);
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(fromPhoneNumber),
                body: $"Your verification code is: {otpCode}. This code will expire in 10 minutes."
            );
            return message.Status != MessageResource.StatusEnum.Failed;
            */

            // For Philippines, you can use Semaphore SMS API:
            /*
            var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("apikey", semaphoreApiKey),
                new KeyValuePair<string, string>("number", phoneNumber),
                new KeyValuePair<string, string>("message", $"Your OTP is: {otpCode}. Valid for 10 minutes.")
            });

            var response = await client.PostAsync("https://api.semaphore.co/api/v4/messages", content);
            return response.IsSuccessStatusCode;
            */

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS OTP to {PhoneNumber}", phoneNumber);
            return false;
        }
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
            <p class=""warning"">⚠️ If you didn't request this code, please ignore this email and ensure your account is secure.</p>
            <p>Best regards,<br>Booking System Team</p>
        </div>
    </div>
</body>
</html>";
    }
}
