using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Tenants;

namespace BookingScheduleSystem.Web.Services;

/// <summary>
/// Service for handling authentication operations
/// </summary>
public interface IAuthenticationService
{
    Task<AuthenticationResponse?> LoginAsync(LoginRequest request);
    Task<AuthenticationResponse?> LoginWithOtpAsync(LoginWithOtpRequest request);
    Task<AuthenticationResponse?> RegisterAsync(RegisterUserRequest request);
    Task<TenantResponse?> CreateOrganizationAsync(CreateOrganizationRequest request);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<AuthenticationResponse?> GetCurrentUserAsync();
}
