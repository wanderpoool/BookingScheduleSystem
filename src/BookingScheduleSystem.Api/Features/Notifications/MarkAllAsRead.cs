using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Notifications;

public sealed class MarkAllAsRead : EndpointWithoutRequest
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/notifications/read-all");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Notifications")
            .WithSummary("Mark all notifications as read"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
            return;
        }

        var userId = Guid.Parse(userIdClaim);

        await using var session = DocumentStore.LightweightSession();

        var unreadNotifications = await session.Query<InAppNotification>()
            .Where(n => n.UserId == userId && n.TenantId == tenantId!.Value.Value && !n.IsRead)
            .ToListAsync(ct);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            session.Update(notification);
        }

        await session.SaveChangesAsync(ct);
        HttpContext.Response.StatusCode = 200;
    }
}
