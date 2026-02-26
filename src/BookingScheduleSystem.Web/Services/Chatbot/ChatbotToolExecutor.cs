using System.Net.Http.Json;
using System.Text.Json;
using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;

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
                "create_and_book" => await CreateAndBookAsync(input),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing chatbot tool {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = $"Tool execution failed: {ex.Message}" });
        }
    }

    private void SetAuthHeaders(string token, Guid? tenantId)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        if (tenantId.HasValue)
            _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.Value.ToString());
    }

    private async Task<string> CheckAvailabilityAsync(JsonElement input, Guid tenantId)
    {
        var startDateStr = input.TryGetProperty("start_date", out var sd) ? sd.GetString() : null;
        var endDateStr = input.TryGetProperty("end_date", out var ed) ? ed.GetString() : null;

        // Use Philippine time (UTC+8) for date defaults since the app serves PH users
        var pht = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        var today = pht.Date;

        var startDate = !string.IsNullOrEmpty(startDateStr) && DateTime.TryParse(startDateStr, out var s)
            ? s
            : today;

        var endDate = !string.IsNullOrEmpty(endDateStr) && DateTime.TryParse(endDateStr, out var e)
            ? e
            : startDate.AddDays(7);

        _logger.LogInformation(
            "Chatbot checking availability for tenant {TenantId}: {StartDate} to {EndDate}",
            tenantId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

        var result = await _scheduleService.ListPublicSchedulesAsync(tenantId, startDate, endDate);

        if (result == null || result.Providers.Count == 0)
        {
            return JsonSerializer.Serialize(new { message = "No providers found for this business.", providers = Array.Empty<object>() });
        }

        // Format for Claude — include both bookable slots (with ScheduleId) and free time windows
        var providers = result.Providers.Select(p => new
        {
            provider_id = p.ProviderId.Value.ToString(),
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
                free_time = d.TimeBlocks
                    .Where(tb => tb.IsAvailable && !tb.ScheduleId.HasValue)
                    .Select(tb => new
                    {
                        start_time = tb.StartTime.ToString("HH:mm"),
                        end_time = tb.EndTime.ToString("HH:mm")
                    }).ToList(),
                booked_count = d.TimeBlocks.Count(tb => !tb.IsAvailable)
            }).ToList()
        }).ToList();

        _logger.LogInformation(
            "Chatbot availability result: {ProviderCount} providers, days with bookable slots: {SlotDays}, days with free time: {FreeDays}",
            providers.Count,
            providers.Sum(p => p.days.Count(d => d.available_slots.Count > 0)),
            providers.Sum(p => p.days.Count(d => d.free_time.Count > 0)));

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

        if (!result.IsVerified || string.IsNullOrEmpty(result.VerificationToken))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = result.Message ?? "Invalid OTP code. Ask the user to try again."
            });
        }

        _verificationToken = result.VerificationToken;

        // Try logging in directly (bypass AuthenticationService to avoid JS interop/localStorage)
        try
        {
            var loginRequest = new LoginWithOtpRequest
            {
                ContactMethod = _contactMethod,
                Email = _contactEmail,
                PhoneNumber = _contactPhone,
                OtpVerificationToken = _verificationToken
            };

            var loginResponse = await _httpClient.PostAsJsonAsync("/api/auth/login-otp", loginRequest);

            if (loginResponse.IsSuccessStatusCode)
            {
                var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
                if (authResponse != null)
                {
                    SetAuthHeaders(authResponse.Token, authResponse.TenantId?.Value);
                    _isAuthenticated = true;

                    _logger.LogInformation("Chatbot: existing user logged in via OTP — {Name}",
                        $"{authResponse.FirstName} {authResponse.LastName}");

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        existing_user = true,
                        user_name = $"{authResponse.FirstName} {authResponse.LastName}",
                        message = $"Welcome back, {authResponse.FirstName}! You're signed in and ready to book."
                    });
                }
            }
            else if (loginResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Chatbot: OTP login attempt returned {StatusCode}", loginResponse.StatusCode);
            }
            else
            {
                _logger.LogInformation("Chatbot: OTP verified but user not found, registration needed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chatbot: OTP login attempt failed");
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            existing_user = false,
            message = "OTP verified. This is a new user — collect their first and last name to complete registration."
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

        // Call API directly (bypass AuthenticationService to avoid JS interop/localStorage)
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Chatbot registration failed: {StatusCode} {Body}", response.StatusCode, errorBody);
            return JsonSerializer.Serialize(new { success = false, error = "Registration failed. The user may already have an account — suggest they sign in instead." });
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        if (authResponse == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Registration failed unexpectedly. Please try again." });
        }

        SetAuthHeaders(authResponse.Token, tenantId);
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

    private async Task<string> CreateAndBookAsync(JsonElement input)
    {
        if (!_isAuthenticated)
        {
            return JsonSerializer.Serialize(new { success = false, error = "User is not authenticated. Complete registration first." });
        }

        var providerIdStr = input.GetProperty("provider_id").GetString()!;
        var dateStr = input.GetProperty("date").GetString()!;
        var startTimeStr = input.GetProperty("start_time").GetString()!;
        var endTimeStr = input.GetProperty("end_time").GetString()!;
        var notes = input.TryGetProperty("notes", out var n) ? n.GetString() : null;

        if (!Guid.TryParse(providerIdStr, out var providerGuid))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid provider_id format." });
        }

        if (!DateOnly.TryParse(dateStr, out var date))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid date format. Use YYYY-MM-DD." });
        }

        if (!TimeOnly.TryParse(startTimeStr, out var startTime) ||
            !TimeOnly.TryParse(endTimeStr, out var endTime))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid time format. Use HH:mm (24-hour)." });
        }

        if (endTime <= startTime)
        {
            return JsonSerializer.Serialize(new { success = false, error = "End time must be after start time." });
        }

        var startDateTime = date.ToDateTime(startTime, DateTimeKind.Unspecified);
        var endDateTime = date.ToDateTime(endTime, DateTimeKind.Unspecified);

        _logger.LogInformation(
            "Chatbot creating schedule + booking: provider {ProviderId}, {Date} {Start}-{End}",
            providerGuid, dateStr, startTimeStr, endTimeStr);

        // Step 1: Create schedule slot
        var scheduleRequest = new CreateScheduleRequest
        {
            ProviderId = providerGuid,
            Title = $"Appointment on {date:MMM d} at {startTime:h:mm tt}",
            StartTime = startDateTime,
            EndTime = endDateTime,
            MaxCapacity = 1
        };

        ScheduleResponse? schedule;
        try
        {
            schedule = await _scheduleService.CreateScheduleAsync(scheduleRequest);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to create schedule slot for chatbot booking");
            return JsonSerializer.Serialize(new { success = false, error = $"Could not create the time slot: {ex.Message}" });
        }

        if (schedule == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Failed to create the time slot. Please try a different time." });
        }

        // Step 2: Book into the new schedule
        var bookingRequest = new CreateBookingRequest
        {
            ScheduleId = schedule.Id.Value,
            Notes = notes
        };

        try
        {
            var booking = await _bookingService.CreateBookingAsync(bookingRequest);

            if (booking == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Schedule was created but booking failed. Please try again." });
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
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to create booking after schedule creation");
            return JsonSerializer.Serialize(new { success = false, error = $"Schedule was created but booking failed: {ex.Message}" });
        }
    }
}
