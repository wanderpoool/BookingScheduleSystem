namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Service for hashing and verifying passwords
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
