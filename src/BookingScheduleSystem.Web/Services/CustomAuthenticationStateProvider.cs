using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using BookingScheduleSystem.Contracts.Auth;

namespace BookingScheduleSystem.Web.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;

    public CustomAuthenticationStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");

            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var user = await _localStorage.GetItemAsync<AuthenticationResponse>("currentUser");

            if (user == null)
            {
                // Token exists but user data couldn't be retrieved (token might be expired)
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("currentUser");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            // Create claims from user data
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.Value.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.GivenName, user.FirstName),
                new Claim(ClaimTypes.Surname, user.LastName)
            };

            // Add role claim based on IsGlobalAdmin
            if (user.IsGlobalAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "GlobalAdmin"));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, "User"));
            }

            // Add tenant ID claim if available
            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim("TenantId", user.TenantId.Value.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            return new AuthenticationState(claimsPrincipal);
        }
        catch
        {
            // If anything fails, return unauthenticated state
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
