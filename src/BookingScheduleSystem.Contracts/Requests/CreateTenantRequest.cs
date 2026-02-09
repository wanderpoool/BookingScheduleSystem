namespace BookingScheduleSystem.Contracts.Requests;

public sealed record CreateTenantRequest
{
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public string? Description { get; init; }
}
