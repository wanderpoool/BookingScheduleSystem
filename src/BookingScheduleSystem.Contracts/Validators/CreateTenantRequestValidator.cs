using BookingScheduleSystem.Contracts.Tenants;
using FluentValidation;

namespace BookingScheduleSystem.Contracts.Validators;

public sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Tenant name is required")
            .MaximumLength(200)
            .WithMessage("Tenant name must not exceed 200 characters");

        RuleFor(x => x.Subdomain)
            .NotEmpty()
            .WithMessage("Subdomain is required")
            .MaximumLength(63)
            .WithMessage("Subdomain must not exceed 63 characters")
            .Matches("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$")
            .WithMessage("Subdomain must contain only lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
