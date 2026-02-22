using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public sealed class SemaphoreSmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreOptions _options;
    private readonly ILogger<SemaphoreSmsService> _logger;
    private readonly bool _isDevelopment;

    public SemaphoreSmsService(
        HttpClient httpClient,
        IOptions<SemaphoreOptions> options,
        ILogger<SemaphoreSmsService> logger,
        IHostEnvironment environment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        if (_isDevelopment)
        {
            _logger.LogWarning("=== SMS (DEV MODE) ===");
            _logger.LogWarning("To: {PhoneNumber}", phoneNumber);
            _logger.LogWarning("Message: {Message}", message);
            _logger.LogWarning("======================");
            return true;
        }

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogError("Semaphore API key is not configured. SMS not sent to {PhoneNumber}", MaskPhone(phoneNumber));
            return false;
        }

        try
        {
            var payload = new Dictionary<string, string>
            {
                ["apikey"] = _options.ApiKey,
                ["number"] = phoneNumber,
                ["message"] = message,
                ["sendername"] = _options.SenderName
            };

            using var content = new FormUrlEncodedContent(payload);
            var response = await _httpClient.PostAsync($"{_options.BaseUrl}/messages", content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent via Semaphore to {PhoneNumber}", MaskPhone(phoneNumber));
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Semaphore API returned {StatusCode} for {PhoneNumber}: {Response}",
                (int)response.StatusCode, MaskPhone(phoneNumber), responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS via Semaphore to {PhoneNumber}", MaskPhone(phoneNumber));
            return false;
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 4) return "***";
        return new string('*', phone.Length - 4) + phone[^4..];
    }
}
