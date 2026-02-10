namespace BookingScheduleSystem.Contracts.Tenants;

public sealed record UpdateTenantRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? OperatingHours { get; init; }
    public string? BannerUrl { get; init; }
    public string? Location { get; init; }
}
