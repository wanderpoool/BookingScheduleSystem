using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record ProrationPreviewResponse
{
    public required string CurrentPlanName { get; init; }
    public required decimal CurrentPlanPrice { get; init; }
    public required string NewPlanName { get; init; }
    public required decimal NewPlanPrice { get; init; }
    public required int DaysRemaining { get; init; }
    public required int TotalDaysInCycle { get; init; }
    public required decimal ProratedCredit { get; init; }
    public required decimal ProratedCharge { get; init; }
    public required decimal NetAmount { get; init; }
    public required DateTime EffectiveDate { get; init; }
    public required bool IsUpgrade { get; init; }
}
