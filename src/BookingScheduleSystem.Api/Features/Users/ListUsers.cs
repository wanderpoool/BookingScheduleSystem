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

        // If tenant-scoped, resolve member UserIds via UserTenant then filter in-memory
        if (tenantId is not null)
        {
            var memberships = await session.Query<UserTenant>()
                .Where(ut => ut.TenantId == tenantId && ut.IsActive)
                .ToListAsync(ct);
            var memberUserIds = memberships.Select(ut => ut.UserId).ToHashSet();

            var allUsers = await session.Query<User>()
                .ToListAsync(ct);
            var tenantUsers = allUsers.Where(u => memberUserIds.Contains(u.Id)).AsEnumerable();

            if (req.IsProvider.HasValue)
                tenantUsers = tenantUsers.Where(u => u.IsProvider == req.IsProvider.Value);

            if (req.IsActive.HasValue)
                tenantUsers = tenantUsers.Where(u => u.IsActive == req.IsActive.Value);

            var filteredList = tenantUsers.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
            var totalCount = filteredList.Count;
            var pagedUsers = filteredList.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            Response = new ListUsersResponse
            {
                Users = pagedUsers.Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    TenantId = tenantId,
                    IsGlobalAdmin = u.IsGlobalAdmin,
                    IsProvider = u.IsProvider,
                    CreatedAt = u.CreatedAt,
                    IsActive = u.IsActive,
                    WorkingHours = u.WorkingHours,
                    AutoAcceptBookings = u.AutoAcceptBookings,
                    IsPriority = u.IsPriority
                }).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
            return;
        }

        // GlobalAdmin: show all users
        IQueryable<User> query = session.Query<User>();

        if (req.IsProvider.HasValue)
        {
            query = query.Where(u => u.IsProvider == req.IsProvider.Value);
        }

        if (req.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == req.IsActive.Value);
        }

        var totalGlobalCount = await query.CountAsync(ct);

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
                AutoAcceptBookings = u.AutoAcceptBookings,
                IsPriority = u.IsPriority
            }).ToList(),
            TotalCount = (int)totalGlobalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
