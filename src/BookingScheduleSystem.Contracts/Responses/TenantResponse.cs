using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Responses;

public sealed record TenantResponse
{
    public required TenantId Id { get; init; }
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }
}
