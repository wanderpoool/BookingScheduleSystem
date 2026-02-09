using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.CreationCodes;

public sealed class ValidateCreationCodeRequest
{
    public required string Code { get; set; }
}

public sealed class ValidateCreationCodeResponse
{
    public required bool IsValid { get; init; }
    public TenantId? TenantId { get; init; }
    public string? Message { get; init; }
}

public sealed class ValidateCreationCode : Endpoint<ValidateCreationCodeRequest, ValidateCreationCodeResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/creation-codes/validate");
        AllowAnonymous();
        Description(d => d
            .WithTags("CreationCodes")
            .WithSummary("Validate a creation code")
            .WithDescription("Checks if a creation code is valid and can be used for registration."));
    }

    public override async Task HandleAsync(ValidateCreationCodeRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var code = await session.Query<CreationCode>()
            .FirstOrDefaultAsync(c => c.Code == req.Code, token: ct);

        if (code is null)
        {
            Response = new ValidateCreationCodeResponse
            {
                IsValid = false,
                Message = "Invalid creation code"
            };
            return;
        }

        if (!code.IsActive)
        {
            Response = new ValidateCreationCodeResponse
            {
                IsValid = false,
                Message = "Creation code is no longer active"
            };
            return;
        }

        if (code.CurrentUses >= code.MaxUses)
        {
            Response = new ValidateCreationCodeResponse
            {
                IsValid = false,
                Message = "Creation code has reached maximum uses"
            };
            return;
        }

        if (code.ExpiresAt.HasValue && code.ExpiresAt.Value < DateTime.UtcNow)
        {
            Response = new ValidateCreationCodeResponse
            {
                IsValid = false,
                Message = "Creation code has expired"
            };
            return;
        }

        Response = new ValidateCreationCodeResponse
        {
            IsValid = true,
            TenantId = code.TenantId,
            Message = "Valid creation code"
        };
    }
}
