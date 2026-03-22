using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Tenants;

public sealed record TenantResponse
{
    public required TenantId Id { get; init; }
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }
    public bool IsInTrial { get; init; }
    public DateTime? TrialStartDate { get; init; }
    public DateTime? TrialEndDate { get; init; }
    public UserId? OwnerId { get; init; }
    public string? OperatingHours { get; init; }
    public string? BannerUrl { get; init; }
    public string? Location { get; init; }
    public string? LandingPageTemplate { get; init; }
    public bool QueueEnabled { get; init; }
    public int QueueAverageServiceTimeMinutes { get; init; } = 15;
    public int QueueNotificationLeadMinutes { get; init; } = 15;
}
