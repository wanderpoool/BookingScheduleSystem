# Contract Proposal: Booking Reminder Notifications

## Agent: Backend
## Date: 2026-03-10

## New/Modified Contracts:
- `NotificationType.BookingReminder` — new enum value added to existing `NotificationType` enum

## Reason:
Background job sends reminder notifications ~1 hour before confirmed appointments. Needs a distinct notification type so the frontend can display reminders differently from booking event notifications.

## Breaking Changes: None (additive enum value only)
