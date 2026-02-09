using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Auth;

namespace BookingScheduleSystem.Web.Services;

public class OtpService : IOtpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OtpService> _logger;

    public OtpService(HttpClient httpClient, ILogger<OtpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OtpResponse> SendOtpAsync(SendOtpRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/send-otp", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OtpResponse>();
                return result ?? new OtpResponse
                {
                    Success = false,
                    Message = "Failed to parse response",
                    IsVerified = false
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to send OTP: {StatusCode} - {Error}", response.StatusCode, errorContent);

            return new OtpResponse
            {
                Success = false,
                Message = $"Failed to send OTP: {response.StatusCode}",
                IsVerified = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending OTP");
            return new OtpResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                IsVerified = false
            };
        }
    }

    public async Task<OtpResponse> VerifyOtpAsync(VerifyOtpRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/verify-otp", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OtpResponse>();
                return result ?? new OtpResponse
                {
                    Success = false,
                    Message = "Failed to parse response",
                    IsVerified = false
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to verify OTP: {StatusCode} - {Error}", response.StatusCode, errorContent);

            return new OtpResponse
            {
                Success = false,
                Message = $"Invalid or expired OTP code",
                IsVerified = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OTP");
            return new OtpResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                IsVerified = false
            };
        }
    }
}
