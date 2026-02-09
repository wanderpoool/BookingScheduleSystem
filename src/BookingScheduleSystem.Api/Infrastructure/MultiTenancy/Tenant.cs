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
}
