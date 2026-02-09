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
                return null;

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse != null)
            {
                await _localStorage.SetItemAsync(TokenKey, authResponse.Token);
                await _localStorage.SetItemAsync(UserKey, authResponse);

                // Set authorization header for future requests
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

                // Notify authentication state changed
                if (_authStateProvider is CustomAuthenticationStateProvider customProvider)
                {
                    customProvider.NotifyAuthenticationStateChanged();
                }
            }

            return authResponse;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthenticationResponse?> RegisterAsync(RegisterUserRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (!response.IsSuccessStatusCode)
                return null;

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse != null)
            {
                await _localStorage.SetItemAsync(TokenKey, authResponse.Token);
                await _localStorage.SetItemAsync(UserKey, authResponse);

                // Set authorization header for future requests
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);

                // Notify authentication state changed
                if (_authStateProvider is CustomAuthenticationStateProvider customProvider)
                {
                    customProvider.NotifyAuthenticationStateChanged();
                }
            }

            return authResponse;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TenantResponse?> CreateOrganizationAsync(CreateOrganizationRequest request)
    {
        try
        {
            // Ensure we have a valid token
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                return null;

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsJsonAsync("/api/organizations/create", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Organization creation failed: {response.StatusCode} - {errorContent}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TenantResponse>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception creating organization: {ex.Message}");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        _httpClient.DefaultRequestHeaders.Authorization = null;

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
}
