# Contract Proposal: Queue Management

## Agent: Frontend
## Date: 2026-03-18

## New Contracts:

### Strongly-typed IDs
- `QueueId(Guid Value)` — daily queue per tenant
- `QueueEntryId(Guid Value)` — individual customer in queue

### Enums
- `QueueEntryStatus` — Waiting, Called, InService, Completed, Skipped, NoShow

### Request/Response DTOs
- `QueueInfoResponse` — Public: tenant name, queue date, current count (for join page display before auth)
- `JoinQueueRequest` — DailyToken, PhoneNumber, CustomerName?
- `JoinQueueResponse` — EntryId, QueueNumber, PositionInQueue, EstimatedWaitMinutes
- `QueueEntryResponse` — Full entry: id, queueNumber, customerName, phone, status, isPriority, estimatedWaitMinutes, timestamps
- `GetTodayQueueResponse` — DailyToken, List<QueueEntryResponse>, TotalWaiting, TotalCalled, TotalCompleted
- `AddQueueEntryRequest` — Provider manual add: PhoneNumber, CustomerName, IsPriority
- `UpdateQueueEntryStatusRequest` — Status (enum value)
- `ReorderQueueEntryRequest` — NewPosition (1-based)

### Modified Contracts
- `UpdateTenantRequest` — add: QueueEnabled?, QueueAverageServiceTimeMinutes?, QueueNotificationLeadMinutes?
- `TenantResponse` — add: QueueEnabled, QueueAverageServiceTimeMinutes (default 15), QueueNotificationLeadMinutes (default 15)
- `UpdateUserRequest` — add: IsPriority?
- `UserResponse` — add: IsPriority

## Reason:
The frontend needs these contracts to implement the queue management feature — customer-facing join/status pages and provider queue management dashboard.

## Breaking Changes:
None — all additions are new fields with sensible defaults (false/15) or new DTOs.

## Backend Endpoints Required:
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/queue/today` | Provider | Get-or-create today's queue, returns QueueResponse with daily token |
| GET | `/api/queue/today/entries` | Provider | List all entries for today's queue |
| GET | `/api/queue/info?token={token}` | Public | Get tenant name + queue stats for the join page |
| POST | `/api/queue/join` | Public | Customer joins queue with phone + token |
| GET | `/api/queue/entries/{id}` | Public | Check queue entry status + ETA |
| POST | `/api/queue/entries` | Provider | Manually add customer to queue |
| PATCH | `/api/queue/entries/{id}/status` | Provider | Update status (Call, InService, Complete, Skip, NoShow) |
| PATCH | `/api/queue/entries/{id}/reorder` | Provider | Change position in queue |

## Background Job:
`QueueNotificationJob` — runs every 5 min, checks waiting entries where ETA <= notificationLeadMinutes, sends SMS via SemaphoreSmsService, deduplicates with QueueNotificationSent document.
