using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Tenant document stored in Marten
/// </summary>
public sealed class Tenant
{
    public TenantId Id { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    // Trial period properties
    public bool IsInTrial { get; set; }
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public UserId? OwnerId { get; set; }

    // Organization profile (shown in QR code registration)
    public string? OperatingHours { get; set; }
    public string? BannerUrl { get; set; }
    public string? Location { get; set; }
}
