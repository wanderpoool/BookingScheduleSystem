using System.Security.Claims;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Web.Components.Shared;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class MyBookings
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private IBookingService BookingService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<BookingResponse>? _bookings;
    private bool _isLoading = true;
    private bool _isCancelling;
    private bool _isSavingNotes;
    private BookingStatus? _statusFilter;
    private int _currentPage = 1;
    private int _pageSize = 20;
    private int _totalCount;
    private BookingId? _editingNotesBookingId;
    private string? _editNotesText;

    protected override async Task OnInitializedAsync()
    {
        await LoadBookings();
    }

    private async Task LoadBookings()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var result = await BookingService.ListBookingsAsync(_statusFilter, _currentPage, _pageSize);
            if (result != null)
            {
                _bookings = result.Bookings;
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

    private async Task FilterByStatus(BookingStatus? status)
    {
        _statusFilter = status;
        _currentPage = 1;
        await LoadBookings();
    }

    private async Task CancelBooking(BookingResponse booking)
    {
        var parameters = new DialogParameters<CancelBookingDialog>
        {
            { x => x.ScheduleTitle, booking.ScheduleTitle },
            { x => x.StartTime, booking.ScheduleStartTime },
            { x => x.EndTime, booking.ScheduleEndTime },
            { x => x.IsReschedule, false }
        };

        var dialog = await DialogService.ShowAsync<CancelBookingDialog>("Cancel Booking", parameters,
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return;

        var reason = result.Data as string;

        _isCancelling = true;
        StateHasChanged();

        try
        {
            var cancelResult = await BookingService.CancelBookingAsync(booking.Id.Value,
                string.IsNullOrWhiteSpace(reason) ? null : reason);
            if (cancelResult != null)
            {
                Snackbar.Add("Booking cancelled successfully.", Severity.Success);
                await LoadBookings();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isCancelling = false;
        }
    }

    private async Task RescheduleBooking(BookingResponse booking)
    {
        var parameters = new DialogParameters<CancelBookingDialog>
        {
            { x => x.ScheduleTitle, booking.ScheduleTitle },
            { x => x.StartTime, booking.ScheduleStartTime },
            { x => x.EndTime, booking.ScheduleEndTime },
            { x => x.IsReschedule, true }
        };

        var dialog = await DialogService.ShowAsync<CancelBookingDialog>("Reschedule Booking", parameters,
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return;

        _isCancelling = true;
        StateHasChanged();

        try
        {
            var cancelResult = await BookingService.CancelBookingAsync(booking.Id.Value, "Rescheduled by customer");
            if (cancelResult != null)
            {
                Snackbar.Add("Booking cancelled. Pick a new time below.", Severity.Info);
                var basePath = string.IsNullOrEmpty(Slug) ? "/browse-schedules" : $"/{Slug}/browse-schedules";
                NavigationManager.NavigateTo(basePath);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isCancelling = false;
        }
    }

    private async Task OnPageChanged(int page)
    {
        _currentPage = page;
        await LoadBookings();
    }

    private void StartEditNotes(BookingResponse booking)
    {
        _editingNotesBookingId = booking.Id;
        _editNotesText = booking.Notes;
    }

    private void CancelEditNotes()
    {
        _editingNotesBookingId = null;
        _editNotesText = null;
    }

    private async Task SaveNotes(BookingResponse booking)
    {
        _isSavingNotes = true;
        StateHasChanged();

        try
        {
            var notes = string.IsNullOrWhiteSpace(_editNotesText) ? null : _editNotesText.Trim();
            var result = await BookingService.UpdateBookingNotesAsync(booking.Id.Value, notes);
            if (result != null)
            {
                Snackbar.Add("Notes updated.", Severity.Success);
                _editingNotesBookingId = null;
                _editNotesText = null;
                await LoadBookings();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSavingNotes = false;
        }
    }

    private Color GetStatusColor(BookingStatus status) => status switch
    {
        BookingStatus.Pending => Color.Warning,
        BookingStatus.Confirmed => Color.Success,
        BookingStatus.Cancelled => Color.Error,
        BookingStatus.Completed => Color.Info,
        _ => Color.Default
    };
}
