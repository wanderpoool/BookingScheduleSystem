namespace BookingScheduleSystem.Web.Services;

public interface ITenantSlugService
{
    string? CurrentSlug { get; }
    bool QueueEnabled { get; }
    void SetSlug(string? slug);
    void SetQueueEnabled(bool enabled);
    string BuildUrl(string path);
}
