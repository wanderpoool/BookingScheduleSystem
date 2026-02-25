using System.Text.Json;
using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Web.Services.Chatbot;

public sealed class ChatbotToolExecutor
{
    private readonly IScheduleService _scheduleService;
    private readonly IOtpService _otpService;
    private readonly IAuthenticationService _authService;
    private readonly IBookingService _bookingService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatbotToolExecutor> _logger;

    // Session state for the OTP/registration flow
    private string? _contactMethod;
    private string? _contactEmail;
    private string? _contactPhone;
    private string? _verificationToken;
    private int _otpSendCount;
    private bool _isAuthenticated;

    public ChatbotToolExecutor(
        IScheduleService scheduleService,
        IOtpService otpService,
        IAuthenticationService authService,
        IBookingService bookingService,
        HttpClient httpClient,
        ILogger<ChatbotToolExecutor> logger)
    {
        _scheduleService = scheduleService;
        _otpService = otpService;
        _authService = authService;
        _bookingService = bookingService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement input, Guid tenantId)
    {
        try
        {
            return toolName switch
            {
                "check_availability" => await CheckAvailabilityAsync(input, tenantId),
                "send_otp" => await SendOtpAsync(input),
                "verify_otp" => await VerifyOtpAsync(input),
                "register_user" => await RegisterUserAsync(input, tenantId),
                "create_booking" => await CreateBookingAsync(input),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing chatbot tool {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = $"Tool execution failed: {ex.Message}" });
        }
    }

    private async Task<string> CheckAvailabilityAsync(JsonElement input, Guid tenantId)
    {
        var startDateStr = input.TryGetProperty("start_date", out var sd) ? sd.GetString() : null;
        var endDateStr = input.TryGetProperty("end_date", out var ed) ? ed.GetString() : null;

        var startDate = !string.IsNullOrEmpty(startDateStr) && DateTime.TryParse(startDateStr, out var s)
            ? s
            : DateTime.UtcNow.Date;

        var endDate = !string.IsNullOrEmpty(endDateStr) && DateTime.TryParse(endDateStr, out var e)
            ? e
            : startDate.AddDays(7);

        var result = await _scheduleService.ListPublicSchedulesAsync(tenantId, startDate, endDate);

        if (result == null || result.Providers.Count == 0)
        {
            return JsonSerializer.Serialize(new { message = "No available slots found for the requested dates.", providers = Array.Empty<object>() });
        }

        // Format for Claude — include schedule IDs so it can reference them
        var providers = result.Providers.Select(p => new
        {
            provider_name = p.ProviderName,
            days = p.Days.Select(d => new
            {
                date = d.Date.ToString("yyyy-MM-dd"),
                day_of_week = d.Date.DayOfWeek.ToString(),
                working_hours = $"{d.WorkingHoursStart} - {d.WorkingHoursEnd}",
                available_slots = d.TimeBlocks
                    .Where(tb => tb.IsAvailable && tb.ScheduleId.HasValue)
                    .Select(tb => new
                    {
                        schedule_id = tb.ScheduleId!.Value.Value.ToString(),
                        start_time = tb.StartTime.ToString("HH:mm"),
                        end_time = tb.EndTime.ToString("HH:mm"),
                        title = tb.ScheduleTitle
                    }).ToList(),
                booked_count = d.TimeBlocks.Count(tb => !tb.IsAvailable)
            }).ToList()
        }).ToList();

        return JsonSerializer.Serialize(new { providers, total_count = result.TotalCount });
    }

    private async Task<string> SendOtpAsync(JsonElement input)
    {
        if (_otpSendCount >= 3)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Maximum OTP attempts reached for this session. Please try again later." });
        }

        var contactMethod = input.GetProperty("contact_method").GetString()!;
        var email = input.TryGetProperty("email", out var em) ? em.GetString() : null;
        var phone = input.TryGetProperty("phone_number", out var ph) ? ph.GetString() : null;

