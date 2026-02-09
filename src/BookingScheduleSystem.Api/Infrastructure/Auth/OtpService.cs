using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// In-memory OTP service for managing one-time passwords
/// In production, use Redis or a database for distributed scenarios
/// </summary>
public class OtpService
{
    private readonly ConcurrentDictionary<string, OtpEntry> _otpStore = new();
    private readonly ILogger<OtpService> _logger;
    private const int OTP_EXPIRY_MINUTES = 10;
    private const int OTP_LENGTH = 6;

    public OtpService(ILogger<OtpService> logger)
    {
        _logger = logger;
    }

    public (string otpCode, DateTime expiresAt) GenerateOtp(string identifier, string purpose)
    {
        // Generate 6-digit OTP
        var otpCode = GenerateNumericCode(OTP_LENGTH);
        var expiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES);

        var key = GetKey(identifier, purpose);
        var entry = new OtpEntry
        {
            OtpCode = otpCode,
            ExpiresAt = expiresAt,
            Attempts = 0,
            IsVerified = false
        };

        _otpStore[key] = entry;
        _logger.LogInformation("Generated OTP for {Identifier} with purpose {Purpose}, expires at {ExpiresAt}",
            MaskIdentifier(identifier), purpose, expiresAt);

        // Clean up expired entries periodically
        CleanupExpiredEntries();

        return (otpCode, expiresAt);
    }

    public (bool isValid, string? verificationToken) VerifyOtp(string identifier, string otpCode, string purpose)
    {
        var key = GetKey(identifier, purpose);

        if (!_otpStore.TryGetValue(key, out var entry))
        {
            _logger.LogWarning("OTP verification failed - not found for {Identifier}", MaskIdentifier(identifier));
            return (false, null);
        }

        // Check expiry
        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            _logger.LogWarning("OTP expired for {Identifier}", MaskIdentifier(identifier));
            _otpStore.TryRemove(key, out _);
            return (false, null);
        }

        // Check attempts (max 5)
        if (entry.Attempts >= 5)
        {
            _logger.LogWarning("Max OTP attempts exceeded for {Identifier}", MaskIdentifier(identifier));
            _otpStore.TryRemove(key, out _);
            return (false, null);
        }

        entry.Attempts++;

        // Verify code
        if (entry.OtpCode != otpCode)
        {
            _logger.LogWarning("Invalid OTP code for {Identifier}, attempt {Attempts}",
                MaskIdentifier(identifier), entry.Attempts);
            return (false, null);
        }

        // Mark as verified and generate verification token
        entry.IsVerified = true;
        var verificationToken = GenerateVerificationToken();
        entry.VerificationToken = verificationToken;

        _logger.LogInformation("OTP verified successfully for {Identifier}", MaskIdentifier(identifier));
        return (true, verificationToken);
    }

    public bool ValidateVerificationToken(string identifier, string verificationToken, string purpose)
    {
        var key = GetKey(identifier, purpose);

        if (!_otpStore.TryGetValue(key, out var entry))
        {
            return false;
        }

        // Token must be verified and match
        var isValid = entry.IsVerified && entry.VerificationToken == verificationToken &&
                      DateTime.UtcNow <= entry.ExpiresAt;

        if (isValid)
        {
            // Clean up after successful validation
            _otpStore.TryRemove(key, out _);
        }

        return isValid;
    }

    private string GetKey(string identifier, string purpose) => $"{identifier}:{purpose}";

    private string GenerateNumericCode(int length)
    {
        var code = string.Empty;
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];

        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(bytes);
            var randomNumber = BitConverter.ToUInt32(bytes, 0);
            code += (randomNumber % 10).ToString();
        }

        return code;
    }

    private string GenerateVerificationToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private string MaskIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length < 4)
            return "***";

        // Mask middle characters
        return identifier.Substring(0, 2) + new string('*', identifier.Length - 4) + identifier.Substring(identifier.Length - 2);
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _otpStore
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _otpStore.TryRemove(key, out _);
        }
    }

    private class OtpEntry
    {
        public required string OtpCode { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public bool IsVerified { get; set; }
        public string? VerificationToken { get; set; }
    }
}
