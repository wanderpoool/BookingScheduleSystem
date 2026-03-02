using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Service for generating and validating JWT tokens
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(User user);
    string GenerateToken(User user, TenantId activeTenantId);
    DateTime GetTokenExpiration();
}
