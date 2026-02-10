# Contract Proposal: Subscription Upgrade/Downgrade

## Agent: Backend
## Date: 2026-02-10

## New Contracts:
- `ChangePlanRequest` — SubscriptionPlanId NewPlanId, BillingCycleDto? NewBillingCycle
- `ProrationPreviewResponse` — current plan info, new plan info, prorated amount, effective date

## Reason: Tenants need to change subscription plans mid-cycle with proration preview.

## Breaking Changes: None — all new contracts.
