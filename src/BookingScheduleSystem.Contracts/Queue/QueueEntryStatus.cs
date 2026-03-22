namespace BookingScheduleSystem.Contracts.Queue;

public enum QueueEntryStatus
{
    Waiting,
    Called,
    InService,
    Completed,
    Skipped,
    NoShow
}
