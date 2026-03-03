using System.Security.Claims;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using BookingScheduleSystem.Web.Components.Shared;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class Dashboard
{
    [Parameter] public string? Slug { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [SupplyParameterFromQuery]
    public bool? SkipOnboarding { get; set; }

    [Inject] private IBookingService BookingService { get; set; } = default!;
    [Inject] private IScheduleService ScheduleService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IAuthenticationService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ITenantSlugService SlugService { get; set; } = default!;

    private string _userName = "User";
    private bool _isLoading = true;
    private bool _hasTenant;
    private bool _isProviderOrAdmin;
    private Guid _tenantId;
    private string? _tenantLocation;
    private List<BookingResponse> _upcomingBookings = new();
    private List<BookingResponse> _pendingApprovals = new();
    private HashSet<BookingId> _processingBookingIds = new();
    private Dictionary<ScheduleId, string?> _providerNameLookup = new();

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask != null)
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity?.IsAuthenticated ?? false)
            {
                _userName = user.FindFirst(ClaimTypes.GivenName)?.Value ?? "User";

                var role = user.FindFirst(ClaimTypes.Role)?.Value;
                _isProviderOrAdmin = role is "Provider" or "GlobalAdmin";

                var tenantIdClaim = user.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantGuid))
                {
                    _hasTenant = true;
                    _tenantId = tenantGuid;
                }
            }
        }

        if (!_hasTenant)
        {
            _isLoading = false;
            return;
        }

        try
        {
            await LoadDashboardData();

            // Redirect first-time customers (no upcoming bookings) to onboarding wizard
            if (SkipOnboarding != true && !_isProviderOrAdmin && _hasTenant && _upcomingBookings.Count == 0)
            {
                Navigation.NavigateTo(SlugService.BuildUrl("/onboarding"));
                return;
            }
        }
        catch
        {
            // Dashboard should not fail if API is unreachable
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadDashboardData()
    {
        // Load confirmed + pending bookings with future dates
        var confirmedTask = BookingService.ListBookingsAsync(BookingStatus.Confirmed, 1, 10);
        var pendingTask = BookingService.ListBookingsAsync(BookingStatus.Pending, 1, 10);
        var tenantTask = TenantService.GetTenantAsync(_tenantId);

        await Task.WhenAll(confirmedTask, pendingTask, tenantTask);

        var confirmedResult = confirmedTask.Result;
        var pendingResult = pendingTask.Result;
        var tenant = tenantTask.Result;

        _tenantLocation = tenant?.Location;

        // Store pending approvals for provider/admin dashboard
        if (_isProviderOrAdmin && pendingResult?.Bookings != null)
        {
            _pendingApprovals = pendingResult.Bookings
                .OrderByDescending(b => b.BookedAt)
                .ToList();
        }

        // Merge and filter to future bookings only
        var allBookings = new List<BookingResponse>();
        if (confirmedResult?.Bookings != null)
            allBookings.AddRange(confirmedResult.Bookings);
        if (pendingResult?.Bookings != null)
            allBookings.AddRange(pendingResult.Bookings);

        _upcomingBookings = allBookings
            .Where(b => b.ScheduleStartTime > DateTime.UtcNow)
            .OrderBy(b => b.ScheduleStartTime)
            .ToList();

        // Load schedules for provider name lookup on booking cards
        if (_upcomingBookings.Count > 0)
        {
            var schedulesResult = await ScheduleService.ListPublicSchedulesAsync(_tenantId, null, null, 1, 50);

            if (schedulesResult?.Providers != null)
            {
                foreach (var provider in schedulesResult.Providers)
                {
                    foreach (var day in provider.Days)
                    {
                        foreach (var block in day.TimeBlocks)
                        {
                            if (block.ScheduleId is not null)
                            {
                                _providerNameLookup[block.ScheduleId.Value] = provider.ProviderName;
                            }
                        }
                    }
                }
            }
        }
    }

    private string? GetProviderName(ScheduleId scheduleId)
    {
        return _providerNameLookup.TryGetValue(scheduleId, out var name) ? name : null;
    }

    private async Task HandleLogout()
    {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo(SlugService.BuildUrl("/login"), forceLoad: true);
    }

    private async Task HandleApprove(BookingResponse booking)
    {
        _processingBookingIds.Add(booking.Id);
        StateHasChanged();

        try
        {
            await BookingService.ApproveBookingAsync(booking.Id.Value);
            _pendingApprovals.Remove(booking);
            Snackbar.Add($"Booking for \"{booking.ScheduleTitle}\" approved", Severity.Success);
            await LoadDashboardData();
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _processingBookingIds.Remove(booking.Id);
            StateHasChanged();
        }
    }

    private async Task HandleReject(BookingResponse booking)
    {
        var parameters = new DialogParameters<RejectReasonDialog>
        {
            { x => x.ScheduleTitle, booking.ScheduleTitle ?? "this schedule" }
        };

        var dialog = await DialogService.ShowAsync<RejectReasonDialog>("Reject Booking", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return;

        var reason = result.Data as string;

        _processingBookingIds.Add(booking.Id);
        StateHasChanged();

        try
        {
            await BookingService.RejectBookingAsync(booking.Id.Value, string.IsNullOrWhiteSpace(reason) ? null : reason);
            _pendingApprovals.Remove(booking);
            Snackbar.Add($"Booking for \"{booking.ScheduleTitle}\" rejected", Severity.Info);
            await LoadDashboardData();
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _processingBookingIds.Remove(booking.Id);
            StateHasChanged();
        }
    }

    private static string GetTimeAgo(DateTime bookedAt)
    {
        var elapsed = DateTime.UtcNow - bookedAt;
        if (elapsed.TotalMinutes < 1)
            return "just now";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays}d ago";
        return bookedAt.ToString("MMM dd");
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
