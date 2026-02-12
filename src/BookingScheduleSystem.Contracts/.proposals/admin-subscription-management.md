# Contract Proposal: Admin Subscription Management
## Agent: Frontend
## Date: 2026-02-13
## New/Modified Contracts:
- `AdminChangePlanRequest` — TenantId TenantId, SubscriptionPlanId NewPlanId, BillingCycleDto? NewBillingCycle, string? Reason
- `AdminSuspendSubscriptionRequest` — TenantId TenantId, required string Reason
- `AdminReactivateSubscriptionRequest` — TenantId TenantId, string? Reason
- `ListAllSubscriptionsResponse` — List<TenantSubscriptionResponse> Subscriptions, int TotalCount, int PageNumber, int PageSize
- Modified `TenantSubscriptionResponse` — added optional `string? TenantName`
## Reason: GlobalAdmin needs to manage all tenant subscriptions from a central admin view (change plan, suspend, reactivate)
## Breaking Changes: None — TenantName is optional/nullable addition
