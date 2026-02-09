namespace BookingScheduleSystem.Contracts.Tenants;

public sealed record CreateOrganizationRequest
{
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public string? Description { get; init; }
    public string? OperatingHours { get; init; }
    public string? BannerUrl { get; init; }
    public string? Location { get; init; }
}
