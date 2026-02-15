using System.ComponentModel.DataAnnotations;

namespace BookingScheduleSystem.Web.Models;

public class LoginFormModel
{
    public string ContactMethod { get; set; } = "email";

    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
}
