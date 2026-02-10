# Agent Task Board — BookingScheduleSystem

## TL;DR

Consolidated task list for the Backend and Frontend agents, grouped by feature.  
Each feature has synchronized backend (API) and frontend (UI) tasks.  
Tasks are ordered by implementation phase (Phase 1 = Critical → Phase 4 = Future).

**Last Updated**: 2026-02-10

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Done |
| 🔧 | Partially done / has stubs |
| ⬜ | Not started |
| 🔴 | High priority |
| 🟡 | Medium priority |
| 🟢 | Low priority |

---

## What's Already Built (Completed)

### ✅ Auth (Login, Register, OTP)
- **API**: 5 endpoints — Login, LoginWithOtp, Register, SendOtp, VerifyOtp
- **Web**: 3 pages — Login, Register, RegisterOrganization
- **Services**: AuthenticationService, OtpService, CustomAuthenticationStateProvider
- **Contracts**: LoginRequest, RegisterUserRequest, SendOtpRequest, VerifyOtpRequest, LoginWithOtpRequest, AuthenticationResponse, OtpResponse

### ✅ Bookings (Consumer Flow)
- **API**: 6 endpoints — Create, Cancel, Get, List, Approve, Reject
- **Web**: 2 pages — BrowseSchedules, MyBookings + Dashboard (shows upcoming bookings)
- **Services**: BookingService (create, list, get, cancel)
- **Contracts**: CreateBookingRequest, CancelBookingRequest, BookingResponse

### ✅ Schedules (Public Browsing)
- **API**: 7 endpoints — Create, Get, List, ListPublic, Update, Delete + PublicSchedules
- **Web**: BrowseSchedules page consumes ListPublicSchedules
- **Services**: ScheduleService (listPublic only — ⚠️ missing CRUD methods)
- **Contracts**: CreateScheduleRequest, UpdateScheduleRequest, ScheduleResponse, PublicScheduleResponse

### ✅ Notifications (In-App)
- **API**: 4 endpoints — List, GetUnreadCount, MarkAsRead, MarkAllAsRead
- **Web**: NotificationBell component (polls every 30s)
- **Services**: NotificationService (all 4 methods)
- **Contracts**: NotificationResponse, ListNotificationsResponse, UnreadCountResponse

### ✅ Tenants (Basic CRUD)
- **API**: 4 endpoints — CreateTenant, CreateOrganization, GetTenant, ListTenants
- **Web**: RegisterOrganization page
- **Services**: TenantService (getTenant only — ⚠️ missing update/settings)
- **Contracts**: CreateTenantRequest, CreateOrganizationRequest, TenantResponse

### ✅ Subscription Plans (Admin CRUD)
- **API**: 5 endpoints — Create, Get, List, Update, SeedDefaults
- **Web**: Home page shows pricing tiers (static)
- **Contracts**: CreateSubscriptionPlanRequest, UpdateSubscriptionPlanRequest, SubscriptionPlanResponse, PlanLimitsDto

### ✅ Subscriptions (Basic)
- **API**: 3 endpoints — SubscribeToPlan, Cancel, GetCurrent
- **Contracts**: SubscribeToPlanRequest, CancelSubscriptionRequest, TenantSubscriptionResponse, UsageStatsDto

### ✅ Analytics (Admin)
- **API**: 3 endpoints — Revenue, Usage, ListAllSubscriptions
- **Contracts**: (inline responses in endpoints)

### ✅ Creation Codes
- **API**: 3 endpoints — Create, List, Validate
- **Web**: Registration uses validation during signup
- **Contracts**: CreateCreationCodeRequest, CreationCodeResponse

### ✅ Infrastructure
- **API**: Marten, JWT auth, multi-tenant middleware, trial validation, CORS, Serilog
- **Web**: MudBlazor, dark mode, responsive layout, auth state, HttpClient DI

---

## Pending Tasks — Grouped by Feature

---

### Feature 1: Schedule Management (Provider View) 🔴

> Providers need a full schedule management UI. API endpoints exist but the frontend has no CRUD pages.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 1.1 | **Backend** | Add `ListSchedules` response contract with pagination (if not in Contracts yet) | 🔴 | — | 🔧 ScheduleResponse exists, needs ListSchedulesResponse wrapper |
| 1.2 | **Frontend** | Add CRUD methods to `IScheduleService`/`ScheduleService` (create, update, delete, listForProvider) | 🔴 | 1.1 | ⬜ |
| 1.3 | **Frontend** | Build `/schedules` page — list provider's schedules with filters, edit/delete actions | 🔴 | 1.2 | ⬜ |
| 1.4 | **Frontend** | Build `/schedules/create` page — form for creating new schedule (title, time, capacity, provider) | 🔴 | 1.2 | ⬜ |
| 1.5 | **Frontend** | Build schedule edit dialog/page — reuse create form with pre-populated data | 🔴 | 1.3 | ⬜ |

