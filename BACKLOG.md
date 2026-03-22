# Subscription System - Feature Backlog

## Overview
Features that have been identified but deferred for future implementation.

---

## 🔄 Background Jobs & Automation

### 1. **Auto-Expire Subscriptions Job**
**Priority**: High
**Effort**: Medium

**Description**:
Background job that runs periodically (e.g., hourly or daily) to automatically update subscription statuses when they expire.

**Requirements**:
- Scan all subscriptions where `EndDate < DateTime.UtcNow` and `Status == Active`
- Update `Status` to `Expired`
- Optional: Send email notifications to tenants before expiration (7 days, 3 days, 1 day)
- Log all status changes for audit trail

**Technical Notes**:
- Consider using Hangfire or Quartz.NET for scheduling
- Ensure idempotency (handle duplicate runs gracefully)

---

### 2. **Usage Statistics Reset Job**
**Priority**: Medium
**Effort**: Low

**Description**:
Reset daily and monthly usage counters at appropriate intervals.

**Requirements**:
- **Daily Reset**: Run at midnight UTC to reset `BookingsToday` and `SchedulesToday` counters
- **Monthly Reset**: Run on the 1st of each month to reset `BookingsThisMonth` and `SchedulesThisMonth` counters
- Update `UsageResetDate` field after each reset
- Log all resets for debugging

**Technical Notes**:
- Can be combined with the auto-expire job
- Consider timezone implications for global usage

---

## 💳 Payment & Billing Integration

### 3. **Payment Gateway Integration**
**Priority**: High
**Effort**: High

**Description**:
Integrate with a payment provider for actual subscription payments.

**Options to Consider**:
- GCASH
- MAYA

**Requirements**:
- Create payment endpoint for subscribing to plans
- Handle webhook events from payment provider
- Store payment method details (tokenized)
- Support subscription billing cycles (monthly/yearly)
- Handle failed payments (retry logic, grace period)
- Generate invoices and receipts
- Support payment method updates
- Implement dunning management (failed payment recovery)

**Technical Notes**:
- Use official SDKs (Stripe.NET, PayPal.NET, etc.)
- Secure webhook endpoints with signature verification
- Implement PCI compliance if storing card data (prefer tokenization)

---

## 🔄 Subscription Management

### 4. **Upgrade/Downgrade Subscriptions**
**Priority**: High
**Effort**: Medium

**Description**:
Allow tenants to change their subscription plan mid-cycle with prorating.

**Requirements**:
- Create `POST /api/subscriptions/change-plan` endpoint
- Calculate prorated charges/credits
- Handle immediate vs. end-of-cycle changes
- Preserve usage stats during plan changes
- Update limits immediately after upgrade
- Notify tenant of changes via email

**Prorating Logic**:
- Upgrade: Charge difference for remaining days
- Downgrade: Credit difference or apply to next billing cycle

**Technical Notes**:
- Consider using payment provider's proration features (Stripe handles this well)
- Document proration calculations for customer support

---

### 5. **Subscription Renewal Management**
**Priority**: Medium
**Effort**: Medium

**Description**:
Handle automatic renewals and manual renewal flows.

**Requirements**:
- Auto-renew active subscriptions at `EndDate`
- Send renewal confirmations via email
- Allow manual renewal before expiration
- Handle renewable grace period (e.g., 3 days after expiry)
- Support turning off auto-renewal (subscription continues until `EndDate`)

---

## 📊 Enhanced Analytics & Reporting

### 6. **Subscription Analytics Dashboard**
**Priority**: Medium
**Effort**: Medium

**Description**:
Additional analytics endpoints and charts for admin dashboard.

**New Metrics**:
- **Churn Rate**: Monthly percentage of cancelled subscriptions
- **Customer Lifetime Value (LTV)**: Average revenue per customer over their lifetime
- **Revenue Growth Rate**: Month-over-month growth
- **Trial Conversion Rate**: Percentage of trials that convert to paid
- **Average Revenue Per User (ARPU)**: Total revenue / active subscriptions
- **Plan Distribution**: Pie chart of subscribers per plan

