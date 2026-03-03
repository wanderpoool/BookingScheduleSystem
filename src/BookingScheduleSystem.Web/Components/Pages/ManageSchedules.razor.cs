using System.Security.Claims;
using BookingScheduleSystem.Contracts.Schedules;
using BookingScheduleSystem.Web.Components.Shared;
using BookingScheduleSystem.Web.Models;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class ManageSchedules
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private IScheduleService ScheduleService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IQrCodeService QrCodeService { get; set; } = default!;
    [Inject] private ITenantSlugService SlugService { get; set; } = default!;

    private enum ViewMode { List, Calendar }

    private List<ScheduleResponse>? _schedules;
    private List<ScheduleCalendarItem> _calendarItems = new();
    private bool _isLoading = true;
    private bool _isCalendarLoading;
    private bool? _activeFilter;
    private ViewMode _viewMode = ViewMode.List;
    private int _currentPage = 1;
    private int _pageSize = 20;
    private int _totalCount;
    private DateRange? _lastDateRange;
    private string? _tenantId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _tenantId = authState.User.FindFirst("TenantId")?.Value;
        await LoadSchedules();
    }

    private async Task LoadSchedules()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var result = await ScheduleService.ListSchedulesAsync(
                isActive: _activeFilter,
                page: _currentPage,
                pageSize: _pageSize);

            if (result != null)
            {
                _schedules = result.Providers
                    .SelectMany(p => p.Days)
                    .SelectMany(d => d.TimeBlocks)
                    .Where(tb => tb.ScheduleId.HasValue)
                    .Select(tb => new ScheduleResponse
                    {
                        Id = tb.ScheduleId!.Value,
                        TenantId = default,
                        Title = tb.ScheduleTitle ?? "Untitled",
                        StartTime = tb.StartTime,
                        EndTime = tb.EndTime,
                        IsActive = tb.IsActive,
                        MaxCapacity = tb.MaxCapacity,
                        CurrentBookings = tb.CurrentBookings
                    })
                    .DistinctBy(s => s.Id)
                    .OrderBy(s => s.StartTime)
                    .ToList();
                _totalCount = result.TotalCount;
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
    }

    private async Task LoadCalendarSchedules()
    {
        if (_lastDateRange?.Start is null || _lastDateRange?.End is null) return;

        _isCalendarLoading = true;
        StateHasChanged();

        try
        {
            var s = _lastDateRange.Start.Value;
            var e = _lastDateRange.End.Value;
            var startDate = new DateTime(s.Year, s.Month, s.Day, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(e.Year, e.Month, e.Day, 0, 0, 0, DateTimeKind.Utc);
            if (endDate <= startDate)
                endDate = startDate.AddDays(1);

            var result = await ScheduleService.ListSchedulesAsync(
                startDate: startDate,
                endDate: endDate,
                isActive: _activeFilter,
                pageSize: 200);

            _calendarItems = result?.Providers != null
                ? ScheduleCalendarItem.FromProviders(result.Providers)
                : new();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isCalendarLoading = false;
        }

        StateHasChanged();
    }

    private async Task OnDateRangeChanged(DateRange dateRange)
    {
        _lastDateRange = dateRange;
        await LoadCalendarSchedules();
    }

    private async Task OnCalendarItemClicked(ScheduleCalendarItem item)
    {
        if (item.TimeBlock.ScheduleId.HasValue)
        {
            try
            {
                var schedule = await ScheduleService.GetScheduleAsync(item.TimeBlock.ScheduleId.Value.Value);
                if (schedule != null)
                {
                    await OpenEditDialog(schedule);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.Message, Severity.Error);
            }
        }
    }

    private async Task FilterByActive(bool? active)
    {
        _activeFilter = active;
        _currentPage = 1;

        if (_viewMode == ViewMode.Calendar)
        {
            await LoadCalendarSchedules();
        }
        else
        {
            await LoadSchedules();
        }
    }

    private async Task SetViewMode(ViewMode mode)
    {
        _viewMode = mode;
        if (mode == ViewMode.List)
        {
            await LoadSchedules();
        }
    }

    private async Task OnPageChanged(int page)
    {
        _currentPage = page;
        await LoadSchedules();
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters<ScheduleFormDialog>
        {
            { x => x.IsEditMode, false }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ScheduleFormDialog>("Create Schedule", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: ScheduleFormDialog.ScheduleFormResult formResult })
        {
            try
            {
                var request = new CreateScheduleRequest
                {
                    Title = formResult.Title,
                    Description = formResult.Description,
                    StartTime = formResult.StartTime,
                    EndTime = formResult.EndTime,
                    MaxCapacity = formResult.MaxCapacity
                };

                var created = await ScheduleService.CreateScheduleAsync(request);
                if (created != null)
                {
                    Snackbar.Add("Schedule created successfully!", Severity.Success);
                    await RefreshCurrentView();
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.Message, Severity.Error);
            }
        }
    }

    private async Task OpenEditDialog(ScheduleResponse schedule)
    {
        var parameters = new DialogParameters<ScheduleFormDialog>
        {
            { x => x.IsEditMode, true },
            { x => x.ExistingSchedule, schedule }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ScheduleFormDialog>("Edit Schedule", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: ScheduleFormDialog.ScheduleFormResult formResult })
        {
            try
            {
                var request = new UpdateScheduleRequest
                {
                    Title = formResult.Title,
                    Description = formResult.Description,
                    StartTime = formResult.StartTime,
                    EndTime = formResult.EndTime,
                    MaxCapacity = formResult.MaxCapacity,
                    IsActive = formResult.IsActive
                };

                var updated = await ScheduleService.UpdateScheduleAsync(schedule.Id.Value, request);
                if (updated != null)
                {
                    Snackbar.Add("Schedule updated successfully!", Severity.Success);
                    await RefreshCurrentView();
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.Message, Severity.Error);
            }
        }
    }

    private async Task ConfirmDelete(ScheduleResponse schedule)
    {
        var parameters = new DialogParameters<DeleteConfirmDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete \"{schedule.Title}\"? This action will deactivate the schedule." }
        };

        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };
        var dialog = await DialogService.ShowAsync<DeleteConfirmDialog>("Delete Schedule", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await ScheduleService.DeleteScheduleAsync(schedule.Id.Value);
                Snackbar.Add("Schedule deleted successfully.", Severity.Success);
                await RefreshCurrentView();
            }
            catch (Exception ex)
            {
                Snackbar.Add(ex.Message, Severity.Error);
            }
        }
    }

    private async Task RefreshCurrentView()
    {
        if (_viewMode == ViewMode.Calendar)
        {
            await LoadCalendarSchedules();
        }
        else
        {
            await LoadSchedules();
        }
    }

    private async Task OpenShareDialog()
    {
        var parameters = new DialogParameters<ShareScheduleDialog>
        {
            { x => x.TenantSlug, SlugService.CurrentSlug }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true
        };

        await DialogService.ShowAsync<ShareScheduleDialog>("Share Booking Page", parameters, options);
    }

    private static double GetBookingPercentage(ScheduleResponse schedule)
    {
        if (schedule.MaxCapacity <= 0) return 0;
        return Math.Min(100, (double)schedule.CurrentBookings / schedule.MaxCapacity * 100);
    }
}
