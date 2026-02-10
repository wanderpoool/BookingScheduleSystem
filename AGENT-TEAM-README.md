# Agent Team Setup — Claude Code

## TL;DR

Two Claude Code agents work in parallel: **Backend** owns the API, **Frontend** owns the Web UI.  
They coordinate through shared contracts and never touch each other's code.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Root CLAUDE.md                        │
│              (shared rules & conventions)                │
├───────────────┬────────────────────┬────────────────────┤
│               │                    │                    │
│  Backend Agent│   Contracts        │  Frontend Agent    │
│  (CLAUDE.md)  │   (shared zone)    │  (CLAUDE.md)      │
│               │                    │                    │
│  Api/         │   Contracts/       │  Web/             │
│  ├─Features/  │   ├─Auth/          │  ├─Components/    │
│  ├─Infra/     │   ├─Bookings/      │  ├─Services/     │
│  └─Program.cs │   ├─Schedules/     │  ├─Models/       │
│               │   ├─Common/        │  └─Program.cs    │
│               │   └─.proposals/    │                    │
│               │     (coordination) │                    │
└───────────────┴────────────────────┴────────────────────┘
```

## Quick Start

### Option 1: Launch Both Agents Together
```batch
start-both-agents.bat
```
This opens two terminal windows, one per agent.

### Option 2: Launch Individually
```batch
REM Terminal 1 — Backend
start-backend-agent.bat

REM Terminal 2 — Frontend
start-frontend-agent.bat
```

### Option 3: Launch with a Task
```batch
REM Backend gets the API task
start-backend-agent.bat "Implement POST /api/subscriptions/change-plan endpoint with proration logic"

REM Frontend gets the UI task
start-frontend-agent.bat "Build the subscription upgrade page with plan comparison and proration preview"
```

## How It Works

### File Ownership (Conflict Prevention)

| File/Directory | Backend Agent | Frontend Agent |
|---|---|---|
| `src/BookingScheduleSystem.Api/**` | **READ + WRITE** | READ only |
| `src/BookingScheduleSystem.Web/**` | READ only | **READ + WRITE** |
| `src/BookingScheduleSystem.Contracts/**` | READ + WRITE (with proposal) | READ + WRITE (with proposal) |
| `src/BookingScheduleSystem.Contracts/.proposals/` | READ + WRITE | READ + WRITE |
| `agent/*.md` | READ only | READ only |
| `BACKLOG.md` | READ only | READ only |

### Contract Coordination Protocol

Since both agents consume the shared `Contracts` project, they follow a **proposal-first** process:

1. Agent needs a new DTO → checks `.proposals/` for any pending proposals
2. Creates a proposal file: `.proposals/<feature-name>.md`
3. Creates the actual contract files
4. The other agent picks up the new contracts on its next task

This prevents both agents from creating conflicting versions of the same DTO.

### Git Workflow (Recommended)

```batch
REM Create feature branches from main
git checkout -b feature/backend-subscription-upgrade main
git checkout -b feature/frontend-subscription-upgrade main

REM Each agent works on its own branch
REM Backend agent commits to feature/backend-subscription-upgrade
REM Frontend agent commits to feature/frontend-subscription-upgrade

REM Merge contracts first, then both feature branches
git checkout main
git merge feature/backend-subscription-upgrade
git merge feature/frontend-subscription-upgrade
```

## CLAUDE.md Hierarchy

Claude Code automatically discovers and merges `CLAUDE.md` files from parent directories:

```
CLAUDE.md                              ← Root: shared rules, agent team overview
└── src/
    ├── BookingScheduleSystem.Api/
    │   └── CLAUDE.md                  ← Backend Agent: ownership, boundaries, tech stack
    ├── BookingScheduleSystem.Web/
    │   └── CLAUDE.md                  ← Frontend Agent: ownership, boundaries, UI rules
    └── BookingScheduleSystem.Contracts/
        └── .proposals/
            └── README.md             ← Contract proposal process docs
```

Each agent inherits the root `CLAUDE.md` rules AND its own project-specific `CLAUDE.md`.

## Example Workflow: Adding "Upgrade Subscription"

### Step 1: Choose a backlog item
From `BACKLOG.md`: *"Upgrade/Downgrade Subscriptions — Allow tenants to change their subscription plan mid-cycle with prorating."*

### Step 2: Launch both agents with their tasks

**Backend Agent:**
> Implement the subscription upgrade feature:
> - Create `POST /api/subscriptions/change-plan` endpoint
> - Calculate prorated charges/credits
> - Handle immediate vs. end-of-cycle changes
> - Create any needed contracts in BookingScheduleSystem.Contracts with a proposal

**Frontend Agent:**
> Build the subscription upgrade UI:
> - Plan comparison page showing current vs. available plans
> - Proration preview calculator
> - Confirmation dialog with price breakdown
> - Use contracts from BookingScheduleSystem.Contracts
> - Check .proposals/ for any new contracts from the Backend Agent

### Step 3: Both agents work in parallel
- Backend creates contracts → creates proposal → implements endpoint
- Frontend checks proposals → picks up new contracts → builds UI components

### Step 4: Merge
Both branches merge cleanly because they touched different files.

## Troubleshooting

| Issue | Solution |
|---|---|
| Both agents edited the same contract | Use the proposal process; one agent proposes, the other consumes |
| Agent edited the wrong project | The CLAUDE.md boundaries will prevent this if followed |
| Merge conflicts in Contracts | Keep proposals small and feature-scoped; merge contracts branch first |
| Agent doesn't see new contracts | Tell it to check `.proposals/` and re-read the contracts directory |