**Endpoints to Create**:
- `GET /api/analytics/churn-rate`
- `GET /api/analytics/ltv`
- `GET /api/analytics/growth-rate`
- `GET /api/analytics/trial-conversion-rate`

---

## 🎁 Promotional Features

### 7. **Coupon & Discount Codes**
**Priority**: Low
**Effort**: Medium

**Description**:
Support promotional codes for discounts on subscriptions.

**Requirements**:
- Create `Coupon` domain model (code, discount %, expiry, max uses)
- Apply coupons at subscription creation
- Support percentage and fixed-amount discounts
- Track coupon usage and redemptions
- Admin endpoints for CRUD operations on coupons

---

### 8. **Free Trial Extensions**
**Priority**: Low
**Effort**: Low

**Description**:
Allow admins to extend trial periods for specific tenants.

**Requirements**:
- Create `POST /api/admin/extend-trial/{tenantId}` endpoint
- Accept `extensionDays` parameter
- Update `TrialEndDate` accordingly
- Log all extensions for audit
- Optional: Auto-notify tenant of extension

---

## 🔐 Security & Compliance

### 9. **Audit Logging**
**Priority**: Medium
**Effort**: Medium

**Description**:
Comprehensive audit trail for all subscription-related actions.

**Events to Log**:
- Subscription created
- Subscription cancelled
- Subscription expired
- Plan changed
- Payment succeeded/failed
- Trial extended
- Limits exceeded

**Requirements**:
- Create `AuditLog` domain model
- Store user, action, timestamp, before/after state
- Admin endpoint to view audit logs
- Filter by tenant, date range, action type

---

## 📧 Notifications

### 10. **Email Notification System**
**Priority**: High
**Effort**: Medium

**Description**:
Send email notifications for subscription events.

**Notifications Needed**:
- Welcome email on registration
- Trial ending soon (7, 3, 1 day before)
- Trial expired
- Subscription confirmation
- Payment succeeded
- Payment failed
- Subscription cancelled
- Subscription renewed

**Technical Notes**:
- Use email service (SendGrid, AWS SES, Mailgun)
- Create email templates (Razor, Handlebars)
- Implement email queue for reliability

---

## 🎯 Usage Enforcement

### 11. **Additional Limit Enforcement**
**Priority**: Medium
**Effort**: Medium

**Description**:
Enforce other plan limits beyond bookings.

**Limits to Enforce**:
- Max users per tenant
- Max providers per tenant
- Max schedules per day/month
- Max concurrent bookings
- Max branches (for Multi-Branch plan)

**Requirements**:
- Update user registration to check `MaxUsers` limit
- Update provider creation to check `MaxProviders` limit
- Update schedule creation to check schedule limits
- Return 403 with clear error messages when limits exceeded

---

## 🌍 Multi-Currency Support

### 12. **International Pricing**
**Priority**: Low
**Effort**: High

**Description**:
Support multiple currencies for global customers.

**Requirements**:
- Add `Currency` field to `SubscriptionPlan` (USD, EUR, GBP, etc.)
- Store prices in all supported currencies
- Detect user's location and show appropriate currency
- Handle currency conversion for reporting
- Support payment in local currency

---

## 📱 Self-Service Portal

### 13. **Tenant Billing Portal**
**Priority**: Medium
**Effort**: High

**Description**:
Web portal for tenants to manage their subscriptions.

**Features**:
- View current subscription and usage
- Upgrade/downgrade plan
- Update payment method
- View billing history and invoices
- Download receipts
- Cancel subscription
- View usage graphs

---

## 👥 Multi-Tenant Customer Experience

### 16. **Multi-Tenant Customer Accounts**
**Priority**: High
**Effort**: High

