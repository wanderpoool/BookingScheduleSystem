# Contract Proposal: Auto-Accept Bookings & Magic Link Actions
## Agent: Backend
## Date: 2026-02-19
## New/Modified Contracts:
- `UserResponse` — added `bool AutoAcceptBookings` field
- `UpdateUserRequest` — added `bool? AutoAcceptBookings` field (nullable for partial update)
## Reason: Providers need an auto-accept setting. When enabled, bookings skip Pending and go straight to Confirmed. Magic link emails use HMAC tokens (no contract changes needed — returns HTML pages, not JSON).
## Breaking Changes: None — new fields only, backward compatible
