using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.CreationCodes;

public sealed record CreateCreationCodeRequest
{
    public required TenantId TenantId { get; init; }
    public int MaxUses { get; init; } = 10;
    public int? ExpiresInDays { get; init; }
    public bool IsProviderCode { get; init; } = false;
}
