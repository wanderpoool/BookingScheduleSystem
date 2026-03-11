# Timezone Fix Required: BookingReminderJob

## Problem Summary

The `BookingReminderJob` is sending reminders at the wrong time due to timezone mismatch.

### Example Issue
- User's appointment: **3:00 PM Philippines time** (stored as `15:00` in DB)
- Reminder received at: **2:30 AM Philippines time** (12.5 hours early!)
- Expected: Reminder at **2:00 PM Philippines time** (1 hour before)

## Root Cause

**File:** `src/BookingScheduleSystem.Api/Infrastructure/BackgroundJobs/BookingReminderJob.cs`

**Line 44:**
```csharp
var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
```

This uses UTC time to compare against schedules that are stored in **Philippines local time** (UTC+8).

### What's Happening:
1. Schedules are stored as `timestamp without time zone` in local Philippines time
2. Job uses `DateTime.UtcNow` (e.g., `18:30 UTC` = `2:30 AM Philippines next day`)
3. Comparison: `schedule.StartTime (15:00 local) vs now (18:30 UTC)` — mismatched timezones!
4. Job incorrectly triggers reminders ~12.5 hours early

## Solution

Update `BookingReminderJob.cs` to convert UTC to Philippines time before comparing:

```csharp
// Line 44 — replace with Philippines timezone conversion
var philippinesZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); // UTC+8
var nowPhilippines = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippinesZone);
var now = DateTime.SpecifyKind(nowPhilippines, DateTimeKind.Unspecified);
var windowEnd = now.AddMinutes(_leadTimeMinutes);
```

### Better Long-Term Solution

Add timezone to Tenant document and use tenant-specific timezone:

1. Add `TimeZone` property to `Tenant.cs` (default: `"Singapore Standard Time"`)
2. Load tenant timezone in the job
3. Convert UTC to tenant's timezone before comparison

This makes the system multi-timezone capable.

## Files to Change

- ✅ **Frontend (FIXED):** All calendar components now use `DateTimeKind.Unspecified`
- ❌ **Backend (NEEDS FIX):** `BookingReminderJob.cs` line 44

## Testing

After fix, verify:
1. Create schedule for 3:00 PM today
2. Wait until 2:00 PM (1 hour before)
3. Check that reminder is sent at 2:00 PM (not 2:30 AM!)

## Priority

**CRITICAL** — Users are receiving reminders at incorrect times, causing confusion and poor UX.
