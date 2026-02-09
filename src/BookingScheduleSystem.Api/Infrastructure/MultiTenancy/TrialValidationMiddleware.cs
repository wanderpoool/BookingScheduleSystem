using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

public sealed class TrialValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TrialValidationMiddleware> _logger;

    private static readonly HashSet<string> AllowedPathsWhenExpired = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/billing",
        "/api/subscriptions",
        "/api/auth/login",
        "/api/auth/register",
        "/api/tenants" // Allow viewing tenant info to see trial status
    };

    public TrialValidationMiddleware(RequestDelegate next, ILogger<TrialValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDocumentStore documentStore)
    {
        // Skip trial validation for anonymous endpoints or allowed paths
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        // Check if current path is allowed even when trial is expired
        var path = context.Request.Path.Value ?? string.Empty;
        if (AllowedPathsWhenExpired.Any(allowedPath => path.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Get user's tenant ID
        var tenantIdClaim = context.User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            // User has no tenant (global admin or unassociated user), allow through
            await _next(context);
            return;
        }

        // Load tenant and check trial status
        await using var session = documentStore.LightweightSession();
        var tenantId = TenantId.Parse(tenantIdClaim);
        var tenant = await session.LoadAsync<Tenant>(tenantId);

        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found for user {UserId}", tenantIdClaim, context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found" });
            return;
        }

        // Check if trial has expired
        if (tenant.IsInTrial && tenant.TrialEndDate.HasValue && tenant.TrialEndDate.Value < DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Trial expired for tenant {TenantId}. Trial ended at {TrialEndDate}",
                tenant.Id, tenant.TrialEndDate.Value);

            context.Response.StatusCode = 402; // Payment Required
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Trial period has expired",
                message = "Your trial period has ended. Please subscribe to continue using the service.",
                trialEndDate = tenant.TrialEndDate.Value,
                tenantId = tenant.Id
            });
            return;
        }

        // If not in trial, check for active subscription
        if (!tenant.IsInTrial)
        {
            var subscription = await session.Query<TenantSubscription>()
                .Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription is null)
            {
                _logger.LogWarning("No subscription found for tenant {TenantId}", tenant.Id);

                context.Response.StatusCode = 402; // Payment Required
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "No active subscription",
                    message = "Please subscribe to a plan to continue using the service.",
                    tenantId = tenant.Id
                });
                return;
            }

            // Check if subscription is active
            if (subscription.Status == SubscriptionStatus.Expired ||
                subscription.Status == SubscriptionStatus.Suspended ||
                (subscription.Status == SubscriptionStatus.Cancelled && subscription.EndDate.HasValue && subscription.EndDate.Value < DateTime.UtcNow))
            {
                _logger.LogWarning(
                    "Subscription {SubscriptionId} is {Status} for tenant {TenantId}",
                    subscription.Id, subscription.Status, tenant.Id);

                context.Response.StatusCode = 402; // Payment Required
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Subscription not active",
                    message = subscription.Status == SubscriptionStatus.Cancelled
                        ? "Your subscription has been cancelled and expired."
                        : $"Your subscription is {subscription.Status.ToString().ToLower()}. Please renew to continue.",
                    status = subscription.Status.ToString(),
                    endDate = subscription.EndDate,
                    tenantId = tenant.Id
                });
                return;
            }

            // Check if subscription has ended
            if (subscription.EndDate.HasValue && subscription.EndDate.Value < DateTime.UtcNow)
            {
                _logger.LogWarning(
                    "Subscription {SubscriptionId} expired on {EndDate} for tenant {TenantId}",
                    subscription.Id, subscription.EndDate.Value, tenant.Id);

                context.Response.StatusCode = 402; // Payment Required
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Subscription expired",
                    message = "Your subscription has expired. Please renew to continue using the service.",
                    endDate = subscription.EndDate.Value,
                    tenantId = tenant.Id
                });
                return;
            }
        }

        await _next(context);
    }
}

public static class TrialValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseTrialValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TrialValidationMiddleware>();
    }
}
