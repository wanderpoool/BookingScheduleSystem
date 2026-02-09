using System.Text.Json;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Validates whether a given time falls within organization's operating hours
/// </summary>
public static class OperatingHoursValidator
{
    /// <summary>
    /// Validates if the schedule time range falls within operating hours
    /// </summary>
    /// <param name="operatingHoursJson">JSON string representing operating hours per day of week</param>
    /// <param name="startTime">Schedule start time</param>
    /// <param name="endTime">Schedule end time</param>
    /// <param name="errorMessage">Output error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsWithinOperatingHours(
        string? operatingHoursJson,
        DateTime startTime,
        DateTime endTime,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        // If no operating hours defined, allow all times
        if (string.IsNullOrWhiteSpace(operatingHoursJson))
        {
            return true;
        }

        try
        {
            var operatingHours = JsonSerializer.Deserialize<Dictionary<string, DaySchedule>>(
                operatingHoursJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (operatingHours is null || operatingHours.Count == 0)
            {
                return true;
            }

            // Check start time
            var startDayOfWeek = startTime.DayOfWeek.ToString();
            if (!operatingHours.TryGetValue(startDayOfWeek, out var startDaySchedule))
            {
                errorMessage = $"Organization is closed on {startDayOfWeek}";
                return false;
            }

            if (!startDaySchedule.IsOpen)
            {
                errorMessage = $"Organization is closed on {startDayOfWeek}";
                return false;
            }

            // Check end time
            var endDayOfWeek = endTime.DayOfWeek.ToString();
            if (!operatingHours.TryGetValue(endDayOfWeek, out var endDaySchedule))
            {
                errorMessage = $"Organization is closed on {endDayOfWeek}";
                return false;
            }

            if (!endDaySchedule.IsOpen)
            {
                errorMessage = $"Organization is closed on {endDayOfWeek}";
                return false;
            }

            // Parse and validate times
            var startTimeOnly = TimeOnly.FromDateTime(startTime);
            var endTimeOnly = TimeOnly.FromDateTime(endTime);

            // If schedule spans multiple days, validate each day
            if (startDayOfWeek != endDayOfWeek)
            {
                errorMessage = "Schedule cannot span multiple days";
                return false;
            }

            // Check if times fall within operating hours
            if (!string.IsNullOrEmpty(startDaySchedule.OpenTime) &&
                !string.IsNullOrEmpty(startDaySchedule.CloseTime))
            {
                if (!TimeOnly.TryParse(startDaySchedule.OpenTime, out var openTime) ||
                    !TimeOnly.TryParse(startDaySchedule.CloseTime, out var closeTime))
                {
                    errorMessage = "Invalid operating hours format";
                    return false;
                }

                if (startTimeOnly < openTime || endTimeOnly > closeTime)
                {
                    errorMessage = $"Schedule must be within operating hours: {startDaySchedule.OpenTime} - {startDaySchedule.CloseTime}";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as no restrictions
            return true;
        }
    }

    /// <summary>
    /// Validates if provider working hours are within organization operating hours
    /// </summary>
    /// <param name="organizationOperatingHours">Organization operating hours JSON</param>
    /// <param name="providerWorkingHours">Provider working hours JSON</param>
    /// <param name="errorMessage">Output error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool AreProviderHoursWithinOrganizationHours(
        string? organizationOperatingHours,
        string? providerWorkingHours,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        // If no provider working hours, no validation needed
        if (string.IsNullOrWhiteSpace(providerWorkingHours))
        {
            return true;
        }

        // If organization has no operating hours, allow any provider hours
        if (string.IsNullOrWhiteSpace(organizationOperatingHours))
        {
            return true;
        }

        try
        {
            var orgHours = JsonSerializer.Deserialize<Dictionary<string, DaySchedule>>(
                organizationOperatingHours,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var providerHours = JsonSerializer.Deserialize<Dictionary<string, DaySchedule>>(
                providerWorkingHours,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (orgHours is null || providerHours is null)
            {
                return true;
            }

            // Validate each day in provider working hours
            foreach (var (day, providerDay) in providerHours)
            {
                if (!providerDay.IsOpen)
                {
                    continue;
                }

                // Check if organization is open on this day
                if (!orgHours.TryGetValue(day, out var orgDay))
                {
                    errorMessage = $"Provider cannot work on {day} when organization is closed";
                    return false;
                }

                if (!orgDay.IsOpen)
                {
                    errorMessage = $"Provider cannot work on {day} when organization is closed";
                    return false;
                }

                // Validate provider hours are within organization hours
                if (!string.IsNullOrEmpty(providerDay.OpenTime) &&
                    !string.IsNullOrEmpty(providerDay.CloseTime) &&
                    !string.IsNullOrEmpty(orgDay.OpenTime) &&
                    !string.IsNullOrEmpty(orgDay.CloseTime))
                {
                    if (!TimeOnly.TryParse(providerDay.OpenTime, out var providerOpen) ||
                        !TimeOnly.TryParse(providerDay.CloseTime, out var providerClose) ||
                        !TimeOnly.TryParse(orgDay.OpenTime, out var orgOpen) ||
                        !TimeOnly.TryParse(orgDay.CloseTime, out var orgClose))
                    {
                        errorMessage = "Invalid time format in working hours";
                        return false;
                    }

                    if (providerOpen < orgOpen || providerClose > orgClose)
                    {
                        errorMessage = $"Provider working hours on {day} ({providerDay.OpenTime}-{providerDay.CloseTime}) must be within organization operating hours ({orgDay.OpenTime}-{orgDay.CloseTime})";
                        return false;
                    }
                }
            }

            return true;
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as valid
            return true;
        }
    }

    public sealed class DaySchedule
    {
        public bool IsOpen { get; set; }
        public string? OpenTime { get; set; }
        public string? CloseTime { get; set; }
    }
}
