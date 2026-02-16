using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Users;

namespace BookingScheduleSystem.Web.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;

    public UserService(HttpClient httpClient, ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ListUsersResponse?> ListUsersAsync(bool? isProvider = null, bool? isActive = null, int page = 1, int pageSize = 20)
    {
        try
        {
            var url = $"/api/users?pageNumber={page}&pageSize={pageSize}";
            if (isProvider.HasValue)
                url += $"&isProvider={isProvider.Value}";
            if (isActive.HasValue)
                url += $"&isActive={isActive.Value}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ListUsersResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to list users: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't load the user list.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            throw new HttpRequestException("Unable to connect to the server.", ex);
        }
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/users/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get user {UserId}: {StatusCode} {Body}", userId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't load the user profile.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            throw new HttpRequestException("Unable to connect to the server.", ex);
        }
    }

    public async Task<UserResponse?> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/users/{userId}", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update user {UserId}: {StatusCode} {Body}", userId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't update the profile.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            throw new HttpRequestException("Unable to update the profile. Please try again.", ex);
        }
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/users/{userId}/deactivate", null);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't deactivate this user.";
                throw new HttpRequestException(message, null, response.StatusCode);
            }
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", userId);
            throw new HttpRequestException("Unable to deactivate the user. Please try again.", ex);
        }
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/users/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't delete this user.";
                throw new HttpRequestException(message, null, response.StatusCode);
            }
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            throw new HttpRequestException("Unable to delete the user. Please try again.", ex);
        }
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/users/{userId}/reset-password", new { NewPassword = newPassword });
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't reset the password.";
                throw new HttpRequestException(message, null, response.StatusCode);
            }
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            throw new HttpRequestException("Unable to reset the password. Please try again.", ex);
        }
    }

    public async Task<UserResponse?> AddProviderAsync(string? email, string? phoneNumber, string firstName, string lastName, string password)
    {
        try
        {
            var payload = new { Email = email, PhoneNumber = phoneNumber, FirstName = firstName, LastName = lastName, Password = password };
            var response = await _httpClient.PostAsJsonAsync("/api/users/add-provider", payload);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to add provider: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't add the provider.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding provider");
            throw new HttpRequestException("Unable to add the provider. Please try again.", ex);
        }
    }
}
