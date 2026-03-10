using System.Text.Json;

namespace BookingScheduleSystem.Web.Models;

public sealed record DayScheduleModel(bool IsOpen, string? OpenTime, string? CloseTime);

public static class OperatingHoursHelper
{
    private static readonly Dictionary<string, DayOfWeek> DayNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Monday"] = DayOfWeek.Monday,
        ["Tuesday"] = DayOfWeek.Tuesday,
        ["Wednesday"] = DayOfWeek.Wednesday,
        ["Thursday"] = DayOfWeek.Thursday,
        ["Friday"] = DayOfWeek.Friday,
        ["Saturday"] = DayOfWeek.Saturday,
        ["Sunday"] = DayOfWeek.Sunday
    };

    public static Dictionary<DayOfWeek, DayScheduleModel>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, DayScheduleModel>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (raw is null)
                return null;

            var result = new Dictionary<DayOfWeek, DayScheduleModel>();
            foreach (var (key, value) in raw)
            {
                if (DayNameMap.TryGetValue(key, out var dayOfWeek))
                {
                    result[dayOfWeek] = value;
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static TimeOnly? GetOpeningTime(Dictionary<DayOfWeek, DayScheduleModel>? hours, DayOfWeek day)
    {
        if (hours is null || !hours.TryGetValue(day, out var schedule))
            return null;

        if (!schedule.IsOpen || string.IsNullOrWhiteSpace(schedule.OpenTime))
            return null;

        return TimeOnly.TryParse(schedule.OpenTime, out var time) ? time : null;
    }

    public static TimeOnly? GetClosingTime(Dictionary<DayOfWeek, DayScheduleModel>? hours, DayOfWeek day)
    {
        if (hours is null || !hours.TryGetValue(day, out var schedule))
            return null;

        if (!schedule.IsOpen || string.IsNullOrWhiteSpace(schedule.CloseTime))
            return null;

        return TimeOnly.TryParse(schedule.CloseTime, out var time) ? time : null;
    }

    public static bool IsWithinOperatingHours(Dictionary<DayOfWeek, DayScheduleModel>? hours, DateTime dateTime)
    {
        if (hours is null)
            return true; // No operating hours defined = all hours available

        if (!hours.TryGetValue(dateTime.DayOfWeek, out var schedule))
            return false; // Day not in schedule = closed

        if (!schedule.IsOpen)
            return false;

        var openTime = GetOpeningTime(hours, dateTime.DayOfWeek);
        var closeTime = GetClosingTime(hours, dateTime.DayOfWeek);

        if (openTime is null || closeTime is null)
            return true; // Open but no times specified = all hours

        var timeOfDay = TimeOnly.FromDateTime(dateTime);
        return timeOfDay >= openTime && timeOfDay < closeTime;
    }

    private static readonly Dictionary<DayOfWeek, string> DayOfWeekNameMap = new()
    {
        [DayOfWeek.Monday] = "Monday",
        [DayOfWeek.Tuesday] = "Tuesday",
        [DayOfWeek.Wednesday] = "Wednesday",
        [DayOfWeek.Thursday] = "Thursday",
        [DayOfWeek.Friday] = "Friday",
        [DayOfWeek.Saturday] = "Saturday",
        [DayOfWeek.Sunday] = "Sunday"
    };

    /// <summary>
    /// Serializes operating hours dictionary back to the JSON string format the API expects.
    /// </summary>
    public static string Serialize(Dictionary<DayOfWeek, DayScheduleModel> hours)
    {
        var raw = new Dictionary<string, DayScheduleModel>();
        foreach (var (day, schedule) in hours)
        {
            if (DayOfWeekNameMap.TryGetValue(day, out var name))
            {
                raw[name] = schedule;
            }
        }

        return JsonSerializer.Serialize(raw, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    /// <summary>
    /// Returns default Mon-Fri 9:00-17:00 operating hours when tenant has none configured.
    /// </summary>
    public static Dictionary<DayOfWeek, DayScheduleModel> GetDefaultHours()
    {
        var defaultDay = new DayScheduleModel(true, "09:00", "17:00");
        var closedDay = new DayScheduleModel(false, null, null);

        return new Dictionary<DayOfWeek, DayScheduleModel>
        {
            [DayOfWeek.Monday] = defaultDay,
            [DayOfWeek.Tuesday] = defaultDay,
            [DayOfWeek.Wednesday] = defaultDay,
            [DayOfWeek.Thursday] = defaultDay,
            [DayOfWeek.Friday] = defaultDay,
            [DayOfWeek.Saturday] = closedDay,
            [DayOfWeek.Sunday] = closedDay
        };
    }
}
