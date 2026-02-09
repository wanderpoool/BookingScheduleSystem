using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.CreationCodes;

public sealed record CreationCodeResponse
{
    public required CreationCodeId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required string Code { get; init; }
    public required int MaxUses { get; init; }
    public required int CurrentUses { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsProviderCode { get; init; }
}
