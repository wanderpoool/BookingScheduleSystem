using System.ComponentModel.DataAnnotations;

namespace BookingScheduleSystem.Web.Models;

public class RegisterFormModel
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Contact method selection: "email" or "phone"
    /// </summary>
    public string ContactMethod { get; set; } = "email";

    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? CreationCode { get; set; }

    /// <summary>
    /// 6-digit OTP code entered by user
    /// </summary>
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>
    /// Verification token received after OTP is verified
    /// </summary>
    public string? OtpVerificationToken { get; set; }
}
