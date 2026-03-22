namespace BookingScheduleSystem.Contracts.Tenants;

public sealed record UpdateTenantRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? OperatingHours { get; init; }
    public string? BannerUrl { get; init; }
    public string? Location { get; init; }
    public string? LandingPageTemplate { get; init; }
    public bool? QueueEnabled { get; init; }
    public int? QueueAverageServiceTimeMinutes { get; init; }
    public int? QueueNotificationLeadMinutes { get; init; }
}
