using System.Net;
using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Tenants;
using Microsoft.AspNetCore.Components.Authorization;

namespace BookingScheduleSystem.Web.Services;

/// <summary>
/// Implementation of authentication service
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private const string TokenKey = "authToken";
    private const string UserKey = "currentUser";

    public AuthenticationService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<AuthenticationResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(GetLoginError(response.StatusCode), null, response.StatusCode);

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse != null)
            {
                await _localStorage.SetItemAsync(TokenKey, authResponse.Token);
                await _localStorage.SetItemAsync(UserKey, authResponse);

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

                SetTenantHeader(authResponse);

                if (_authStateProvider is CustomAuthenticationStateProvider customProvider)
                {
                    customProvider.NotifyAuthenticationStateChanged();
                }
            }

            return authResponse;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<AuthenticationResponse?> LoginWithOtpAsync(LoginWithOtpRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login-otp", request);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(GetOtpLoginError(response.StatusCode), null, response.StatusCode);

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse != null)
            {
                await _localStorage.SetItemAsync(TokenKey, authResponse.Token);
                await _localStorage.SetItemAsync(UserKey, authResponse);

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

                SetTenantHeader(authResponse);

                if (_authStateProvider is CustomAuthenticationStateProvider customProvider)
                {
                    customProvider.NotifyAuthenticationStateChanged();
                }
            }

            return authResponse;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<AuthenticationResponse?> RegisterAsync(RegisterUserRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(GetRegisterError(response.StatusCode), null, response.StatusCode);

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse != null)
            {
                await _localStorage.SetItemAsync(TokenKey, authResponse.Token);
                await _localStorage.SetItemAsync(UserKey, authResponse);

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

                SetTenantHeader(authResponse);

                if (_authStateProvider is CustomAuthenticationStateProvider customProvider)
                {
                    customProvider.NotifyAuthenticationStateChanged();
                }
            }

            return authResponse;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<TenantResponse?> CreateOrganizationAsync(CreateOrganizationRequest request)
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new HttpRequestException("Your session has expired. Please sign in again.");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsJsonAsync("/api/organizations/create", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Organization creation failed: {response.StatusCode} - {errorContent}");
                throw new HttpRequestException(GetCreateOrgError(response.StatusCode), null, response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<TenantResponse>();
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        // Notify authentication state changed
        if (_authStateProvider is CustomAuthenticationStateProvider customProvider)
        {
            customProvider.NotifyAuthenticationStateChanged();
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(TokenKey);
    }

    public async Task<AuthenticationResponse?> GetCurrentUserAsync()
    {
        return await _localStorage.GetItemAsync<AuthenticationResponse>(UserKey);
    }

    private void SetTenantHeader(AuthenticationResponse authResponse)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        if (authResponse.TenantId.HasValue)
        {
            _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", authResponse.TenantId.Value.Value.ToString());
        }
    }

    private static string GetLoginError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Invalid email or password. Please check your credentials and try again.",
        HttpStatusCode.NotFound => "No account found with that email. Please check your email or register a new account.",
        HttpStatusCode.Forbidden => "Your account has been suspended. Please contact support.",
        HttpStatusCode.TooManyRequests => "Too many login attempts. Please wait a moment before trying again.",
        _ => "We're having trouble signing you in right now. Please try again in a moment."
    };

    private static string GetOtpLoginError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Your verification code has expired or is invalid. Please request a new one.",
        HttpStatusCode.NotFound => "No account found with that contact information. Please register first.",
        HttpStatusCode.Forbidden => "Your account has been suspended. Please contact support.",
        HttpStatusCode.TooManyRequests => "Too many login attempts. Please wait a moment before trying again.",
        _ => "We're having trouble signing you in right now. Please try again in a moment."
    };

    private static string GetRegisterError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Conflict => "An account with this email already exists. Please sign in or use a different email.",
        HttpStatusCode.BadRequest => "Please check your registration details and try again.",
        HttpStatusCode.TooManyRequests => "Too many registration attempts. Please wait a moment before trying again.",
        _ => "We couldn't create your account right now. Please try again in a moment."
    };

    private static string GetCreateOrgError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Conflict => "An organization with this name already exists. Please choose a different name.",
        HttpStatusCode.BadRequest => "Please check your organization details and try again.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to create an organization.",
        _ => "We couldn't create your organization right now. Please try again in a moment."
    };
}
