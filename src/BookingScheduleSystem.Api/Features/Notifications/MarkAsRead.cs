using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Notifications;

public sealed class MarkAsReadRequest
{
    public Guid Id { get; set; }
}

public sealed class MarkAsRead : Endpoint<MarkAsReadRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/notifications/{Id}/read");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Notifications")
            .WithSummary("Mark notification as read"));
    }

    public override async Task HandleAsync(MarkAsReadRequest req, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
            return;
        }

        var userId = Guid.Parse(userIdClaim);

        await using var session = DocumentStore.LightweightSession();

        var notification = await session.LoadAsync<InAppNotification>(req.Id, ct);

        if (notification is null || notification.UserId != userId)
        {
            ThrowError("Notification not found", 404);
            return;
        }

        notification.IsRead = true;
        session.Update(notification);
        await session.SaveChangesAsync(ct);

        HttpContext.Response.StatusCode = 200;
    }
}
