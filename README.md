# Book me App!

A multi-tenant booking and scheduling platform built with .NET 9. Organizations can manage provider schedules, and customers can browse availability and book appointments in real time.

## Features

- **Multi-Tenant Architecture** - Shared database with tenant isolation via headers
- **Role-Based Views** - Customers browse and book; Providers manage their schedules; Admins oversee all
- **Real-Time Calendar** - Interactive day/week calendar with availability and booking status
- **Capacity-Based Availability** - Schedules show as available until fully booked
- **OTP Authentication** - Phone-based login and registration with one-time passwords
- **Subscription Management** - Tiered plans with usage limits and proration
- **In-App Notifications** - Real-time notification bell for booking updates

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Backend API** | ASP.NET Core 9, FastEndpoints, Vertical Slice Architecture |
| **Frontend** | Blazor Server (InteractiveServer), MudBlazor 9, MudCalendar 4 |
| **Database** | PostgreSQL 16 via Marten (document store) |
| **Auth** | JWT with OTP-based registration/login |
| **Shared** | Contracts project with strongly-typed IDs |

## Project Structure

```
src/
  BookingScheduleSystem.Api/         # Backend API (FastEndpoints)
    Features/                        # Vertical slices (Auth, Bookings, Schedules, etc.)
    Infrastructure/                  # Marten documents, middleware, services
  BookingScheduleSystem.Web/         # Blazor frontend
    Components/Pages/                # Routable pages
    Components/Shared/               # Reusable dialogs and components
    Services/                        # Typed HTTP client services
  BookingScheduleSystem.Contracts/   # Shared DTOs, requests, responses, typed IDs
```

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/) (for PostgreSQL)

### Quick Start with Docker Compose

```bash
docker compose up -d
```

This starts PostgreSQL, the API (port 5059), and the Web app (port 5288).

### Local Development

1. Start PostgreSQL:
   ```bash
   docker compose up -d postgres
   ```

2. Run the API:
   ```bash
   cd src/BookingScheduleSystem.Api
   dotnet run
   ```

3. Run the Web app:
   ```bash
   cd src/BookingScheduleSystem.Web
   dotnet watch run
   ```

4. Open http://localhost:5288

## Ports

| Service | Port |
|---------|------|
| Web App | 5288 |
| API | 5059 |
| PostgreSQL | 5432 |

## Deployment

The application is automatically deployed to AWS ECS when changes are pushed to the `main` branch.

**📖 See [DEPLOYMENT.md](DEPLOYMENT.md) for:**
- Where to find your application after deployment
- How to access deployment summaries
- AWS Console locations for your application URL
- Deployment workflow details
- Monitoring and troubleshooting tips

## License

Private - All rights reserved.

