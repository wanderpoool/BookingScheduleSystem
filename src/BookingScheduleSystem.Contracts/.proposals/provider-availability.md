# Contract Proposal: Provider Availability
## Agent: Backend
## Date: 2026-02-10
## New/Modified Contracts:
- `ProviderAvailabilityResponse` — per-provider availability with days and time blocks
- `DayAvailability` — per-day working hours and time blocks
- `TimeBlock` — start/end time, availability flag, optional schedule reference
- `ListPublicSchedulesResponse.Schedules` → `Providers` (List<ProviderAvailabilityResponse>)
- `ListSchedulesResponse.Schedules` → `Providers` (List<ProviderAvailabilityResponse>)
## Reason: Endpoints need to return provider-centric availability blocks instead of flat schedule lists
## Breaking Changes: ListPublicSchedulesResponse and ListSchedulesResponse property renamed from Schedules to Providers with new type
