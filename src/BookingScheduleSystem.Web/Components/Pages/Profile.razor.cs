using System.Security.Claims;
using BookingScheduleSystem.Contracts.Users;
using BookingScheduleSystem.Web.Models;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class Profile
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private UserResponse? _user;
    private bool _isLoading = true;
    private bool _isEditing;
    private bool _isSaving;
    private bool _isTogglingAutoAccept;
    private string? _userId;
    private string _editFirstName = string.Empty;
    private string _editLastName = string.Empty;
    private string? _editPhone;

    // Working hours state
    private Dictionary<DayOfWeek, DayScheduleModel> _orgOperatingHours = OperatingHoursHelper.GetDefaultHours();
    private Dictionary<DayOfWeek, DayScheduleModel>? _providerWorkingHours;
    private Dictionary<DayOfWeek, EditableDaySchedule> _editWorkingHours = new();
    private bool _isEditingWorkingHours;
    private bool _isSavingWorkingHours;
    private bool _hasCustomHours;
    private bool _orgHoursLoaded;
    private string? _tenantId;

    private static readonly DayOfWeek[] _daysOfWeek =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _tenantId = authState.User.FindFirst("TenantId")?.Value;

        if (!string.IsNullOrEmpty(_userId))
        {
            await LoadProfile();
        }
        else
        {
            _isLoading = false;
        }
    }

    private async Task LoadProfile()
    {
        _isLoading = true;
        try
        {
            _user = await UserService.GetUserAsync(Guid.Parse(_userId!));
            InitializeProviderWorkingHours();

            if (_user?.IsProvider == true)
            {
                await LoadOrgHours();
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

    private async Task LoadOrgHours()
    {
        if (string.IsNullOrEmpty(_tenantId)) return;

        try
        {
            var tenant = await TenantService.GetTenantAsync(Guid.Parse(_tenantId));
            if (tenant != null)
            {
                _orgOperatingHours = OperatingHoursHelper.Parse(tenant.OperatingHours)
                                     ?? OperatingHoursHelper.GetDefaultHours();
                _orgHoursLoaded = true;
            }
        }
        catch
        {
            // Fall back to defaults — non-critical
            _orgOperatingHours = OperatingHoursHelper.GetDefaultHours();
            _orgHoursLoaded = true;
        }
    }

    private void InitializeProviderWorkingHours()
    {
        _providerWorkingHours = OperatingHoursHelper.Parse(_user?.WorkingHours);
        _hasCustomHours = _providerWorkingHours != null;
    }

    private void StartEditing()
    {
        if (_user == null) return;
        _editFirstName = _user.FirstName;
        _editLastName = _user.LastName;
        _editPhone = _user.PhoneNumber;
        _isEditing = true;
    }

    private void CancelEditing()
    {
        _isEditing = false;
    }

    private async Task ToggleAutoAccept(bool newValue)
    {
        if (_user == null) return;

        _isTogglingAutoAccept = true;
        try
        {
            var request = new UpdateUserRequest
            {
                AutoAcceptBookings = newValue
            };

            var updated = await UserService.UpdateUserAsync(Guid.Parse(_userId!), request);
            if (updated != null)
            {
                _user = updated;
                Snackbar.Add(newValue
                    ? "Auto-accept enabled. New bookings will be confirmed automatically."
                    : "Auto-accept disabled. New bookings will require your approval.",
                    Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isTogglingAutoAccept = false;
        }
    }

    private async Task SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(_editFirstName) || string.IsNullOrWhiteSpace(_editLastName))
        {
            Snackbar.Add("First and last name are required.", Severity.Warning);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateUserRequest
            {
                FirstName = _editFirstName.Trim(),
                LastName = _editLastName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(_editPhone) ? null : _editPhone.Trim()
            };

            var updated = await UserService.UpdateUserAsync(Guid.Parse(_userId!), request);
            if (updated != null)
            {
                _user = updated;
                _isEditing = false;
                Snackbar.Add("Profile updated successfully!", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    // Working hours methods

    private void StartEditingWorkingHours()
    {
        _editWorkingHours = new Dictionary<DayOfWeek, EditableDaySchedule>();
        foreach (var day in _daysOfWeek)
        {
            var orgSchedule = _orgOperatingHours.GetValueOrDefault(day);
            var orgClosed = orgSchedule is null or { IsOpen: false };

            if (orgClosed)
            {
                _editWorkingHours[day] = new EditableDaySchedule { IsOpen = false };
            }
            else if (_hasCustomHours && _providerWorkingHours!.TryGetValue(day, out var providerSchedule))
            {
                _editWorkingHours[day] = EditableDaySchedule.FromModel(providerSchedule);
            }
            else
            {
                // Default to org hours for new customization
                _editWorkingHours[day] = EditableDaySchedule.FromModel(orgSchedule!);
            }
        }
        _isEditingWorkingHours = true;
    }

    private void CancelEditingWorkingHours()
    {
        _isEditingWorkingHours = false;
    }

    private void ToggleWorkingDay(DayOfWeek day, bool isOpen, DayScheduleModel orgSchedule)
    {
        var current = _editWorkingHours[day];
        current.IsOpen = isOpen;
        if (isOpen && current.OpenTimeSpan is null)
        {
            // Default to org hours when enabling
            current.OpenTimeSpan = TimeOnly.TryParse(orgSchedule.OpenTime, out var open)
                ? open.ToTimeSpan()
                : new TimeSpan(9, 0, 0);
            current.CloseTimeSpan = TimeOnly.TryParse(orgSchedule.CloseTime, out var close)
                ? close.ToTimeSpan()
                : new TimeSpan(17, 0, 0);
        }
    }

    private async Task SaveWorkingHours()
    {
        _isSavingWorkingHours = true;
        StateHasChanged();

        try
        {
            var hours = new Dictionary<DayOfWeek, DayScheduleModel>();
            foreach (var (day, edit) in _editWorkingHours)
            {
                hours[day] = edit.ToModel();
            }

            var request = new UpdateUserRequest
            {
                WorkingHours = OperatingHoursHelper.Serialize(hours)
            };

            var updated = await UserService.UpdateUserAsync(Guid.Parse(_userId!), request);
            if (updated != null)
            {
                _user = updated;
                InitializeProviderWorkingHours();
                _isEditingWorkingHours = false;
                Snackbar.Add("Working hours updated!", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSavingWorkingHours = false;
        }
    }

    private async Task ResetWorkingHours()
    {
        _isSavingWorkingHours = true;
        StateHasChanged();

        try
        {
            var request = new UpdateUserRequest
            {
                WorkingHours = ""
            };

            var updated = await UserService.UpdateUserAsync(Guid.Parse(_userId!), request);
            if (updated != null)
            {
                _user = updated;
                InitializeProviderWorkingHours();
                Snackbar.Add("Working hours reset to organization defaults.", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSavingWorkingHours = false;
        }
    }

    private static string FormatTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return "--";
        return TimeOnly.TryParse(time, out var t) ? t.ToString("h:mm tt") : time;
    }

    private sealed class EditableDaySchedule
    {
        public bool IsOpen { get; set; }
        public TimeSpan? OpenTimeSpan { get; set; }
        public TimeSpan? CloseTimeSpan { get; set; }

        public static EditableDaySchedule FromModel(DayScheduleModel model)
        {
            return new EditableDaySchedule
            {
                IsOpen = model.IsOpen,
                OpenTimeSpan = TimeOnly.TryParse(model.OpenTime, out var open) ? open.ToTimeSpan() : null,
                CloseTimeSpan = TimeOnly.TryParse(model.CloseTime, out var close) ? close.ToTimeSpan() : null
            };
        }

        public DayScheduleModel ToModel()
        {
            if (!IsOpen)
                return new DayScheduleModel(false, null, null);

            var openStr = OpenTimeSpan.HasValue
                ? TimeOnly.FromTimeSpan(OpenTimeSpan.Value).ToString("HH:mm")
                : null;
            var closeStr = CloseTimeSpan.HasValue
                ? TimeOnly.FromTimeSpan(CloseTimeSpan.Value).ToString("HH:mm")
                : null;

            return new DayScheduleModel(true, openStr, closeStr);
        }
    }
}
