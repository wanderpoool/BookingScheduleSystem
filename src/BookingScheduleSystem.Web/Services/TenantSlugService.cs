namespace BookingScheduleSystem.Web.Services;

public class TenantSlugService : ITenantSlugService
{
    public string? CurrentSlug { get; private set; }

    public void SetSlug(string? slug)
    {
        CurrentSlug = slug?.ToLowerInvariant();
    }

    public string BuildUrl(string path)
    {
        if (string.IsNullOrEmpty(CurrentSlug))
            return path;

        if (!path.StartsWith('/'))
            path = "/" + path;

        return $"/{CurrentSlug}{path}";
    }
}
