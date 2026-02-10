# Contract Proposal: TimeBlock BookingId and BookingStatus

## Agent: Frontend
## Date: 2026-02-10

## New/Modified Contracts:
- `TimeBlock` (in `Schedules/ProviderAvailabilityResponse.cs`) — Added two optional fields:
  - `BookingId? BookingId` — The ID of the booking occupying this time block (null if available)
  - `BookingStatus? BookingStatus` — The status of the booking (Pending, Confirmed, etc.)

## Reason:
The provider booking approval workflow needs to know the BookingId for each booked time block so the UI can call the approve/reject endpoints. BookingStatus is needed to show pending vs confirmed visual state on the calendar.

## Breaking Changes: None
Both fields are optional (nullable) so existing API responses will continue to work. The backend `ListPublicSchedules` endpoint should be updated to populate these fields from the Booking document when a time block is booked.

## Fallback:
Until the backend populates these fields, the frontend will call `ListBookingsAsync(status: Pending)` to fetch pending bookings and match them to calendar items by ScheduleId.