---

### Feature 2: Booking Approval (Provider/Admin) 🔴

> Providers/admins can approve or reject pending bookings. API exists but no UI.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 2.1 | **Backend** | Add `ListBookings` response contract with pagination wrapper (if missing) | 🔴 | — | 🔧 BookingResponse exists |
| 2.2 | **Frontend** | Add approve/reject methods to `IBookingService`/`BookingService` | 🔴 | — | ⬜ |
| 2.3 | **Frontend** | Build booking management page — list pending bookings, approve/reject with reason dialog | 🔴 | 2.2 | ⬜ |
| 2.4 | **Frontend** | Build booking detail dialog — full booking info with action buttons | 🟡 | 2.3 | ⬜ |

---

### Feature 3: User & Provider Management 🔴

> Users feature directory is empty in API. Frontend has no user management pages.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 3.1 | **Backend** | Create contracts: `UserResponse`, `ListUsersResponse`, `UpdateUserRequest` | 🔴 | — | ⬜ |
| 3.2 | **Backend** | Implement `GET /api/users` — list users for tenant (paginated, filterable by role) | 🔴 | 3.1 | ⬜ |
| 3.3 | **Backend** | Implement `GET /api/users/{id}` — get user detail | 🔴 | 3.1 | ⬜ |
| 3.4 | **Backend** | Implement `PUT /api/users/{id}` — update user profile | 🟡 | 3.1 | ⬜ |
| 3.5 | **Backend** | Implement `POST /api/users/{id}/deactivate` — deactivate user | 🟡 | 3.1 | ⬜ |
| 3.6 | **Frontend** | Create `IUserService`/`UserService` with all user API methods | 🔴 | 3.1 | ⬜ |
| 3.7 | **Frontend** | Build `/providers` page — list providers with working hours, status | 🔴 | 3.6 | ⬜ |
| 3.8 | **Frontend** | Build `/providers/invite` page — generate creation codes for inviting providers | 🔴 | 3.6 | ⬜ |
| 3.9 | **Frontend** | Build `/profile` page — view/edit own profile | 🟡 | 3.6 | ⬜ |

---

### Feature 4: Organization Settings 🔴

> Tenants need to manage their org settings, operating hours, and branding. API partially exists.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 4.1 | **Backend** | Create contract: `UpdateTenantRequest` with fields for name, description, operatingHours, bannerUrl, location | 🔴 | — | ⬜ |
| 4.2 | **Backend** | Implement `PUT /api/tenants/{id}` — update tenant settings | 🔴 | 4.1 | ⬜ |
| 4.3 | **Frontend** | Add update methods to `ITenantService`/`TenantService` | 🔴 | 4.1 | ⬜ |
| 4.4 | **Frontend** | Build `/organization/settings` page — org name, subdomain, description, banner, location | 🔴 | 4.3 | ⬜ |
| 4.5 | **Frontend** | Build `/organization/operating-hours` page — weekly hours editor | 🔴 | 4.3 | ⬜ |

---

### Feature 5: Subscription Management UI 🔴

> API exists for subscribe/cancel/getCurrent. Frontend has no subscription management pages.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 5.1 | **Frontend** | Create `ISubscriptionService`/`SubscriptionService` (subscribe, cancel, getCurrent) | 🔴 | — | ⬜ |
| 5.2 | **Frontend** | Create `ISubscriptionPlanService`/`SubscriptionPlanService` (list, get plans) | 🔴 | — | ⬜ |
| 5.3 | **Frontend** | Build `/organization/subscription` page — current plan, usage stats, plan comparison, upgrade CTA | 🔴 | 5.1, 5.2 | ⬜ |
| 5.4 | **Frontend** | Build plan selection dialog — browse plans, compare features, subscribe | 🔴 | 5.2 | ⬜ |
| 5.5 | **Frontend** | Build cancel subscription dialog with reason input | 🟡 | 5.1 | ⬜ |

---

### Feature 6: Creation Code Management (Admin) 🟡

> API endpoints exist. Frontend has no management UI.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 6.1 | **Frontend** | Create `ICreationCodeService`/`CreationCodeService` (create, list, validate) | 🟡 | — | ⬜ |
| 6.2 | **Frontend** | Build creation codes management section — list codes, generate new, copy code, show usage stats | 🟡 | 6.1 | ⬜ |

