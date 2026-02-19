namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Marten document for storing OTP records in PostgreSQL.
/// Supports multi-instance deployments and survives process restarts.
/// </summary>
public sealed class OtpRecord
{
    /// <summary>
    /// Composite key: "{identifier}:{purpose}" (e.g., "user@email.com:registration")
    /// </summary>
    public string Id { get; set; } = "";

    public required string OtpCode { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime SentAt { get; set; }
    public int Attempts { get; set; }
    public bool IsVerified { get; set; }
    public string? VerificationToken { get; set; }
}
