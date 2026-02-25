# Contract Proposal: Landing Page Template

## Agent: Frontend
## Date: 2026-02-26

## New/Modified Contracts:
- `TenantResponse` — add `string? LandingPageTemplate` field (values: "simple", "premium")
- `UpdateTenantRequest` — add `string? LandingPageTemplate` field

## Reason:
Provider landing pages at `/{slug}` need a selectable template. Providers choose between "simple" (minimal) and "premium" (rich marketing page) in Organization Settings. The backend Tenant document needs to persist this choice.

## Breaking Changes: None
Both fields are nullable with "simple" as the default fallback when null.
