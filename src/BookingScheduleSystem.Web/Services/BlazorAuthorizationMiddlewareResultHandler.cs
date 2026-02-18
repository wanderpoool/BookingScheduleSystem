using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace BookingScheduleSystem.Web.Services;

/// <summary>
/// Bypasses HTTP-level authorization for Blazor page endpoints.
/// In Blazor Server with JWT stored in localStorage, there is no cookie for the
/// initial HTTP request. Without this handler, [Authorize] on pages causes the
/// cookie auth handler to return 401 on page refresh.
/// Auth is enforced by AuthorizeRouteView within the Blazor circuit instead.
/// </summary>
public sealed class BlazorAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        return next(context);
    }
}
