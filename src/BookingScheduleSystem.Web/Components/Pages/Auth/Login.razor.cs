using System.Timers;
using BookingScheduleSystem.Contracts.Auth;
using BookingScheduleSystem.Contracts.Tenants;
using BookingScheduleSystem.Web.Models;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages.Auth;

public partial class Login : IDisposable
{
    [Parameter] public string? Slug { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Company { get; set; }

    [Inject] private IAuthenticationService AuthService { get; set; } = default!;
    [Inject] private IOtpService OtpService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ITenantSlugService SlugService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    // Password login state
    private LoginFormModel _loginModel = new();
    private bool _showPassword;
    private bool _rememberMe;
    private bool _isLoading;
    private int _activeTab;
    private TenantResponse? _companyInfo;

    // OTP login state
    private OtpLoginStep _otpStep = OtpLoginStep.SendOtp;
    private string _otpContactMethod = "email";
    private string _otpEmail = string.Empty;
    private string _otpPhone = string.Empty;
    private string CleanPhone => "+63" + new string(_otpPhone.Where(char.IsDigit).ToArray());
    private string _otpCode = string.Empty;
    private DateTime? _otpExpiresAt;
    private int _resendCooldown;
    private System.Timers.Timer? _timer;

    private enum OtpLoginStep
    {
        SendOtp,
        VerifyOtp
    }

    protected override async Task OnInitializedAsync()
    {
        // Priority 1: Slug from URL route parameter
        if (!string.IsNullOrWhiteSpace(Slug))
        {
            _companyInfo = await TenantService.GetTenantBySubdomainAsync(Slug);
        }
        // Priority 2: Legacy ?company={guid} query parameter (backward compat)
        else if (!string.IsNullOrWhiteSpace(Company) && Guid.TryParse(Company, out var companyGuid))
        {
            _companyInfo = await TenantService.GetTenantAsync(companyGuid);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_activeTab == 0 && _loginModel.ContactMethod == "phone")
        {
            await Task.Delay(50);
            await JS.InvokeVoidAsync("phoneInput.restrict", null, "login-password-phone-input");
        }
        if (_activeTab == 1 && _otpContactMethod == "phone")
        {
            await Task.Delay(50);
            await JS.InvokeVoidAsync("phoneInput.restrict", null, "login-phone-input");
        }
    }

    private string CleanPasswordPhone => "+63" + new string((_loginModel.PhoneNumber ?? "").Where(char.IsDigit).ToArray());

    private void TogglePasswordVisibility()
    {
        _showPassword = !_showPassword;
    }

    private void OnPasswordPhoneInput(string value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length > 10)
            digits = digits[..10];
        _loginModel.PhoneNumber = digits;
    }

    private void FormatPasswordPhoneNumber()
    {
        var digits = new string((_loginModel.PhoneNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length > 10)
            digits = digits[..10];

        _loginModel.PhoneNumber = digits.Length switch
        {
            > 6 => $"{digits[..3]}-{digits[3..6]}-{digits[6..]}",
            > 3 => $"{digits[..3]}-{digits[3..]}",
            _ => digits
        };
    }

    private void OnPhoneInput(string value)
    {
        // Strip non-digits immediately, limit to 10
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length > 10)
            digits = digits[..10];
        _otpPhone = digits;
    }

