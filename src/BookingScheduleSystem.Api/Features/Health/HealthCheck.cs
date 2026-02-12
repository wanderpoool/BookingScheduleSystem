using FastEndpoints;

namespace BookingScheduleSystem.Api.Features.Health;

public sealed class HealthCheckEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/health");
        AllowAnonymous();
        Description(d => d
            .WithTags("Health")
            .WithSummary("Health check endpoint for ALB"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.StatusCode = 200;
        return Task.CompletedTask;
    }
}