---

### Feature 7: Analytics Dashboard 🟡

> API has 3 analytics endpoints. Frontend has no analytics pages.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 7.1 | **Frontend** | Create `IAnalyticsService`/`AnalyticsService` (revenue, usage, subscriptions) | 🟡 | — | ⬜ |
| 7.2 | **Frontend** | Build `/analytics` page — revenue charts (MRR, ARR), usage stats, subscription distribution | 🟡 | 7.1 | ⬜ |
| 7.3 | **Backend** | Implement `GET /api/analytics/churn-rate` | 🟡 | — | ⬜ |
| 7.4 | **Backend** | Implement `GET /api/analytics/ltv` (Customer Lifetime Value) | 🟡 | — | ⬜ |
| 7.5 | **Backend** | Implement `GET /api/analytics/growth-rate` | 🟡 | — | ⬜ |
| 7.6 | **Backend** | Implement `GET /api/analytics/trial-conversion-rate` | 🟡 | — | ⬜ |
| 7.7 | **Backend** | Create contracts: `ChurnRateResponse`, `LtvResponse`, `GrowthRateResponse`, `TrialConversionResponse` | 🟡 | — | ⬜ |
| 7.8 | **Frontend** | Add churn, LTV, growth, trial conversion charts to analytics page | 🟡 | 7.7 | ⬜ |

---

### Feature 8: Admin Panel 🟡

> Global admin needs overview pages. NavMenu links exist but no pages.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 8.1 | **Frontend** | Build `/admin/tenants` page — list all tenants with user counts, subscription status | 🟡 | — | ⬜ |
| 8.2 | **Frontend** | Build `/admin/tenants/{id}` page — tenant detail with users, subscription, usage | 🟡 | 8.1 | ⬜ |
| 8.3 | **Frontend** | Build `/admin/subscriptions` page — all subscriptions across tenants | 🟡 | — | ⬜ |
| 8.4 | **Frontend** | Build `/admin/creation-codes` page — manage creation codes across tenants | 🟡 | 6.1 | ⬜ |

---

### Feature 9: Auto-Expire Subscriptions Job � (Phase 4 — Deferred)

> Background job to expire subscriptions when EndDate passes. **Deferred** — not needed until production.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 9.1 | **Backend** | Add Hangfire or Quartz.NET to the project | 🟢 | — | ⬜ |
| 9.2 | **Backend** | Implement `SubscriptionExpirationJob` — scan active subs past EndDate, set Expired | 🟢 | 9.1 | ⬜ |
| 9.3 | **Backend** | Add expiration notification — create in-app notification when sub expires | 🟢 | 9.2 | ⬜ |
| 9.4 | **Frontend** | Display subscription expiration warnings on dashboard (banner when <7 days remaining) | 🟢 | 9.3 | ⬜ |

---

### Feature 10: Email Notification System � (Phase 4 — Deferred)

> Only in-app notifications exist. **Deferred** — keep using logger stubs for now. Real email/SMS integration later.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 10.1 | **Backend** | Integrate email service (SendGrid/Mailgun/AWS SES) with DI | 🟢 | — | ⬜ |
| 10.2 | **Backend** | Replace OTP stub with real email/SMS delivery in `OtpNotificationService` | 🟢 | 10.1 | 🔧 Stubbed — using logger for now |
| 10.3 | **Backend** | Create email templates (Razor) for: welcome, trial ending, booking confirmed, booking cancelled | 🟢 | 10.1 | ⬜ |
| 10.4 | **Backend** | Implement email queue/outbox for reliability | 🟢 | 10.1 | ⬜ |
| 10.5 | **Frontend** | Add notification preferences page (email on/off per event type) | 🟢 | 10.1 | ⬜ |

---

### Feature 11: Upgrade/Downgrade Subscriptions 🔴 (Phase 2)

> Allow tenants to change plans mid-cycle with proration.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 11.1 | **Backend** | Create contract: `ChangePlanRequest`, `ProrationPreviewResponse` | 🔴 | — | ⬜ |
| 11.2 | **Backend** | Implement `POST /api/subscriptions/change-plan` with proration logic | 🔴 | 11.1 | ⬜ |
| 11.3 | **Backend** | Implement `POST /api/subscriptions/preview-change` — preview proration without committing | 🔴 | 11.1 | ⬜ |
| 11.4 | **Frontend** | Add change-plan and preview methods to SubscriptionService | 🔴 | 11.1 | ⬜ |
| 11.5 | **Frontend** | Build plan upgrade/downgrade page — compare current vs new, proration preview, confirm | 🔴 | 11.4 | ⬜ |