    private void FormatPhoneNumber()
    {
        var digits = new string((_otpPhone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length > 10)
            digits = digits[..10];

        _otpPhone = digits.Length switch
        {
            > 6 => $"{digits[..3]}-{digits[3..6]}-{digits[6..]}",
            > 3 => $"{digits[..3]}-{digits[3..]}",
            _ => digits
        };
    }

    private async Task HandlePasswordLogin()
    {
        if (_loginModel.ContactMethod == "phone" && string.IsNullOrWhiteSpace(_loginModel.PhoneNumber))
        {
            Snackbar.Add("Please enter your phone number", Severity.Warning);
            return;
        }
        if (_loginModel.ContactMethod == "email" && string.IsNullOrWhiteSpace(_loginModel.Email))
        {
            Snackbar.Add("Please enter your email", Severity.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_loginModel.Password) || _loginModel.Password.Length < 6)
        {
            Snackbar.Add("Password must be at least 6 characters", Severity.Warning);
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            // Set tenant context for lazy enrollment
            if (_companyInfo != null)
            {
                AuthService.SetTenantContext(_companyInfo.Id.Value);
            }

            var loginRequest = new LoginRequest
            {
                Email = _loginModel.ContactMethod == "email" ? _loginModel.Email : null,
                PhoneNumber = _loginModel.ContactMethod == "phone" ? CleanPasswordPhone : null,
                Password = _loginModel.Password
            };

            var result = await AuthService.LoginAsync(loginRequest);

            if (result != null)
            {
                Snackbar.Add($"Welcome back, {result.FirstName}!", Severity.Success);
                await NavigateAfterLoginAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SendLoginOtp()
    {
        var identifier = _otpContactMethod == "email" ? _otpEmail : _otpPhone;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            Snackbar.Add($"Please enter your {_otpContactMethod}.", Severity.Warning);
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            var request = new SendOtpRequest
            {
                ContactMethod = _otpContactMethod,
                Email = _otpContactMethod == "email" ? _otpEmail : null,
                PhoneNumber = _otpContactMethod == "phone" ? CleanPhone : null,
                Purpose = "login"
            };

            var result = await OtpService.SendOtpAsync(request);

            if (result.Success)
            {
                _otpExpiresAt = result.ExpiresAt;
                _otpStep = OtpLoginStep.VerifyOtp;
                _otpCode = string.Empty;
                _resendCooldown = 30;
                StartCountdownTimer();
                Snackbar.Add("Verification code sent!", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "We couldn't send your verification code. Please try again.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task VerifyLoginOtp()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var verifyRequest = new VerifyOtpRequest
            {
                ContactMethod = _otpContactMethod,
                Email = _otpContactMethod == "email" ? _otpEmail : null,
                PhoneNumber = _otpContactMethod == "phone" ? CleanPhone : null,
                OtpCode = _otpCode,
                Purpose = "login"
            };

            var verifyResult = await OtpService.VerifyOtpAsync(verifyRequest);

            if (!verifyResult.Success || !verifyResult.IsVerified)
            {
                Snackbar.Add(verifyResult.Message ?? "The code you entered is incorrect. Please try again.", Severity.Error);
                return;
            }

            // Set tenant context for lazy enrollment
            if (_companyInfo != null)
            {
                AuthService.SetTenantContext(_companyInfo.Id.Value);
            }

            var loginRequest = new LoginWithOtpRequest
            {
                ContactMethod = _otpContactMethod,
                Email = _otpContactMethod == "email" ? _otpEmail : null,
                PhoneNumber = _otpContactMethod == "phone" ? CleanPhone : null,
                OtpVerificationToken = verifyResult.VerificationToken ?? ""
            };

            var result = await AuthService.LoginWithOtpAsync(loginRequest);

            if (result != null)
            {
                Snackbar.Add($"Welcome back, {result.FirstName}!", Severity.Success);
                await NavigateAfterLoginAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task NavigateAfterLoginAsync()
    {
        var slug = Slug ?? _companyInfo?.Subdomain;

        // If no slug yet, resolve it from the logged-in user's TenantId
        if (slug is null)
        {
            try
            {
                var authState = await AuthStateProvider.GetAuthenticationStateAsync();
                var tenantIdClaim = authState.User.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantGuid))
                {
                    var tenant = await TenantService.GetTenantAsync(tenantGuid);
                    if (tenant != null)
                    {
                        slug = tenant.Subdomain;
                        SlugService.SetSlug(slug);
                    }
                }
            }
            catch
            {
                // Non-critical — proceed without slug
            }
        }
        else
        {
            SlugService.SetSlug(slug);
        }

        // Signal App.razor to show branded loading screen during circuit reconnect
        await JS.InvokeVoidAsync("localStorage.setItem", "showBrandedLoading", "true");

        if (!string.IsNullOrWhiteSpace(ReturnUrl))
        {
            Navigation.NavigateTo(Uri.UnescapeDataString(ReturnUrl));
            return;
        }

        // Always go to dashboard — it handles onboarding redirect for new customers
        Navigation.NavigateTo(slug != null ? $"/{slug}/dashboard" : "/dashboard");
    }

    private void StartCountdownTimer()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (_, _) => InvokeAsync(() =>
        {
            if (_resendCooldown > 0) _resendCooldown--;
            StateHasChanged();
        });
        _timer.Start();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
