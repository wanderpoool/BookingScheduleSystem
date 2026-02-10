using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Contracts.Notifications;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Notifications;

public sealed class GetUnreadCount : EndpointWithoutRequest<UnreadCountResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/notifications/unread-count");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Notifications")
            .WithSummary("Get unread notification count"));
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

        var count = await session.Query<InAppNotification>()
            .CountAsync(n => n.UserId == userId && n.TenantId == tenantId!.Value.Value && !n.IsRead, ct);

        Response = new UnreadCountResponse { Count = (int)count };
    }
}
