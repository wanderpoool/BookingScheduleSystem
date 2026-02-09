using BookingScheduleSystem.Contracts.Auth;

namespace BookingScheduleSystem.Web.Services;

public interface IOtpService
{
    Task<OtpResponse> SendOtpAsync(SendOtpRequest request);
    Task<OtpResponse> VerifyOtpAsync(VerifyOtpRequest request);
}