        // Store for later use
        _contactMethod = contactMethod;
        _contactEmail = email;
        _contactPhone = phone;

        var request = new SendOtpRequest
        {
            ContactMethod = contactMethod,
            Email = email,
            PhoneNumber = phone,
            Purpose = "registration"
        };

        var result = await _otpService.SendOtpAsync(request);
        _otpSendCount++;

        return JsonSerializer.Serialize(new
        {
            success = result.Success,
            message = result.Message ?? (result.Success ? "OTP sent successfully. Ask the user to check their inbox and enter the 6-digit code." : "Failed to send OTP."),
            expires_at = result.ExpiresAt?.ToString("o")
        });
    }

    private async Task<string> VerifyOtpAsync(JsonElement input)
    {
        if (string.IsNullOrEmpty(_contactMethod))
        {
            return JsonSerializer.Serialize(new { success = false, error = "No OTP has been sent yet. Call send_otp first." });
        }

        var otpCode = input.GetProperty("otp_code").GetString()!;

        var request = new VerifyOtpRequest
        {
            ContactMethod = _contactMethod,
            Email = _contactEmail,
            PhoneNumber = _contactPhone,
            OtpCode = otpCode,
            Purpose = "registration"
        };

        var result = await _otpService.VerifyOtpAsync(request);

        if (result.IsVerified && !string.IsNullOrEmpty(result.VerificationToken))
        {
            _verificationToken = result.VerificationToken;
        }

        return JsonSerializer.Serialize(new
        {
            success = result.IsVerified,
            message = result.IsVerified
                ? "OTP verified. Now collect the user's first and last name to complete registration."
                : result.Message ?? "Invalid OTP code. Ask the user to try again."
        });
    }

    private async Task<string> RegisterUserAsync(JsonElement input, Guid tenantId)
    {
        if (string.IsNullOrEmpty(_verificationToken))
        {
            return JsonSerializer.Serialize(new { success = false, error = "OTP not verified yet. Complete OTP verification first." });
        }

        var firstName = input.GetProperty("first_name").GetString()!;
        var lastName = input.GetProperty("last_name").GetString()!;

        // Auto-generate a temporary password
        var tempPassword = $"Temp{Guid.NewGuid():N}!";

        var request = new RegisterUserRequest
        {
            Email = _contactEmail,
            PhoneNumber = _contactPhone,
            Password = tempPassword,
            FirstName = firstName,
            LastName = lastName,
            TenantId = new TenantId(tenantId),
            OtpVerificationToken = _verificationToken
        };

        var authResponse = await _authService.RegisterAsync(request);

        if (authResponse == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Registration failed. The user may already have an account — suggest they sign in instead." });
        }

        // Set auth headers for subsequent booking calls
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        _isAuthenticated = true;

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"User {firstName} {lastName} registered successfully. They are now authenticated and ready to book.",
            user_name = $"{firstName} {lastName}"
        });
    }

    private async Task<string> CreateBookingAsync(JsonElement input)
    {
        if (!_isAuthenticated)
        {
            return JsonSerializer.Serialize(new { success = false, error = "User is not authenticated. Complete registration first." });
        }

        var scheduleIdStr = input.GetProperty("schedule_id").GetString()!;
        var notes = input.TryGetProperty("notes", out var n) ? n.GetString() : null;

        if (!Guid.TryParse(scheduleIdStr, out var scheduleGuid))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid schedule_id format." });
        }

        var request = new CreateBookingRequest
        {
            ScheduleId = scheduleGuid,
            Notes = notes
        };

        var booking = await _bookingService.CreateBookingAsync(request);

        if (booking == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Failed to create booking. The slot may no longer be available." });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            booking_id = booking.Id.Value.ToString(),
            schedule_title = booking.ScheduleTitle,
            start_time = booking.ScheduleStartTime?.ToString("dddd, MMMM d 'at' h:mm tt"),
            end_time = booking.ScheduleEndTime?.ToString("h:mm tt"),
            message = "Booking confirmed! Share the details with the user."
        });
    }
}
