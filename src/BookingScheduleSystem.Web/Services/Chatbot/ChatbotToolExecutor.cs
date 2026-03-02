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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatbotToolExecutor> _logger;

    // Isolated HttpClient for chatbot auth calls — never shares headers with the main app
    private HttpClient? _chatbotHttpClient;

    // Session state for the OTP/registration flow
    private string? _contactMethod;
    private string? _contactEmail;
    private string? _contactPhone;
    private string? _verificationToken;
    private string? _authenticatedUserName;
    private int _otpSendCount;
    private bool _isAuthenticated;

    public ChatbotToolExecutor(
        IScheduleService scheduleService,
        IOtpService otpService,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatbotToolExecutor> logger)
    {
        _scheduleService = scheduleService;
        _otpService = otpService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the isolated HttpClient for chatbot-specific authenticated API calls.
    /// This is separate from the shared HttpClient to avoid leaking customer auth
    /// headers into the provider's session.
    /// </summary>
    private HttpClient GetAuthenticatedClient()
    {
        if (_chatbotHttpClient is null)
        {
            _chatbotHttpClient = _httpClientFactory.CreateClient("BookingScheduleAPI");
        }
        return _chatbotHttpClient;
    }

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement input, Guid tenantId)
    {
        try
        {
            return toolName switch
            {
                "check_availability" => await CheckAvailabilityAsync(input, tenantId),
                "send_otp" => await SendOtpAsync(input),
                "verify_otp" => await VerifyOtpAsync(input, tenantId),
                "register_user" => await RegisterUserAsync(input, tenantId),
                "create_booking" => await CreateBookingAsync(input),
                "create_and_book" => await CreateAndBookAsync(input),
                "list_my_bookings" => await ListMyBookingsAsync(input),
                "cancel_booking" => await CancelBookingChatbotAsync(input),
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
        var client = GetAuthenticatedClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        if (tenantId.HasValue)
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.Value.ToString());
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

    private async Task<string> VerifyOtpAsync(JsonElement input, Guid tenantId)
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

            // Set tenant header so the API can perform lazy enrollment
            var client = GetAuthenticatedClient();
            client.DefaultRequestHeaders.Remove("X-Tenant-Id");
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login-otp", loginRequest);

            if (loginResponse.IsSuccessStatusCode)
            {
                var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
                if (authResponse != null)
                {
                    SetAuthHeaders(authResponse.Token, authResponse.TenantId?.Value);
                    _isAuthenticated = true;
                    _authenticatedUserName = $"{authResponse.FirstName} {authResponse.LastName}";

                    _logger.LogInformation("Chatbot: existing user logged in via OTP — {Name}",
                        _authenticatedUserName);

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        existing_user = true,
                        user_name = $"{authResponse.FirstName} {authResponse.LastName}",
                        message = $"Welcome back, {authResponse.FirstName}! You're signed in and ready to book."
                    });
                }
            }
            else if (loginResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Chatbot: OTP login returned 403 — user cannot access this tenant");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "This account cannot be used to book at this business."
                });
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
        var response = await GetAuthenticatedClient().PostAsJsonAsync("/api/auth/register", request);

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
        _authenticatedUserName = $"{firstName} {lastName}";

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"User {firstName} {lastName} registered successfully. They are now authenticated and ready to book.",
            user_name = _authenticatedUserName
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

        var client = GetAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/bookings", request);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Chatbot booking conflict for schedule {ScheduleId}", scheduleGuid);
            return JsonSerializer.Serialize(new
            {
                success = false,
                conflict = true,
                error = "This time slot is no longer available — it may have just been booked by someone else. Please check availability again and choose a different time."
            });
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Chatbot booking failed for schedule {ScheduleId}: {StatusCode} {Error}", scheduleGuid, response.StatusCode, error);
            return JsonSerializer.Serialize(new { success = false, error = $"Booking failed: {response.StatusCode}" });
        }

        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        if (booking == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Failed to create booking. The slot may no longer be available." });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            booking_id = booking.Id.Value.ToString(),
            reference_number = booking.ReferenceNumber,
            schedule_title = booking.ScheduleTitle,
            start_time = booking.ScheduleStartTime?.ToString("dddd, MMMM d 'at' h:mm tt"),
            end_time = booking.ScheduleEndTime?.ToString("h:mm tt"),
            message = "Booking confirmed! Share the details with the user including the reference number."
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

        var client = GetAuthenticatedClient();

        // Step 1: Create schedule slot
        var scheduleRequest = new CreateScheduleRequest
        {
            ProviderId = providerGuid,
            Title = $"Appointment for {_authenticatedUserName ?? "Customer"}",
            StartTime = startDateTime,
            EndTime = endDateTime,
            MaxCapacity = 1
        };

        var scheduleResponse = await client.PostAsJsonAsync("/api/schedules", scheduleRequest);

        if (scheduleResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Chatbot schedule conflict: provider {ProviderId} at {Date} {Start}-{End}",
                providerGuid, dateStr, startTimeStr, endTimeStr);
            return JsonSerializer.Serialize(new
            {
                success = false,
                conflict = true,
                error = "This time slot overlaps with an existing appointment for this provider. Please choose a different time."
            });
        }

        if (!scheduleResponse.IsSuccessStatusCode)
        {
            var error = await scheduleResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create schedule slot for chatbot booking: {StatusCode} {Error}",
                scheduleResponse.StatusCode, error);
            return JsonSerializer.Serialize(new { success = false, error = "Could not create the time slot. Please try a different time." });
        }

        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ScheduleResponse>();
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

        var bookingResponse = await client.PostAsJsonAsync("/api/bookings", bookingRequest);

        if (bookingResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Chatbot booking conflict after schedule creation");
            await CleanupScheduleAsync(schedule.Id.Value);
            return JsonSerializer.Serialize(new
            {
                success = false,
                conflict = true,
                error = "Booking failed due to a conflict. Please choose a different time."
            });
        }

        if (!bookingResponse.IsSuccessStatusCode)
        {
            var error = await bookingResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create booking after schedule creation: {StatusCode} {Error}",
                bookingResponse.StatusCode, error);
            await CleanupScheduleAsync(schedule.Id.Value);
            return JsonSerializer.Serialize(new { success = false, error = "Booking failed. Please try again." });
        }

        var booking = await bookingResponse.Content.ReadFromJsonAsync<BookingResponse>();
        if (booking == null)
        {
            await CleanupScheduleAsync(schedule.Id.Value);
            return JsonSerializer.Serialize(new { success = false, error = "Booking failed. Please try again." });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            booking_id = booking.Id.Value.ToString(),
            reference_number = booking.ReferenceNumber,
            schedule_title = booking.ScheduleTitle,
            start_time = booking.ScheduleStartTime?.ToString("dddd, MMMM d 'at' h:mm tt"),
            end_time = booking.ScheduleEndTime?.ToString("h:mm tt"),
            message = "Booking confirmed! Share the details with the user including the reference number."
        });
    }

    private async Task<string> ListMyBookingsAsync(JsonElement input)
    {
        if (!_isAuthenticated)
        {
            return JsonSerializer.Serialize(new { success = false, error = "User is not authenticated. Complete OTP verification or registration first." });
        }

        var statusFilter = input.TryGetProperty("status_filter", out var sf) ? sf.GetString() : "all";

        var client = GetAuthenticatedClient();

        var url = "/api/bookings";
        if (statusFilter != null && statusFilter != "all")
        {
            url += $"?status={statusFilter}";
        }

        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Chatbot list bookings failed: {StatusCode} {Error}", response.StatusCode, error);
            return JsonSerializer.Serialize(new { success = false, error = "Could not retrieve bookings." });
        }

        var result = await response.Content.ReadFromJsonAsync<ListBookingsResponse>();
        if (result == null || result.Bookings.Count == 0)
        {
            return JsonSerializer.Serialize(new { success = true, message = "No bookings found.", bookings = Array.Empty<object>() });
        }

        var pht = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        var now = pht;

        var bookings = result.Bookings.Select(b => new
        {
            booking_id = b.Id.Value.ToString(),
            reference_number = b.ReferenceNumber,
            title = b.ScheduleTitle ?? "Appointment",
            date = b.ScheduleStartTime?.ToString("yyyy-MM-dd"),
            start_time = b.ScheduleStartTime?.ToString("h:mm tt"),
            end_time = b.ScheduleEndTime?.ToString("h:mm tt"),
            status = b.Status.ToString().ToLowerInvariant(),
            is_future = b.ScheduleStartTime.HasValue && b.ScheduleStartTime.Value > now,
            booked_at = b.BookedAt.ToString("yyyy-MM-dd"),
            notes = b.Notes
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            success = true,
            total_count = result.TotalCount,
            bookings
        });
    }

    private async Task<string> CancelBookingChatbotAsync(JsonElement input)
    {
        if (!_isAuthenticated)
        {
            return JsonSerializer.Serialize(new { success = false, error = "User is not authenticated. Complete OTP verification or registration first." });
        }

        var bookingIdStr = input.GetProperty("booking_id").GetString()!;
        var reason = input.TryGetProperty("reason", out var r) ? r.GetString() : null;

        if (!Guid.TryParse(bookingIdStr, out var bookingGuid))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid booking_id format." });
        }

        var client = GetAuthenticatedClient();
        // API expects nested { cancellation: { cancellationReason: "..." } }
        var cancelPayload = new { cancellation = new CancelBookingRequest { CancellationReason = reason } };
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingGuid}/cancel", cancelPayload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Chatbot cancel booking failed for {BookingId}: {StatusCode} {Error}",
                bookingGuid, response.StatusCode, error);
            return JsonSerializer.Serialize(new { success = false, error = $"Could not cancel booking: {response.StatusCode}" });
        }

        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>();

        return JsonSerializer.Serialize(new
        {
            success = true,
            booking_id = bookingGuid.ToString(),
            reference_number = booking?.ReferenceNumber,
            title = booking?.ScheduleTitle ?? "Appointment",
            message = "Booking has been cancelled successfully."
        });
    }

    private async Task CleanupScheduleAsync(Guid scheduleId)
    {
        try
        {
            var response = await GetAuthenticatedClient().DeleteAsync($"/api/schedules/{scheduleId}");
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Cleaned up orphaned schedule {ScheduleId}", scheduleId);
            else
                _logger.LogWarning("Cleanup of orphaned schedule {ScheduleId} returned {StatusCode}", scheduleId, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup orphaned schedule {ScheduleId}", scheduleId);
        }
    }
}
