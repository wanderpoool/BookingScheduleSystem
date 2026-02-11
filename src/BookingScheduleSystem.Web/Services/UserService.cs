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
}
