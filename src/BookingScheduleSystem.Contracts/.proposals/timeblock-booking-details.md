# Contract Proposal: TimeBlock Booking Details
## Agent: Frontend
## Date: 2026-02-10
## Modified Contracts:
- `TimeBlock` — add optional `CustomerName` (string?) and `BookingNotes` (string?) fields
## Reason: Provider calendar view needs to show the customer name and booking notes for booked time slots. Currently TimeBlock only has schedule-level data (ScheduleTitle, ScheduleId) but no booking-level details.
## Breaking Changes: None — new fields are optional (nullable), backward compatible
