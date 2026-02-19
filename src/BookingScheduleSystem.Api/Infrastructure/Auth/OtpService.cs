using System.Security.Cryptography;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Marten-backed OTP service for managing one-time passwords.
/// Supports multi-instance deployments and survives process restarts.
/// </summary>
public class OtpService
{
    private readonly IDocumentStore _store;
    private readonly ILogger<OtpService> _logger;
    private const int OTP_EXPIRY_MINUTES = 10;
    private const int OTP_LENGTH = 6;
    private const int RESEND_COOLDOWN_SECONDS = 60;
    private const int MAX_VERIFY_ATTEMPTS = 5;

    public OtpService(IDocumentStore store, ILogger<OtpService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<(string otpCode, DateTime expiresAt)> GenerateOtpAsync(string identifier, string purpose, CancellationToken ct = default)
    {
        var key = GetKey(identifier, purpose);
        var now = DateTime.UtcNow;

        await using var session = _store.LightweightSession();

        var existing = await session.LoadAsync<OtpRecord>(key, ct);

        // Enforce cooldown
        if (existing is not null && existing.SentAt.AddSeconds(RESEND_COOLDOWN_SECONDS) > now)
        {
            var waitSeconds = (int)(existing.SentAt.AddSeconds(RESEND_COOLDOWN_SECONDS) - now).TotalSeconds;
            _logger.LogWarning("OTP resend cooldown active for {Identifier}, {WaitSeconds}s remaining",
                MaskIdentifier(identifier), waitSeconds);
            throw new InvalidOperationException($"Please wait {waitSeconds} seconds before requesting a new code.");
        }

        var otpCode = GenerateNumericCode(OTP_LENGTH);
        var expiresAt = now.AddMinutes(OTP_EXPIRY_MINUTES);

        var record = new OtpRecord
        {
            Id = key,
            OtpCode = otpCode,
            ExpiresAt = expiresAt,
            SentAt = now,
            Attempts = 0,
            IsVerified = false,
            VerificationToken = null
        };

        session.Store(record);
        await session.SaveChangesAsync(ct);

        _logger.LogInformation("Generated OTP for {Identifier} with purpose {Purpose}, expires at {ExpiresAt}",
            MaskIdentifier(identifier), purpose, expiresAt);

        return (otpCode, expiresAt);
    }

    public async Task<(bool isValid, string? verificationToken)> VerifyOtpAsync(string identifier, string otpCode, string purpose, CancellationToken ct = default)
    {
        var key = GetKey(identifier, purpose);

        await using var session = _store.LightweightSession();

        var record = await session.LoadAsync<OtpRecord>(key, ct);

        if (record is null)
        {
            _logger.LogWarning("OTP verification failed - not found for {Identifier}", MaskIdentifier(identifier));
            return (false, null);
        }

        // Check expiry
        if (DateTime.UtcNow > record.ExpiresAt)
        {
            _logger.LogWarning("OTP expired for {Identifier}", MaskIdentifier(identifier));
            session.Delete(record);
            await session.SaveChangesAsync(ct);
            return (false, null);
        }

        // Check attempts
        record.Attempts++;
        if (record.Attempts > MAX_VERIFY_ATTEMPTS)
        {
            _logger.LogWarning("Max OTP attempts exceeded for {Identifier}", MaskIdentifier(identifier));
            session.Delete(record);
            await session.SaveChangesAsync(ct);
            return (false, null);
        }

        // Verify code
        if (record.OtpCode != otpCode)
        {
            _logger.LogWarning("Invalid OTP code for {Identifier}, attempt {Attempts}",
                MaskIdentifier(identifier), record.Attempts);
            session.Store(record); // persist updated attempt count
            await session.SaveChangesAsync(ct);
            return (false, null);
        }

        // Mark as verified and generate verification token
        record.IsVerified = true;
        var verificationToken = GenerateVerificationToken();
        record.VerificationToken = verificationToken;
        session.Store(record);
        await session.SaveChangesAsync(ct);

        _logger.LogInformation("OTP verified successfully for {Identifier}", MaskIdentifier(identifier));
        return (true, verificationToken);
    }

    public async Task<bool> ValidateVerificationTokenAsync(string identifier, string verificationToken, string purpose, CancellationToken ct = default)
    {
        var key = GetKey(identifier, purpose);

        await using var session = _store.LightweightSession();

        var record = await session.LoadAsync<OtpRecord>(key, ct);

        if (record is null)
            return false;

        var isValid = record.IsVerified &&
                      record.VerificationToken == verificationToken &&
                      DateTime.UtcNow <= record.ExpiresAt;

        if (isValid)
        {
            session.Delete(record);
            await session.SaveChangesAsync(ct);
        }

        return isValid;
    }

    private static string GetKey(string identifier, string purpose) => $"{identifier}:{purpose}";

    private static string GenerateNumericCode(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        Span<char> buffer = stackalloc char[length];

        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(bytes);
            buffer[i] = (char)('0' + BitConverter.ToUInt32(bytes, 0) % 10);
        }

        return new string(buffer);
    }

    private static string GenerateVerificationToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string MaskIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length < 4)
            return "***";

        return identifier[..2] + new string('*', identifier.Length - 4) + identifier[^2..];
    }
}
