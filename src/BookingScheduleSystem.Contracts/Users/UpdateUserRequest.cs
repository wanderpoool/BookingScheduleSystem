namespace BookingScheduleSystem.Contracts.Users;

public sealed record UpdateUserRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhoneNumber { get; init; }
    public string? WorkingHours { get; init; }
    public Guid? TenantId { get; init; }
}
