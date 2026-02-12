using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using MudBlazor;

namespace BookingScheduleSystem.Web.Models;

public class ScheduleCalendarItem
{
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public string Text { get; set; } = string.Empty;

    public required TimeBlock TimeBlock { get; init; }
    public required UserId ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public string Title { get; set; } = string.Empty;
    public Color Color { get; set; } = Color.Primary;
    public bool IsAvailabilityBlock => TimeBlock.IsAvailable && TimeBlock.ScheduleId is null;

    public static List<ScheduleCalendarItem> FromProviders(List<ProviderAvailabilityResponse> providers)
    {
        var items = new List<ScheduleCalendarItem>();

        foreach (var provider in providers)
        {
            foreach (var day in provider.Days)
            {
                foreach (var block in day.TimeBlocks)
                {
                    items.Add(new ScheduleCalendarItem
                    {
                        TimeBlock = block,
                        ProviderId = provider.ProviderId,
                        ProviderName = provider.ProviderName,
                        Title = block.IsAvailable
                            ? (block.ScheduleTitle ?? "Available")
                            : (block.CustomerName ?? "Booked"),
                        Text = block.IsAvailable
                            ? provider.ProviderName
                            : (block.BookingNotes ?? block.ScheduleTitle ?? ""),
                        Start = block.StartTime,
                        End = block.EndTime,
                        Color = block.IsAvailable ? Color.Success : Color.Error
                    });
                }
            }
        }

        return items;
    }
}
