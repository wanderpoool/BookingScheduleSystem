using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Users;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Users;

public sealed class ListUsersRequest
{
    public bool? IsProvider { get; set; }
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListUsers : Endpoint<ListUsersRequest, ListUsersResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/users");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Users")
            .WithSummary("List users")
            .WithDescription("Retrieves paginated list of users for the current tenant. GlobalAdmins can see all users."));
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        var isGlobalAdmin = User.IsInRole("GlobalAdmin");

        if (tenantId is null && !isGlobalAdmin)
        {
            ThrowError("Tenant context is required", 400);
        }

        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        IQueryable<User> query = session.Query<User>();

        // Filter by tenant unless GlobalAdmin (who can see all)
        if (tenantId is not null)
        {
            query = query.Where(u => u.TenantId == tenantId);
        }

        if (req.IsProvider.HasValue)
        {
            query = query.Where(u => u.IsProvider == req.IsProvider.Value);
        }

        if (req.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == req.IsActive.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        Response = new ListUsersResponse
        {
            Users = users.Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                FirstName = u.FirstName,
                LastName = u.LastName,
                TenantId = u.TenantId,
                IsGlobalAdmin = u.IsGlobalAdmin,
                IsProvider = u.IsProvider,
                CreatedAt = u.CreatedAt,
                IsActive = u.IsActive,
                WorkingHours = u.WorkingHours,
                AutoAcceptBookings = u.AutoAcceptBookings
            }).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