---

### Feature 12: Usage Statistics Reset Job � (Phase 4 — Deferred)

> **Deferred** — depends on background job framework (Feature 9).

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 12.1 | **Backend** | Implement `UsageResetJob` — daily reset (BookingsToday, SchedulesToday) | 🟢 | 9.1 | ⬜ |
| 12.2 | **Backend** | Implement monthly reset (BookingsThisMonth, SchedulesThisMonth) | 🟢 | 9.1 | ⬜ |

---

### Feature 13: Additional Limit Enforcement 🟡 (Phase 2)

> Only booking limits are enforced. Missing: schedules, users, providers, branches.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 13.1 | **Backend** | Enforce `MaxSchedulesPerDay`/`MaxSchedulesPerMonth` in CreateSchedule endpoint | 🟡 | — | ⬜ |
| 13.2 | **Backend** | Enforce `MaxUsers` per tenant in Register endpoint | 🟡 | — | ⬜ |
| 13.3 | **Backend** | Enforce `MaxProviders` per tenant in Register (provider) endpoint | 🟡 | — | ⬜ |
| 13.4 | **Backend** | Enforce `MaxBranches` in relevant operations | 🟡 | — | ⬜ |
| 13.5 | **Frontend** | Show limit warnings in UI when approaching capacity (usage bar on subscription page) | 🟡 | 5.3 | ⬜ |

---

### Feature 14: Payment Gateway Integration � (Phase 4 — Deferred)

> No payment infrastructure exists. **Deferred** — subscriptions work without payments for now.

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 14.1 | **Backend** | Research & add GCash/Maya SDK package | 🟢 | — | ⬜ |
| 14.2 | **Backend** | Create contracts: `CreatePaymentRequest`, `PaymentResponse`, `WebhookPayload` | 🟢 | — | ⬜ |
| 14.3 | **Backend** | Implement `POST /api/payments/create` — initiate payment for subscription | 🟢 | 14.2 | ⬜ |
| 14.4 | **Backend** | Implement `POST /api/payments/webhook` — handle payment completion/failure callbacks | 🟢 | 14.2 | ⬜ |
| 14.5 | **Backend** | Implement payment status tracking and receipt generation | 🟢 | 14.3 | ⬜ |
| 14.6 | **Frontend** | Build payment flow page — payment method selection, confirmation, receipt | 🟢 | 14.2 | ⬜ |
| 14.7 | **Frontend** | Build billing history page — list past payments, download receipts | 🟢 | 14.5 | ⬜ |

---

### Feature 15: Subscription Renewal Management � (Phase 4 — Deferred)

> **Deferred** — depends on background jobs (Feature 9) and payments (Feature 14).

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 15.1 | **Backend** | Implement auto-renewal job (renew at EndDate if auto-renew=true) | 🟢 | 9.1, 14.3 | ⬜ |
| 15.2 | **Backend** | Implement grace period logic (3-day window after expiry) | 🟢 | 15.1 | ⬜ |
| 15.3 | **Backend** | Create contract: `RenewalSettingsRequest` (toggle auto-renew) | 🟢 | — | ⬜ |
| 15.4 | **Frontend** | Add auto-renewal toggle to subscription management page | 🟢 | 15.3 | ⬜ |

---

### Feature 16: Audit Logging 🟡 (Phase 3)

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 16.1 | **Backend** | Create `AuditLog` document model and Marten mapping | 🟡 | — | ⬜ |
| 16.2 | **Backend** | Create contracts: `AuditLogResponse`, `ListAuditLogsResponse` | 🟡 | — | ⬜ |
| 16.3 | **Backend** | Implement audit logging middleware/decorator for key actions | 🟡 | 16.1 | ⬜ |
| 16.4 | **Backend** | Implement `GET /api/admin/audit-logs` — paginated, filterable | 🟡 | 16.2 | ⬜ |
| 16.5 | **Frontend** | Build `/admin/audit-logs` page — searchable log viewer | 🟡 | 16.4 | ⬜ |

---

### Feature 17: Forgot Password 🟡

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 17.1 | **Backend** | Create contracts: `ForgotPasswordRequest`, `ResetPasswordRequest` | 🟡 | — | ⬜ |
| 17.2 | **Backend** | Implement `POST /api/auth/forgot-password` — send reset token (log token for now) | 🟡 | 17.1 | ⬜ |
| 17.3 | **Backend** | Implement `POST /api/auth/reset-password` — validate token, update password | 🟡 | 17.1 | ⬜ |
| 17.4 | **Frontend** | Build `/forgot-password` page — email input, send reset link | 🟡 | 17.1 | ⬜ |
| 17.5 | **Frontend** | Build `/reset-password` page — new password form with token validation | 🟡 | 17.3 | ⬜ |