**Description**:
Allow a single Customer account to be associated with multiple tenants (organizations). When a customer registers via an invitation link for a new tenant, link their existing account to that tenant instead of requiring a new account. On login, present a tenant selector so the customer can choose which provider/organization to book with, setting it as the active tenant for the session.

**Requirements**:
- Support many-to-many relationship between Customer accounts and Tenants
- When a customer registers for a new tenant and their email already exists, link the existing account to the new tenant
- After login, if the customer belongs to multiple tenants, show a dropdown/selector to choose the active tenant
- Store the selected tenant as the current session tenant (X-Tenant-Id header)
- Allow switching tenants without re-logging in
- Dashboard and booking flows use the currently selected tenant context
- Persist last-used tenant preference for convenience

**Technical Notes**:
- Requires new `UserTenant` join table/document (UserId + TenantId + JoinedAt)
- Update auth flow: after JWT validation, check tenant memberships
- Add tenant switcher component in MainLayout (visible only for multi-tenant customers)
- Update `TenantResolutionMiddleware` to support session-based tenant selection
- JWT token may need to omit TenantId claim (resolve at session level instead)
- Consider impact on existing single-tenant customers (backward compatible)

**UI/UX**:
- Tenant selector: dropdown in nav bar or modal after login
- Show organization name + logo for each tenant
- "Switch Organization" option in profile/settings
- Mobile: bottom sheet selector for tenant switching

---

## 🚀 Performance & Observability (HIGH PRIORITY)

### 17. **Fix X-Ray OTLP Endpoint (Broken — Zero Traces)**
**Priority**: HIGH
**Effort**: Low

**Description**:
X-Ray shows 0 traces in production. The OTLP exporter endpoint in CloudFormation is set to `https://xray.{region}.amazonaws.com/v1/traces`, but the OpenTelemetry .NET OTLP HTTP exporter auto-appends `/v1/traces`, resulting in a doubled path (`/v1/traces/v1/traces`).

**Fix**:
- Change `OpenTelemetry__ExporterEndpoint` in `infra/main.yaml` from `https://xray.${AWS::Region}.amazonaws.com/v1/traces` to `https://xray.${AWS::Region}.amazonaws.com`

---

### 18. **Enable OTEL Export Error Logging**
**Priority**: HIGH
**Effort**: Low

**Description**:
OTEL exporters fail silently by default. When X-Ray export fails, nothing appears in CloudWatch logs.

**Fix**:
- Add `ExportProcessorType` and error callback to the OTLP exporter configuration in `Program.cs` (API)
- Log export failures so broken telemetry pipelines are detectable

---

### 19. **Use Marten Lightweight Sessions**
**Priority**: HIGH
**Effort**: Low

**Description**:
Every request logs: "Opening a session without explicitly providing desired type." Default Marten sessions use identity map + dirty tracking overhead, which is unnecessary for a read-heavy API.

**Fix**:
- Add `.UseLightweightSessions()` to the `AddMarten()` call in API `Program.cs`
- Eliminates per-request identity map overhead

---

### 20. **Move Serilog Request Logging Before Endpoints**
**Priority**: HIGH
**Effort**: Low

**Description**:
`UseSerilogRequestLogging()` is placed after `UseFastEndpoints()` in the middleware pipeline. It should be before endpoints to capture accurate full-pipeline request timing.

**Fix**:
- Move `app.UseSerilogRequestLogging()` before `app.UseFastEndpoints()` in API `Program.cs`

---

### 21. **Fail Fast on send-otp When SMS Not Configured**
**Priority**: HIGH
**Effort**: Low

**Description**:
`POST /api/auth/send-otp` takes 5.5 seconds to return 500 when Semaphore API key is missing. It does DB lookups and OTP generation before discovering the SMS provider isn't configured.

