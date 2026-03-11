using System.Security.Claims;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using BookingScheduleSystem.Web.Components.Shared;
using BookingScheduleSystem.Web.Models;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class BrowseSchedules
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private IScheduleService ScheduleService { get; set; } = default!;
    [Inject] private IBookingService BookingService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ILogger<BrowseSchedules> _logger { get; set; } = default!;

    private List<ScheduleCalendarItem> _calendarItems = new();
    private Dictionary<Guid, BookingResponse> _pendingBookingsBySchedule = new();
    private string? _tenantId;
    private string? _userId;
    private string _userFullName = "";
    private bool _isCustomer;
    private bool _isProvider;
    private bool _isLoading;
    private DateRange? _lastDateRange;
    private BookMeCalendarView _providerView = BookMeCalendarView.Week;
    private TimeOnly _businessStartTime = new(9, 0);

    private static string GetBookingStatusColor(BookingStatus? status) => status switch
    {
        BookingStatus.Confirmed => "var(--mud-palette-success)",
        BookingStatus.Pending => "var(--mud-palette-warning)",
        _ => "var(--mud-palette-primary)"
    };

    private static string GetBookingStatusBg(BookingStatus? status) => status switch
    {
        BookingStatus.Confirmed => "var(--color-success-light)",
        BookingStatus.Pending => "var(--color-warning-light)",
        _ => "var(--color-error-light)"
    };

    private static string GetBookingStatusLabel(BookingStatus? status) => status switch
    {
        BookingStatus.Confirmed => "Confirmed",
        BookingStatus.Pending => "Pending",
        _ => "Booked"
    };

    private string GetColor(Color color, bool solid = false)
    {
        var (hex, light) = color switch
        {
            Color.Success => ("var(--mud-palette-success)", "var(--color-success-light)"),
            Color.Error => ("var(--mud-palette-error)", "var(--color-error-light)"),
            Color.Warning => ("var(--mud-palette-warning)", "var(--color-warning-light)"),
            Color.Info => ("var(--mud-palette-info)", "var(--color-info-light)"),
            _ => ("var(--mud-palette-text-secondary)", "var(--color-action-disabled)")
        };
        return solid ? hex : light;
    }

    private static string GetProviderCellAccent(ScheduleCalendarItem item) => item.TimeBlock.BookingStatus switch
    {
        BookingStatus.Confirmed => "var(--mud-palette-success)",
        BookingStatus.Pending => "var(--mud-palette-warning)",
        BookingStatus.Cancelled => "var(--mud-palette-error)",
        _ => "var(--mud-palette-warning)"
    };

    private static string GetProviderCellBg(ScheduleCalendarItem item) => item.TimeBlock.BookingStatus switch
    {
        BookingStatus.Confirmed => "var(--color-success-light)",
        BookingStatus.Pending => "var(--color-warning-light)",
        BookingStatus.Cancelled => "var(--color-error-light)",
        _ => "var(--color-warning-light)"
    };

    private static string GetProviderCellLabel(ScheduleCalendarItem item)
    {
        var status = item.TimeBlock.BookingStatus switch
        {
            BookingStatus.Confirmed => "Confirmed",
            BookingStatus.Pending => "Pending",
            BookingStatus.Cancelled => "Cancelled",
            _ => "Pending"
        };
        return !string.IsNullOrWhiteSpace(item.TimeBlock.ScheduleTitle)
            ? $"{item.TimeBlock.ScheduleTitle}"
            : status;
    }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _tenantId = user.FindFirst("TenantId")?.Value;
        _userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var firstName = user.FindFirst(ClaimTypes.GivenName)?.Value ?? "";
        var lastName = user.FindFirst(ClaimTypes.Surname)?.Value ?? "";
        _userFullName = $"{firstName} {lastName}".Trim();

        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        _isCustomer = role is not ("Provider" or "GlobalAdmin");
        _isProvider = role is "Provider";

        // Compute earliest business start time from tenant operating hours
        if (!string.IsNullOrEmpty(_tenantId))
        {
            try
            {
                var tenant = await TenantService.GetTenantAsync(Guid.Parse(_tenantId));
                var hours = OperatingHoursHelper.Parse(tenant?.OperatingHours)
                            ?? OperatingHoursHelper.GetDefaultHours();

                var earliest = hours.Values
                    .Where(d => d.IsOpen && !string.IsNullOrWhiteSpace(d.OpenTime))
                    .Select(d => TimeOnly.TryParse(d.OpenTime, out var t) ? t : (TimeOnly?)null)
                    .Where(t => t.HasValue)
                    .Select(t => t!.Value)
                    .DefaultIfEmpty(new TimeOnly(9, 0))
                    .Min();

                _businessStartTime = earliest;
            }
            catch
            {
                // Fall back to default 9:00 AM
            }
        }
    }

    private async Task OnDateRangeChanged(DateRange dateRange)
    {
        if (string.IsNullOrEmpty(_tenantId)) return;
        if (dateRange.Start is null || dateRange.End is null) return;

        _lastDateRange = dateRange;
        _isLoading = true;
        StateHasChanged();

        try
        {
            var tenantGuid = Guid.Parse(_tenantId);
            // Strip timezone — schedule times are stored without timezone
            var s = dateRange.Start.Value;
            var e = dateRange.End.Value;
            var startDate = new DateTime(s.Year, s.Month, s.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var endDate = new DateTime(e.Year, e.Month, e.Day, 0, 0, 0, DateTimeKind.Unspecified);
            // Day view sends Start==End, so expand to cover the full day
            if (endDate <= startDate)
                endDate = startDate.AddDays(1);

            var result = await ScheduleService.ListPublicSchedulesAsync(
                tenantGuid, startDate, endDate, 1, 200);

            var items = result?.Providers != null
                ? ScheduleCalendarItem.FromProviders(result.Providers)
                : new();

            if (_isCustomer)
            {
                _calendarItems = SplitIntoHourlySlots(items);
            }
            else if (_isProvider && Guid.TryParse(_userId, out var providerGuid))
            {
                // Provider view: only show active bookings (exclude cancelled/rejected)
                _calendarItems = items
                    .Where(i => i.ProviderId.Value == providerGuid
                                && !i.TimeBlock.IsAvailable
                                && i.TimeBlock.BookingId is not null
                                && i.TimeBlock.BookingStatus is not BookingStatus.Cancelled)
                    .ToList();

                // Fallback: fetch pending bookings to map ScheduleId → BookingId
                await LoadPendingBookingsLookup();
            }
            else
            {
                // Admin view: only show time blocks with actual bookings
                _calendarItems = items
                    .Where(i => !i.TimeBlock.IsAvailable && i.TimeBlock.BookingId is not null)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }

        StateHasChanged();
    }

    private async Task OnCellClicked(DateTime clickedTime)
    {
        if (_isCustomer)
        {
            await ShowBookingForm(clickedTime);
        }
    }

    private async Task OnItemClicked(ScheduleCalendarItem item)
    {
        if (_isCustomer)
        {
            await ShowBookingForm(item.Start);
        }
        else
        {
            await OnProviderItemClicked(item);
        }
    }

    private async Task ShowBookingForm(DateTime selectedTime)
    {
        var parameters = new DialogParameters<ProviderSelectionDialog>
        {
            { x => x.SelectedTime, selectedTime },
            { x => x.AllCalendarItems, _calendarItems }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ProviderSelectionDialog>("Book Appointment", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false } && _lastDateRange is not null)
        {
            await OnDateRangeChanged(_lastDateRange);
        }
    }

    private static List<ScheduleCalendarItem> SplitIntoHourlySlots(List<ScheduleCalendarItem> items)
    {
        var result = new List<ScheduleCalendarItem>();

        foreach (var item in items)
        {
            var duration = (item.End ?? item.Start.AddHours(1)) - item.Start;
            if (duration.TotalHours <= 1)
            {
                result.Add(item);
                continue;
            }

            // Split into 1-hour chunks
            var current = item.Start;
            var endTime = item.End ?? item.Start.AddHours(1);
            while (current.AddHours(1) <= endTime)
            {
                result.Add(new ScheduleCalendarItem
                {
                    TimeBlock = item.TimeBlock,
                    ProviderId = item.ProviderId,
                    ProviderName = item.ProviderName,
                    Title = item.Title,
                    Text = item.Text,
                    Start = current,
                    End = current.AddHours(1),
                    Color = item.Color
                });
                current = current.AddHours(1);
            }
        }

        return result;
    }

    private async Task LoadPendingBookingsLookup()
    {
        try
        {
            var result = await BookingService.ListBookingsAsync(status: BookingStatus.Pending, pageSize: 100);
            _pendingBookingsBySchedule = result?.Bookings
                ?.ToDictionary(b => b.ScheduleId.Value, b => b)
                ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load pending bookings for lookup");
        }
    }

    private async Task OnProviderItemClicked(ScheduleCalendarItem item)
    {
        if (item.TimeBlock.IsAvailable && item.TimeBlock.CustomerName is null)
        {
            Snackbar.Add($"Available slot: {item.TimeBlock.ScheduleTitle ?? "Schedule"}", Severity.Info);
            return;
        }

        // Resolve BookingId: prefer from TimeBlock, fallback to pending bookings lookup
        Guid? bookingId = item.TimeBlock.BookingId?.Value;
        if (bookingId is null && item.TimeBlock.ScheduleId.HasValue)
        {
            _pendingBookingsBySchedule.TryGetValue(item.TimeBlock.ScheduleId.Value.Value, out var pending);
            bookingId = pending?.Id.Value;
        }

        var parameters = new DialogParameters<BookingActionDialog>
        {
            { x => x.Item, item },
            { x => x.BookingId, bookingId }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<BookingActionDialog>(string.Empty, parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false } && _lastDateRange is not null)
        {
            await OnDateRangeChanged(_lastDateRange);
        }
    }
}
