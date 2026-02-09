using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Tenant-scoped creation code for secure user onboarding
/// </summary>
public sealed class CreationCode
{
    public CreationCodeId Id { get; set; }
    public required TenantId TenantId { get; set; }
    public required string Code { get; set; }
    public int MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public required UserId CreatedBy { get; set; }
    public bool IsActive { get; set; }
    public bool IsProviderCode { get; set; }
}
