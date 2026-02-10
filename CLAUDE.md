# BookingScheduleSystem – Root Agent Configuration

## TL;DR

This is a multi-tenant booking/scheduling system with three projects:

- **API** (`src/BookingScheduleSystem.Api/`) — ASP.NET Core + FastEndpoints backend
- **Web** (`src/BookingScheduleSystem.Web/`) — Blazor + MudBlazor frontend
- **Contracts** (`src/BookingScheduleSystem.Contracts/`) — Shared DTOs, request/response models, strong IDs

## Architecture

- .NET 9 / C# 13  
- Vertical Slice Architecture  
- Multi-tenant with shared database  
- FastEndpoints for API  
- MudBlazor for UI  
- Marten for document storage  
- IdentityServer + JWT for auth  

## Agent Team Rules

This workspace uses a **two-agent team** pattern. Each agent has strict file ownership:

| Agent | Owns | Config File |
|-------|------|-------------|
| Backend Agent | `src/BookingScheduleSystem.Api/` | `src/BookingScheduleSystem.Api/CLAUDE.md` |
| Frontend Agent | `src/BookingScheduleSystem.Web/` | `src/BookingScheduleSystem.Web/CLAUDE.md` |

### Shared Contracts (Coordination Zone)

`src/BookingScheduleSystem.Contracts/` is the **contract boundary** between agents.

- **Both agents may READ** contracts at any time.
- **Only ONE agent may WRITE** to contracts per task — the one whose feature requires the new contract.
- When writing a new contract, the agent MUST create a **contract proposal file** at `src/BookingScheduleSystem.Contracts/.proposals/<feature-name>.md` describing the new/changed DTOs before implementing them.
- The other agent should check `.proposals/` before starting work to pick up any new contracts.

### Conflict Prevention Rules

1. **Never edit files outside your owned directory** (except Contracts with the proposal process above).
2. **Never rename or restructure the other agent's files.**
3. **Use git branches**: each agent works on its own feature branch.
4. **Contract-first development**: agree on DTOs/contracts before implementing features.
5. **Use the BACKLOG.md** as the shared task source.

## Conventions

- Follow `agent/skills.md` for all coding rules.
- Follow `agent/csharpagent.md` for C# patterns.
- Follow `agent/frontendengineer.md` for UI/UX guidelines.
- Microsoft naming conventions (PascalCase for public, camelCase for private).
- No numbers in C# identifiers.
- RFC 7807 problem details for errors.
- Structured logging with Serilog.