**Fix**:
- In `SendOtp.HandleAsync`, check SMS provider availability **before** DB lookup and OTP generation when `ContactMethod == "phone"`
- Add an `IsConfigured` check on `ISmsService` or `SemaphoreSmsService` to short-circuit early

---

## 🔑 Auth & Onboarding

### 22. **Chatbot-Registered User First Login: OTP + Password Creation**
**Priority**: High
**Effort**: Medium

**Description**:
Users registered via the chatbot have no password set. On their first web login attempt, detect the missing password, send an OTP to verify identity, then prompt them to create a password before continuing to the normal authenticated flow.

**Requirements**:
- Detect at login that the user has no password hash stored (chatbot-registered users)
- Instead of returning "invalid credentials", trigger an OTP flow (email or SMS based on available contact info)
- After OTP verification, redirect to a "Create Password" screen
- User sets a new password (with confirmation field + strength validation)
- After password creation, automatically log the user in and continue to the existing post-login flow
- Subsequent logins use normal email + password authentication

**Technical Notes**:
- API: Add a check in the login endpoint — if user exists but `PasswordHash` is null/empty, return a specific response (e.g., `"requiresPasswordSetup": true`) instead of 401
- API: Add `POST /api/auth/set-initial-password` endpoint — accepts OTP verification + new password, sets the password hash
- Web: Handle the `requiresPasswordSetup` response on the login page — redirect to OTP verification then password creation
- Ensure OTP is validated before allowing password set (prevent account takeover)
- Consider rate limiting on the OTP + password set flow

**UI/UX**:
- Login page detects "needs password" response → shows info message ("First time? Let's verify your identity")
- OTP entry screen (reuse existing OTP component if available)
- Password creation form with strength indicator and confirm field
- Success → auto-login → redirect to dashboard

---

## 📈 Future Enhancements

### 14. **Add-Ons & Custom Features**
**Priority**: Low
**Effort**: High

**Description**:
Allow tenants to purchase additional capacity or features outside their plan.

**Examples**:
- +1000 bookings per month
- Additional branch location
- Premium support add-on
- API access add-on (if not in base plan)

---

### 15. **Referral Program**
**Priority**: Low
**Effort**: Medium

**Description**:
Reward users for referring new customers.

**Requirements**:
- Generate unique referral codes per tenant
- Track referrals and conversions
- Offer rewards (discount, free month, credits)
- Admin dashboard for referral analytics

---

## Implementation Priority

### Phase 0 (Performance — HIGH PRIORITY)
- [x] Fix X-Ray OTLP Endpoint (#17) — zero traces, doubled path
- [x] Enable OTEL Export Error Logging (#18) — silent failures
- [x] Use Marten Lightweight Sessions (#19) — unnecessary overhead per request
- [x] Move Serilog Request Logging Before Endpoints (#20) — inaccurate timing
- [x] Fail Fast on send-otp When SMS Not Configured (#21) — 5.5s wasted on missing key

### Phase 1 (Critical)
- [x] Auto-Expire Subscriptions Job
- [ ] Payment Gateway Integration
- [x] Email Notification System (Partial — OTP via AWS SES/SNS, subscription emails pending)

### Phase 2 (Important)
- [ ] Chatbot-Registered User First Login: OTP + Password Creation (#22)
- [ ] Multi-Tenant Customer Accounts
- [x] Upgrade/Downgrade Subscriptions (API complete, UI partial)
- [x] Usage Statistics Reset Job
- [x] Additional Limit Enforcement

### Phase 3 (Nice to Have)
- [x] Subscription Analytics Dashboard (Admin revenue + tenant usage views)
- [ ] Audit Logging
- [x] Tenant Billing Portal (View-only — no payment processing yet)

### Phase 4 (Future)
- [ ] Coupon & Discount Codes
- [ ] Free Trial Extensions
- [ ] Multi-Currency Support
- [ ] Add-Ons & Custom Features
- [ ] Referral Program

---

**Last Updated**: 2026-03-02
