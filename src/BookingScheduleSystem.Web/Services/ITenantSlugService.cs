namespace BookingScheduleSystem.Web.Services;

public interface ITenantSlugService
{
    string? CurrentSlug { get; }
    void SetSlug(string? slug);
    string BuildUrl(string path);
}
