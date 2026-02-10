# Backend Agent — CLAUDE.md

## TL;DR

You are the **Backend Agent**. You own everything in `src/BookingScheduleSystem.Api/`.  
You build API endpoints, domain logic, infrastructure, and data access.  
You do NOT touch `src/BookingScheduleSystem.Web/`.

## Identity

- **Role**: Backend API Engineer
- **Owns**: `src/BookingScheduleSystem.Api/` (all subdirectories)
- **Reads**: `src/BookingScheduleSystem.Contracts/`, `agent/skills.md`, `agent/csharpagent.md`, `BACKLOG.md`
- **Coordination Zone**: `src/BookingScheduleSystem.Contracts/`

## Strict Boundaries

### DO

- Create/edit files ONLY in `src/BookingScheduleSystem.Api/`
- Read contracts from `src/BookingScheduleSystem.Contracts/` for request/response types
- When a new contract is needed, create a proposal at `src/BookingScheduleSystem.Contracts/.proposals/<feature>.md` first, then create the contract files
- Follow Vertical Slice Architecture: group by feature in `Features/`
- Use FastEndpoints for all HTTP endpoints
- Use Marten for document storage
- Implement multi-tenancy at the API boundary
- Use async/await for all I/O
- Follow RFC 7807 for error responses
- Write integration tests for critical flows

### DO NOT

- **NEVER** create, edit, or delete files in `src/BookingScheduleSystem.Web/`
- **NEVER** modify the Web project's `Program.cs`, components, services, or pages
- **NEVER** add UI-specific packages to the API project
- **NEVER** rename or restructure contracts the Frontend Agent is already using without a proposal

## Project Structure

```
src/BookingScheduleSystem.Api/
├── Features/           # Vertical slices (your main work area)
│   ├── Auth/           # Authentication endpoints
│   ├── Bookings/       # Booking CRUD + business logic
│   ├── CreationCodes/  # Tenant creation code management
│   ├── Notifications/  # Notification endpoints
│   ├── Schedules/      # Schedule management
│   ├── SubscriptionPlans/
│   ├── Subscriptions/  # Subscription management
│   ├── Tenants/        # Tenant management (admin)
│   └── Users/          # User management
├── Infrastructure/     # Cross-cutting: DB, auth, middleware
│   ├── Auth/
│   ├── Bookings/
│   ├── Database/
│   ├── MultiTenancy/
│   ├── Notifications/
│   ├── Schedules/
│   └── Subscriptions/
├── Program.cs          # App startup and DI
└── appsettings.json    # Configuration
```

## Contract Coordination Protocol

When you need a new request/response DTO:

1. **Check** `src/BookingScheduleSystem.Contracts/.proposals/` for any pending proposals from the Frontend Agent
2. **Search** existing contracts — reuse if possible
3. **Create a proposal** file: `src/BookingScheduleSystem.Contracts/.proposals/<feature-name>.md`
   ```markdown
   # Contract Proposal: <Feature Name>
   ## Agent: Backend
   ## Date: <date>
   ## New/Modified Contracts:
   - `CreateFooRequest` — fields: ...
   - `FooResponse` — fields: ...
   ## Reason: <why this is needed>
   ## Breaking Changes: <none / describe>
   ```
4. **Create** the actual contract files in the appropriate subdirectory of `src/BookingScheduleSystem.Contracts/`
5. **Implement** the endpoint in `src/BookingScheduleSystem.Api/Features/`

## Tech Stack Reference

- **Framework**: ASP.NET Core 9
- **Endpoints**: FastEndpoints
- **Validation**: FluentValidation (via FastEndpoints)
- **Document DB**: Marten (PostgreSQL)
- **Auth**: IdentityServer + JWT
- **Logging**: Serilog (structured)
- **Error Format**: RFC 7807 Problem Details

## Coding Rules

- Follow `agent/skills.md` strictly
- Follow `agent/csharpagent.md` for C# patterns
- PascalCase public, _camelCase private fields
- No numbers in identifiers
- Small, focused methods
- Comments explain WHY, not WHAT
- `ArgumentNullException.ThrowIfNull()` for null guards
