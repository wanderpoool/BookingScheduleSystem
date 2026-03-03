using System.Security.Claims;
using BookingScheduleSystem.Contracts.Tenants;
using BookingScheduleSystem.Web.Models;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class OrganizationSettings
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private TenantResponse? _tenant;
    private bool _isLoading = true;
    private bool _isEditing;
    private bool _isSaving;
    private string? _tenantId;
    private string _editName = string.Empty;
    private string? _editDescription;
    private string? _editLocation;
    private string _editLandingPageTemplate = "simple";

    // Operating hours state
    private Dictionary<DayOfWeek, DayScheduleModel> _operatingHours = OperatingHoursHelper.GetDefaultHours();
    private Dictionary<DayOfWeek, EditableDaySchedule> _editHours = new();
    private bool _isEditingHours;
    private bool _isSavingHours;

    private static readonly DayOfWeek[] _daysOfWeek =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _tenantId = authState.User.FindFirst("TenantId")?.Value;

        if (!string.IsNullOrEmpty(_tenantId))
        {
            await LoadTenant();
        }
        else
        {
            _isLoading = false;
        }
    }

    private async Task LoadTenant()
    {
        _isLoading = true;
        try
        {
            _tenant = await TenantService.GetTenantAsync(Guid.Parse(_tenantId!));
            InitializeOperatingHours();
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

    private void StartEditing()
    {
        if (_tenant == null) return;
        _editName = _tenant.Name;
        _editDescription = _tenant.Description;
        _editLocation = _tenant.Location;
        _editLandingPageTemplate = _tenant.LandingPageTemplate ?? "simple";
        _isEditing = true;
    }

    private void CancelEditing()
    {
        _isEditing = false;
    }

    private async Task SaveChanges()
    {
        if (string.IsNullOrWhiteSpace(_editName))
        {
            Snackbar.Add("Organization name is required.", Severity.Warning);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateTenantRequest
            {
                Name = _editName.Trim(),
                Description = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription.Trim(),
                Location = string.IsNullOrWhiteSpace(_editLocation) ? null : _editLocation.Trim(),
                OperatingHours = _tenant?.OperatingHours,
                BannerUrl = _tenant?.BannerUrl,
                LandingPageTemplate = _editLandingPageTemplate
            };

            var updated = await TenantService.UpdateTenantAsync(Guid.Parse(_tenantId!), request);
            if (updated != null)
            {
                _tenant = updated;
                _isEditing = false;
                Snackbar.Add("Organization updated successfully!", Severity.Success);
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

    private void InitializeOperatingHours()
    {
        _operatingHours = OperatingHoursHelper.Parse(_tenant?.OperatingHours)
                          ?? OperatingHoursHelper.GetDefaultHours();
    }

    private void StartEditingHours()
    {
        _editHours = new Dictionary<DayOfWeek, EditableDaySchedule>();
        foreach (var day in _daysOfWeek)
        {
            var schedule = _operatingHours.GetValueOrDefault(day, new DayScheduleModel(false, null, null));
            _editHours[day] = EditableDaySchedule.FromModel(schedule);
        }
        _isEditingHours = true;
    }

    private void CancelEditingHours()
    {
        _isEditingHours = false;
    }

    private void ToggleDay(DayOfWeek day, bool isOpen)
    {
        var current = _editHours[day];
        current.IsOpen = isOpen;
        if (isOpen && current.OpenTimeSpan is null)
        {
            current.OpenTimeSpan = new TimeSpan(9, 0, 0);
            current.CloseTimeSpan = new TimeSpan(17, 0, 0);
        }
    }

    private async Task SaveOperatingHours()
    {
        _isSavingHours = true;
        StateHasChanged();

        try
        {
            var hours = new Dictionary<DayOfWeek, DayScheduleModel>();
            foreach (var (day, edit) in _editHours)
            {
                hours[day] = edit.ToModel();
            }

            var request = new UpdateTenantRequest
            {
                Name = _tenant!.Name,
                Description = _tenant.Description,
                Location = _tenant.Location,
                OperatingHours = OperatingHoursHelper.Serialize(hours),
                BannerUrl = _tenant.BannerUrl,
                LandingPageTemplate = _tenant.LandingPageTemplate
            };

            var updated = await TenantService.UpdateTenantAsync(Guid.Parse(_tenantId!), request);
            if (updated != null)
            {
                _tenant = updated;
                InitializeOperatingHours();
                _isEditingHours = false;
                Snackbar.Add("Operating hours updated!", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSavingHours = false;
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
