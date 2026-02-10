# Contract Proposal: Organization Settings

## Agent: Backend
## Date: 2026-02-10

## New Contracts:
- `UpdateTenantRequest` — string Name, string? Description, string? OperatingHours, string? BannerUrl, string? Location

## Reason: Frontend needs an API to update tenant/organization settings (name, description, operating hours, banner, location) from the org settings page.

## Breaking Changes: None — new contract only. Reuses existing `TenantResponse` for the response.
