using System.Security.Cryptography;
using System.Text;
using BookingScheduleSystem.Contracts.Common;
using Microsoft.Extensions.Options;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public sealed class BookingActionTokenOptions
{
    public const string SectionName = "BookingActionToken";

    public string SecretKey { get; set; } = "";
    public int ExpirationHours { get; set; } = 48;
}

public sealed record BookingActionPayload(BookingId BookingId, string Action, DateTime ExpiresAtUtc);

public sealed class BookingActionTokenService
{
    private const string DefaultPlaceholderKey = "CHANGE-THIS-IN-PRODUCTION-MIN-32-CHARS";
    private readonly byte[] _secretKey;
    private readonly int _expirationHours;

    public BookingActionTokenService(IOptions<BookingActionTokenOptions> options)
    {
        var key = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(key) || key == DefaultPlaceholderKey)
            throw new InvalidOperationException(
                "BookingActionToken:SecretKey must be configured with a unique secret (min 32 chars). " +
                "Do not use the default placeholder.");

        if (key.Length < 32)
            throw new InvalidOperationException(
                "BookingActionToken:SecretKey must be at least 32 characters.");

        _secretKey = Encoding.UTF8.GetBytes(key);
        _expirationHours = options.Value.ExpirationHours;
    }

    public string GenerateToken(BookingId bookingId, string action)
    {
        var expiresAt = DateTime.UtcNow.AddHours(_expirationHours);
        var payload = $"{bookingId.Value}|{action}|{expiresAt.Ticks}";
        var signatureBytes = ComputeSignatureBytes(payload);
        var combined = Encoding.UTF8.GetBytes(payload + "|")
            .Concat(signatureBytes)
            .ToArray();
        return Convert.ToBase64String(combined)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public BookingActionPayload? ValidateToken(string token)
    {
        try
        {
            // Restore Base64 padding
            var base64 = token.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var rawBytes = Convert.FromBase64String(base64);

            // HMAC-SHA256 produces 32 bytes; the signature is the last 32 bytes
            const int signatureLength = 32;
            if (rawBytes.Length <= signatureLength)
                return null;

            var payloadWithPipeBytes = rawBytes[..^signatureLength];
            var receivedSignatureBytes = rawBytes[^signatureLength..];

            // Remove trailing '|' separator from payload
            var payloadStr = Encoding.UTF8.GetString(payloadWithPipeBytes);
            if (!payloadStr.EndsWith('|'))
                return null;
            payloadStr = payloadStr[..^1];

            var parts = payloadStr.Split('|');
            if (parts.Length != 3)
                return null;

            var bookingIdStr = parts[0];
            var action = parts[1];
            var ticksStr = parts[2];

            // Verify signature with constant-time comparison on fixed-length byte arrays
            var expectedSignatureBytes = ComputeSignatureBytes(payloadStr);
            if (!CryptographicOperations.FixedTimeEquals(receivedSignatureBytes, expectedSignatureBytes))
                return null;

            // Check ticks bounds before creating DateTime
            if (!long.TryParse(ticksStr, out var ticks))
                return null;

            if (ticks < 0 || ticks > DateTime.MaxValue.Ticks)
                return null;

            var expiresAt = new DateTime(ticks, DateTimeKind.Utc);
            if (DateTime.UtcNow > expiresAt)
                return null;

            if (!Guid.TryParse(bookingIdStr, out var bookingGuid))
                return null;

            return new BookingActionPayload(
                new BookingId(bookingGuid),
                action,
                expiresAt);
        }
        catch
        {
            return null;
        }
    }

    private byte[] ComputeSignatureBytes(string payload)
    {
        using var hmac = new HMACSHA256(_secretKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }
}