---

### Feature 18: Cleanup & Polish 🟢

| # | Agent | Task | Priority | Depends On | Status |
|---|-------|------|----------|------------|--------|
| 18.1 | **Frontend** | Remove boilerplate pages (Counter.razor, Weather.razor) | 🟢 | — | ⬜ |
| 18.2 | **Frontend** | Build `/privacy` and `/terms` pages (linked from footer and registration) | 🟢 | — | ⬜ |
| 18.3 | **Frontend** | Implement theme manager (AppBar palette button is stubbed) | 🟢 | — | ⬜ |
| 18.4 | **Frontend** | Build `/settings` page — user preferences, theme, language | 🟢 | — | ⬜ |
| 18.5 | **Frontend** | Deduplicate theme definition between MainLayout and EmptyLayout | 🟢 | — | ⬜ |
| 18.6 | **Backend** | Clean up `.tmp` files in Features directories | 🟢 | — | ⬜ |
| 18.7 | **Backend** | Move OTP store from in-memory to Redis/distributed cache for production | 🟡 | — | 🔧 In-memory only |

---

## Phase Execution Summary

### Phase 1 — Critical (Start Here)

| Feature | Backend Tasks | Frontend Tasks |
|---------|--------------|----------------|
| **1. Schedule Management** | 1.1 (contract) | 1.2–1.5 (service + 3 pages) |
| **2. Booking Approval** | 2.1 (contract) | 2.2–2.4 (service + 2 pages) |
| **3. User/Provider Mgmt** | 3.1–3.5 (contracts + 4 endpoints) | 3.6–3.9 (service + 3 pages) |
| **4. Org Settings** | 4.1–4.2 (contract + endpoint) | 4.3–4.5 (service + 2 pages) |
| **5. Subscription UI** | — (API exists) | 5.1–5.5 (2 services + 3 pages) |


### Phase 2 — Important

| Feature | Backend Tasks | Frontend Tasks |
|---------|--------------|----------------|
| **11. Upgrade/Downgrade** | 11.1–11.3 (contracts + 2 endpoints) | 11.4–11.5 (service + page) |
| **13. Limit Enforcement** | 13.1–13.4 (4 checks) | 13.5 (usage bar) |

### Phase 3 — Nice to Have

| Feature | Backend Tasks | Frontend Tasks |
|---------|--------------|----------------|
| **6. Creation Code UI** | — (API exists) | 6.1–6.2 (service + page) |
| **7. Analytics Dashboard** | 7.3–7.7 (4 endpoints + contracts) | 7.1–7.2, 7.8 (service + charts) |
| **8. Admin Panel** | — (API exists) | 8.1–8.4 (4 pages) |
| **16. Audit Logging** | 16.1–16.4 (model + middleware + endpoint) | 16.5 (page) |
| **17. Forgot Password** | 17.1–17.3 (contracts + 2 endpoints) | 17.4–17.5 (2 pages) |

### Phase 4 — Future / Deferred

- **Feature 9**: Auto-Expire Subscriptions Job (use manual expiry for now)
- **Feature 10**: Email/SMS Service (keep using logger stubs)
- **Feature 12**: Usage Statistics Reset Job
- **Feature 14**: Payment Gateway Integration (GCash/Maya)
- **Feature 15**: Subscription Renewal Management
- Coupon & Discount Codes
- Free Trial Extensions
- Multi-Currency Support
- Tenant Billing Portal (extended)
- Add-Ons & Custom Features
- Referral Program

---

## How to Assign Tasks

Give each agent a feature group to work on. Example:

**Terminal 1 — Backend Agent:**
```
start-backend-agent.bat "Work on Feature 3: User & Provider Management. Implement tasks 3.1 through 3.5. Create the contracts first with a proposal in .proposals/, then implement the endpoints."
```

**Terminal 2 — Frontend Agent:**
```
start-frontend-agent.bat "Work on Feature 1: Schedule Management. Implement tasks 1.2 through 1.5. Read the existing contracts from BookingScheduleSystem.Contracts/Schedules/ and build the service and pages."
```

> **Tip**: Start the Frontend Agent on features where the API already exists (Features 1, 2, 5, 6, 8) while the Backend Agent builds new endpoints (Features 3, 4, 11, 13).
