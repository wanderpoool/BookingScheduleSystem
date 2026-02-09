using System.Security.Claims;
using System.Security.Cryptography;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.CreationCodes;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.CreationCodes;

public sealed class CreateCreationCode : Endpoint<CreateCreationCodeRequest, CreationCodeResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/creation-codes");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("CreationCodes")
            .WithSummary("Create a tenant-scoped creation code")
            .WithDescription("Generates a secure creation code for user onboarding. Admin only."));
    }

    public override async Task HandleAsync(CreateCreationCodeRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Verify tenant exists
        var tenant = await session.LoadAsync<Tenant>(req.TenantId, ct);
        if (tenant is null)
        {
            ThrowError("Tenant not found", 404);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        var creationCode = new CreationCode
        {
            Id = CreationCodeId.New(),
            TenantId = req.TenantId,
            Code = GenerateSecureCode(),
            MaxUses = Math.Max(1, req.MaxUses),
            CurrentUses = 0,
            ExpiresAt = req.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(req.ExpiresInDays.Value)
                : null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserId.Parse(userIdClaim),
            IsActive = true,
            IsProviderCode = req.IsProviderCode
        };

        session.Store(creationCode);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Created creation code {CodeId} for tenant {TenantId} by user {UserId}",
            creationCode.Id, creationCode.TenantId, creationCode.CreatedBy);

        Response = new CreationCodeResponse
        {
            Id = creationCode.Id,
            TenantId = creationCode.TenantId,
            Code = creationCode.Code,
            MaxUses = creationCode.MaxUses,
            CurrentUses = creationCode.CurrentUses,
            ExpiresAt = creationCode.ExpiresAt,
            CreatedAt = creationCode.CreatedAt,
            IsActive = creationCode.IsActive,
            IsProviderCode = creationCode.IsProviderCode
        };
    }

    private static string GenerateSecureCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new char[12];
        var randomBytes = RandomNumberGenerator.GetBytes(12);

        for (int i = 0; i < 12; i++)
        {
            code[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(code).Insert(4, "-").Insert(9, "-");
    }
}
