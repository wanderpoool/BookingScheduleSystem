using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using BookingScheduleSystem.Contracts.Auth;

namespace BookingScheduleSystem.Web.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private AuthenticationState? _cachedState;

    public CustomAuthenticationStateProvider(ILocalStorageService localStorage, IJSRuntime jsRuntime)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // During prerender, JS interop is not available.
        // Return cached state if we have one, otherwise return a "pending" anonymous state
        // that won't trigger redirects (pages should handle prerender gracefully).
        if (_jsRuntime is not IJSInProcessRuntime && !IsInteractiveRendering())
        {
            return _cachedState ?? new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");

            if (string.IsNullOrWhiteSpace(token))
            {
                _cachedState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                return _cachedState;
            }

            var user = await _localStorage.GetItemAsync<AuthenticationResponse>("currentUser");

            if (user == null)
            {
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("currentUser");
                _cachedState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                return _cachedState;
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.Value.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.GivenName, user.FirstName),
                new Claim(ClaimTypes.Surname, user.LastName)
            };

            if (user.IsGlobalAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "GlobalAdmin"));
            }
            else if (user.IsProvider)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Provider"));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, "Customer"));
            }

            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim("TenantId", user.TenantId.Value.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _cachedState = new AuthenticationState(claimsPrincipal);
            return _cachedState;
        }
        catch (InvalidOperationException)
        {
            // JS interop called during prerender — return cached or anonymous
            return _cachedState ?? new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        catch (JSDisconnectedException)
        {
            return _cachedState ?? new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private bool IsInteractiveRendering()
    {
        // RemoteJSRuntime (used in Blazor Server interactive mode) is not IJSInProcessRuntime
        // but it IS ready for async calls. The prerender runtime throws InvalidOperationException.
        // We detect prerender by checking the runtime type name.
        var typeName = _jsRuntime.GetType().Name;
        return typeName != "UnsupportedJavaScriptRuntime";
    }
}
