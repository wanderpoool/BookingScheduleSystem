using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Bookings;

public sealed class BookingReferenceCounter
{
    public TenantId Id { get; set; }
    public int CurrentValue { get; set; }
}
