using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Contracts.Notifications;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Notifications;

public sealed class ListNotificationsRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class ListNotifications : Endpoint<ListNotificationsRequest, ListNotificationsResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/notifications");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Notifications")
            .WithSummary("List notifications")
            .WithDescription("Retrieves paginated list of notifications for the current user."));
    }

    public override async Task HandleAsync(ListNotificationsRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
            return;
        }

        var userId = Guid.Parse(userIdClaim);
        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 50);

        await using var session = DocumentStore.LightweightSession();

        var query = session.Query<InAppNotification>()
            .Where(n => n.UserId == userId && n.TenantId == tenantId!.Value.Value);

        var totalCount = await query.CountAsync(ct);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        Response = new ListNotificationsResponse
        {
            Notifications = notifications.Select(n => new NotificationResponse
            {
                Id = n.Id,
                UserId = n.UserId,
                TenantId = n.TenantId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                RelatedEntityId = n.RelatedEntityId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
