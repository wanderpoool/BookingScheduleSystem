# Contract Proposal: Tenant Subdomain Lookup

## Agent: Frontend
## Date: 2026-02-13

## New/Modified Contracts:
- No new contracts needed — reuses existing `TenantResponse`

## New API Endpoint Required:
- `GET /api/tenants/by-subdomain/{subdomain}` -> returns `TenantResponse`
- This endpoint should be **public** (no auth required) so unauthenticated users can resolve tenant context from slug URLs like `/{slug}/register`

## Reason:
The frontend is implementing slug-based URL routing (`/{slug}/page`) to replace opaque GUID-based URLs (`?company={guid}`). When an unauthenticated user visits `/{slug}/register` or `/{slug}/login`, the frontend needs to resolve the slug (which maps to `TenantResponse.Subdomain`) to get the tenant's ID and name for display.

## Breaking Changes: None
- Existing `?company={guid}` query parameter support is preserved as fallback
- `TenantResponse.Subdomain` field already exists and is populated during org creation
