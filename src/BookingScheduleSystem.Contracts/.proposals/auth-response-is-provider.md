# Contract Proposal: AuthenticationResponse — IsProvider Flag

## Agent: Frontend
## Date: 2026-02-10

## Modified Contracts
- `AuthenticationResponse` — added `public bool IsProvider { get; init; }` (non-required, defaults to `false`)

## Reason
The frontend needs to distinguish between **customers** and **providers** to show role-appropriate navigation and dashboard content:
- Customers see a minimal nav (My Bookings, Book a Schedule, Profile) and a simplified dashboard
- Providers/GlobalAdmins see the full nav (Schedules, Providers, Organization, Analytics, Settings)

## Backend Action Required
In the login and OTP verification endpoints, populate `IsProvider` from the `User` document's `IsProvider` property when constructing the `AuthenticationResponse`.

## Breaking Changes
None — `IsProvider` is non-required and defaults to `false`, so all existing users will be treated as customers until the backend populates the field.
