using System.Net;
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
                    Message = "We received an unexpected response. Please try again.",
                    IsVerified = false
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to send OTP: {StatusCode} - {Error}", response.StatusCode, errorContent);

            return new OtpResponse
            {
                Success = false,
                Message = ApiErrorHelper.ExtractMessage(errorContent) ?? GetSendOtpError(response.StatusCode),
                IsVerified = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending OTP");
            return new OtpResponse
            {
                Success = false,
                Message = "Unable to connect to the server. Please check your connection and try again.",
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
                    Message = "We received an unexpected response. Please try again.",
                    IsVerified = false
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to verify OTP: {StatusCode} - {Error}", response.StatusCode, errorContent);

            return new OtpResponse
            {
                Success = false,
                Message = ApiErrorHelper.ExtractMessage(errorContent) ?? GetVerifyOtpError(response.StatusCode),
                IsVerified = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OTP");
            return new OtpResponse
            {
                Success = false,
                Message = "Unable to connect to the server. Please check your connection and try again.",
                IsVerified = false
            };
        }
    }

    private static string GetSendOtpError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "We couldn't find an account with that information. Please check and try again.",
        HttpStatusCode.BadRequest => "Please check your email or phone number and try again.",
        HttpStatusCode.TooManyRequests => "Too many attempts. Please wait a moment before requesting a new code.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please refresh the page and try again.",
        _ => "We couldn't send your verification code right now. Please try again in a moment."
    };

    private static string GetVerifyOtpError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This verification code has expired. Please request a new one.",
        HttpStatusCode.BadRequest => "The code you entered is invalid. Please check and try again.",
        HttpStatusCode.TooManyRequests => "Too many attempts. Please wait a moment before trying again.",
        HttpStatusCode.Gone => "This verification code has expired. Please request a new one.",
        HttpStatusCode.Unauthorized => "The code you entered is incorrect or has expired. Please request a new one.",
        _ => "We couldn't verify your code right now. Please try again in a moment."
    };
}
