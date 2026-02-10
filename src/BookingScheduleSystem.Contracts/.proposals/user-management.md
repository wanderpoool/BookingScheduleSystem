# Contract Proposal: User Management

## Agent: Backend
## Date: 2026-02-10

## New Contracts:
- `UserResponse` — UserId Id, string Email, string? PhoneNumber, string FirstName, string LastName, TenantId? TenantId, bool IsGlobalAdmin, bool IsProvider, DateTime CreatedAt, bool IsActive, string? WorkingHours
- `ListUsersResponse` — List<UserResponse> Users, int TotalCount, int PageNumber, int PageSize
- `UpdateUserRequest` — string? FirstName, string? LastName, string? PhoneNumber, string? WorkingHours

## Reason: Frontend needs user management APIs (list providers, view/edit user profiles, deactivate users) for the provider management page.

## Breaking Changes: None — all new contracts.
