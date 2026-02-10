namespace BookingScheduleSystem.Contracts.Users;

public sealed record ListUsersResponse
{
    public required List<UserResponse> Users { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}
