using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Middleware that resolves the tenant from the request header
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private const string TenantHeaderName = "X-Tenant-Id";

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdValue) && !string.IsNullOrEmpty(tenantIdValue))
        {
            var tenantIdString = tenantIdValue.ToString();
            if (Guid.TryParse(tenantIdString, out var tenantGuid))
            {
                var tenantId = new TenantId(tenantGuid);
                tenantContext.SetTenant(tenantId);
                _logger.LogInformation("Resolved tenant {TenantId} from header", tenantId);
            }
            else
            {
                _logger.LogWarning("Invalid tenant ID format in header: {TenantIdValue}", tenantIdString);
            }
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
