# Contract Proposal: Schedule Availability Blocks
## Agent: Frontend
## Date: 2026-02-10
## New/Modified Contracts:
- `PublicScheduleResponse` — add `bool IsAvailabilityBlock { get; init; }` (default `false`)

## Reason:
The API is being updated to return provider availability blocks (open time slots with no existing schedule)
alongside real schedules. The frontend needs a way to distinguish between:
- **Real schedules** (`IsAvailabilityBlock = false`) — bookable slots with Title, Capacity, etc.
- **Availability blocks** (`IsAvailabilityBlock = true`) — open time during working hours, shown as green on the calendar

## How the API should populate availability blocks:
- `IsAvailabilityBlock = true`
- `Title = "Available"` (or provider name + "Available")
- `StartTime` / `EndTime` = the available time range
- `MaxCapacity` / `CurrentBookings` = provider's default capacity / 0
- `ProviderId` / `ProviderName` = the provider who is available
- `Id` = can be a generated/empty ScheduleId (not persisted)

## Frontend behavior:
- Green CellTemplate for availability blocks, green/red for real schedules based on capacity
- Clicking availability block shows info dialog ("time is open, no schedules posted")
- Clicking real schedule opens provider selection dialog for booking

## Breaking Changes: None — new field with default value `false`, fully backwards compatible
