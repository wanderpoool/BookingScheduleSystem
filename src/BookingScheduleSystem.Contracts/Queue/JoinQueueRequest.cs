namespace BookingScheduleSystem.Contracts.Queue;

public sealed record JoinQueueRequest
{
    public required string DailyToken { get; init; }
    public required string PhoneNumber { get; init; }
    public string? CustomerName { get; init; }
}
