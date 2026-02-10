using System.Text.Json;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;

namespace BookingScheduleSystem.Api.Infrastructure.Schedules;

/// <summary>
/// Calculates provider availability by subtracting existing schedules from working hours.
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>
    /// Generates available/unavailable time blocks for a single day by subtracting
    /// existing schedules from the working hours window.
    /// Optionally enriches schedule blocks with booking details.
    /// </summary>
    public static List<TimeBlock> CalculateAvailability(
        TimeOnly workingStart,
        TimeOnly workingEnd,
        DateOnly date,
        IReadOnlyList<Schedule> schedulesOnDay,
        IReadOnlyDictionary<ScheduleId, List<Booking>>? bookingsBySchedule = null,
        IReadOnlyDictionary<UserId, string>? userNamesById = null)
    {
        var blocks = new List<TimeBlock>();
        var dayStart = date.ToDateTime(workingStart);
        var dayEnd = date.ToDateTime(workingEnd);
        var currentTime = dayStart;

        var sorted = schedulesOnDay
            .OrderBy(s => s.StartTime)
            .ToList();

        foreach (var schedule in sorted)
        {
            // Clamp schedule times to the working hours window
            var schedStart = schedule.StartTime < dayStart ? dayStart : schedule.StartTime;
            var schedEnd = schedule.EndTime > dayEnd ? dayEnd : schedule.EndTime;

            // Skip if schedule is entirely outside working hours or already covered
            if (schedEnd <= currentTime || schedStart >= dayEnd)
                continue;

            // Advance start past already-covered time (handles overlapping schedules)
            if (schedStart < currentTime)
                schedStart = currentTime;

            // Available gap before this schedule
            if (schedStart > currentTime)
            {
                blocks.Add(new TimeBlock
                {
                    StartTime = currentTime,
                    EndTime = schedStart,
                    IsAvailable = true
                });
            }

            // Schedule block — available if capacity remains
            var hasCapacity = schedule.CurrentBookings < schedule.MaxCapacity;

            // Look up booking details for this schedule
            Booking? activeBooking = null;
            string? customerName = null;
            if (bookingsBySchedule is not null &&
                bookingsBySchedule.TryGetValue(schedule.Id, out var bookings))
            {
                activeBooking = bookings.FirstOrDefault(b => b.Status != BookingStatus.Cancelled);
                if (activeBooking is not null && userNamesById is not null)
                {
                    userNamesById.TryGetValue(activeBooking.UserId, out customerName);
                }
            }

            blocks.Add(new TimeBlock
            {
                StartTime = schedStart,
                EndTime = schedEnd,
                IsAvailable = hasCapacity,
                ScheduleId = schedule.Id,
                ScheduleTitle = schedule.Title,
                BookingId = activeBooking?.Id,
                BookingStatus = activeBooking?.Status,
                CustomerName = customerName,
                BookingNotes = activeBooking?.Notes
            });

            currentTime = schedEnd;
        }

        // Available gap after the last schedule
        if (currentTime < dayEnd)
        {
            blocks.Add(new TimeBlock
            {
                StartTime = currentTime,
                EndTime = dayEnd,
                IsAvailable = true
            });
        }

        return blocks;
    }

    /// <summary>
    /// Builds per-provider, per-day availability from a list of providers and their schedules.
    /// Optionally enriches time blocks with booking details when bookings and user names are provided.
    /// </summary>
    public static List<ProviderAvailabilityResponse> BuildProviderAvailability(
        IReadOnlyList<User> providers,
        IReadOnlyList<Schedule> allSchedules,
        string? tenantOperatingHoursJson,
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<Booking>? allBookings = null,
        IReadOnlyDictionary<UserId, string>? userNamesById = null)
    {
        var result = new List<ProviderAvailabilityResponse>();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // Group schedules by provider for efficient lookup
        var schedulesByProvider = allSchedules
            .Where(s => s.ProviderId.HasValue)
            .GroupBy(s => s.ProviderId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group bookings by schedule for efficient lookup
        var bookingsBySchedule = allBookings?
            .GroupBy(b => b.ScheduleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var provider in providers)
        {
            // Use provider working hours, fall back to tenant operating hours
            var workingHoursJson = provider.WorkingHours ?? tenantOperatingHoursJson;
            var workingHours = ParseWorkingHours(workingHoursJson);
            if (workingHours is null || workingHours.Count == 0)
                continue;

            var days = new List<DayAvailability>();

            if (!schedulesByProvider.TryGetValue(provider.Id, out var providerSchedules))
                providerSchedules = new List<Schedule>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayName = date.DayOfWeek.ToString();
                if (!workingHours.TryGetValue(dayName, out var daySchedule) || !daySchedule.IsOpen)
                    continue;

                if (!TimeOnly.TryParse(daySchedule.OpenTime, out var openTime) ||
                    !TimeOnly.TryParse(daySchedule.CloseTime, out var closeTime))
                    continue;

                // For today, clip past time so past slots aren't shown as available
                var effectiveStart = openTime;
                if (date == today && TimeOnly.FromDateTime(now) > openTime)
                    effectiveStart = TimeOnly.FromDateTime(now);

                // Skip if working hours have already passed for today
                if (effectiveStart >= closeTime)
                    continue;

                // Find schedules overlapping this day
                var dayDateTime = date.ToDateTime(TimeOnly.MinValue);
                var nextDayDateTime = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
                var schedulesOnDay = providerSchedules
                    .Where(s => s.StartTime < nextDayDateTime && s.EndTime > dayDateTime)
                    .ToList();

                var timeBlocks = CalculateAvailability(
                    effectiveStart, closeTime, date, schedulesOnDay,
                    bookingsBySchedule, userNamesById);

                days.Add(new DayAvailability
                {
                    Date = date,
                    WorkingHoursStart = daySchedule.OpenTime!,
                    WorkingHoursEnd = daySchedule.CloseTime!,
                    TimeBlocks = timeBlocks
                });
            }

            result.Add(new ProviderAvailabilityResponse
            {
                ProviderId = provider.Id,
                ProviderName = $"{provider.FirstName} {provider.LastName}",
                Days = days
            });
        }

        return result;
    }

    /// <summary>
    /// Parses a working hours JSON string into a day-of-week keyed dictionary.
    /// </summary>
    public static Dictionary<string, OperatingHoursValidator.DaySchedule>? ParseWorkingHours(string? workingHoursJson)
    {
        if (string.IsNullOrWhiteSpace(workingHoursJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, OperatingHoursValidator.DaySchedule>>(
                workingHoursJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
