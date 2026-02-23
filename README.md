<div align="center">

# BookMeApp

**A Multi-Tenant Booking & Scheduling Platform**

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?style=for-the-badge&logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![AWS](https://img.shields.io/badge/AWS-ECS_Fargate-FF9900?style=for-the-badge&logo=amazonaws&logoColor=white)](https://aws.amazon.com/ecs/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
[![GitHub Actions](https://img.shields.io/badge/CI%2FCD-GitHub_Actions-2088FF?style=for-the-badge&logo=githubactions&logoColor=white)](https://github.com/features/actions)
[![MudBlazor](https://img.shields.io/badge/MudBlazor-9.0-7B1FA2?style=for-the-badge)](https://mudblazor.com/)

Organizations manage provider schedules. Customers browse availability and book appointments in real time.
Built with **Vertical Slice Architecture**, multi-tenant isolation, and deployed on **AWS ECS Fargate**.

</div>

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              AWS Cloud (VPC)                                │
│                                                                             │
│   ┌───────────────────────────────────────────────────────────────────────┐ │
│   │                      Public Subnets (2 AZs)                           │ │
│   │                                                                       │ │
│   │                 ┌───────────────────────────────┐                     │ │
│   │                 │   Application Load Balancer    │                     │ │
│   │                 │     (HTTP / HTTPS Routing)     │                     │ │
│   │                 └──────────┬──────────┬──────────┘                     │ │
│   └────────────────────────────┼──────────┼───────────────────────────────┘ │
│                                │          │                                 │
│   ┌────────────────────────────┼──────────┼───────────────────────────────┐ │
│   │                  Private Subnets (2 AZs)                              │ │
│   │                            │          │                               │ │
│   │   ┌────────────────────────▼───┐  ┌───▼────────────────────────────┐  │ │
│   │   │    ECS Fargate (Spot)      │  │    ECS Fargate (Spot)          │  │ │
│   │   │  ┌──────────────────────┐  │  │  ┌──────────────────────────┐  │  │ │
│   │   │  │  Blazor Server App   │  │  │  │   FastEndpoints API      │  │  │ │
│   │   │  │  MudBlazor UI        │  │  │  │   Marten + JWT Auth      │  │  │ │
│   │   │  │  Port 5288           │  │  │  │   Port 5059              │  │  │ │
│   │   │  └──────────────────────┘  │  │  └────────────┬─────────────┘  │  │ │
│   │   └────────────────────────────┘  └───────────────┼────────────────┘  │ │
│   │                                                   │                   │ │
│   │   ┌─────────────────────────┐  ┌──────────────────▼────────────────┐  │ │
│   │   │     AWS Cloud Map       │  │      RDS PostgreSQL 16            │  │ │
│   │   │   (Service Discovery)   │  │      (Marten Document Store)      │  │ │
│   │   └─────────────────────────┘  └───────────────────────────────────┘  │ │
│   └───────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
│   ┌──────────────┐ ┌─────────────┐ ┌────────────┐ ┌───────────────────┐   │
│   │ Secrets       │ │ CloudWatch  │ │  AWS X-Ray │ │     AWS SES       │   │
│   │  Manager      │ │   Logs      │ │  (Tracing) │ │    (Email)        │   │
│   └──────────────┘ └─────────────┘ └────────────┘ └───────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

        ┌───────────────┐
        │ GitHub Actions │──── Build ──── Push to ECR ──── Deploy to ECS
        │   (CI / CD)    │
        └───────────────┘
```

## Application Architecture

```
┌──────────────────────────────────┐      ┌──────────────────────────────────┐
│       Blazor Server (Web)        │      │       FastEndpoints (API)        │
│                                  │      │                                  │
│  ┌────────────┐  ┌────────────┐  │      │  ┌───────────────────────────┐  │
│  │   Pages     │  │  Shared    │  │      │  │      Feature Slices       │  │
│  │   (27+)     │  │ Components │  │ HTTP │  │                           │  │
│  │             │  │  (15+)     │  ├─────►│  │  Auth · Bookings          │  │
│  └──────┬──────┘  └────────────┘  │      │  │  Schedules · Tenants      │  │
│         │                         │      │  │  Subscriptions · Users     │  │
│  ┌──────▼───────────────────────┐ │      │  │  Notifications · Analytics │  │
│  │   Services (18 typed HTTP    │ │      │  └────────────┬──────────────┘  │
│  │   client services)           │ │      │               │                 │
│  └──────────────────────────────┘ │      │  ┌────────────▼──────────────┐  │
│                                  │      │  │     Infrastructure        │  │
│  ┌──────────────────────────────┐ │      │  │                           │  │
│  │ Custom Auth State Provider   │ │      │  │  Marten Documents         │  │
│  │ (JWT in LocalStorage)        │ │      │  │  Tenant Middleware        │  │
│  └──────────────────────────────┘ │      │  │  JWT Auth · Email / SMS   │  │
│                                  │      │  │  Background Jobs          │  │
└──────────────────────────────────┘      │  │  Availability Calculator   │  │
                                          │  └───────────────────────────┘  │
┌──────────────────────────────────┐      │                                  │
│      Contracts (Shared DTOs)     │      └──────────────────────────────────┘
│                                  │
│  Strongly-Typed IDs              │
│  Request / Response Models       │
│  FluentValidation Rules          │
│  Enums (BookingStatus, etc.)     │
└──────────────────────────────────┘
```

---

## Complete Tech Stack

### Backend

| Technology | Version | Purpose |
|:---|:---:|:---|
| ![.NET](https://img.shields.io/badge/.NET_9-512BD4?style=flat-square&logo=dotnet&logoColor=white) | 9.0 | Runtime & framework |
| ![C#](https://img.shields.io/badge/C%23_13-239120?style=flat-square&logo=csharp&logoColor=white) | 13 | Language |
| ![FastEndpoints](https://img.shields.io/badge/FastEndpoints-blue?style=flat-square) | 7.2 | Minimal API framework with vertical slice support |
| ![Marten](https://img.shields.io/badge/Marten-green?style=flat-square) | 8.20 | PostgreSQL-backed document store |
| ![FluentValidation](https://img.shields.io/badge/FluentValidation-orange?style=flat-square) | 12.1 | Request validation |
| ![JWT](https://img.shields.io/badge/JWT-Bearer_Auth-000000?style=flat-square&logo=jsonwebtokens&logoColor=white) | — | Stateless authentication |
| ![Serilog](https://img.shields.io/badge/Serilog-red?style=flat-square) | 10.0 | Structured logging |
| ![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-blue?style=flat-square&logo=opentelemetry&logoColor=white) | 1.15 | Distributed tracing & observability |
| ![MailKit](https://img.shields.io/badge/MailKit-lightgrey?style=flat-square) | 4.12 | SMTP email (dev fallback) |

### Frontend

| Technology | Version | Purpose |
|:---|:---:|:---|
| ![Blazor](https://img.shields.io/badge/Blazor_Server-512BD4?style=flat-square&logo=blazor&logoColor=white) | 9.0 | Interactive server-side UI |
| ![MudBlazor](https://img.shields.io/badge/MudBlazor-7B1FA2?style=flat-square) | 9.0 | Material Design component library |
| ![MudCalendar](https://img.shields.io/badge/MudCalendar-blue?style=flat-square) | Custom | Interactive booking calendar |
| ![QRCoder](https://img.shields.io/badge/QRCoder-darkgreen?style=flat-square) | 1.7 | QR code generation for booking actions |

### Database

| Technology | Version | Purpose |
|:---|:---:|:---|
| ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=flat-square&logo=postgresql&logoColor=white) | 16 | Primary database |
| ![Marten](https://img.shields.io/badge/Marten-green?style=flat-square) | 8.20 | Document store over PostgreSQL (JSONB) |
| ![Npgsql](https://img.shields.io/badge/Npgsql-4169E1?style=flat-square) | 10.0 | .NET PostgreSQL data provider |

### Infrastructure & Cloud (AWS)

| Technology | Purpose |
|:---|:---|
| ![ECS](https://img.shields.io/badge/ECS_Fargate_Spot-FF9900?style=flat-square&logo=amazonecs&logoColor=white) | Container orchestration (80% Spot / 20% On-Demand) |
| ![RDS](https://img.shields.io/badge/RDS_PostgreSQL-4169E1?style=flat-square&logo=amazonrds&logoColor=white) | Managed database (db.t4g.micro, encrypted) |
| ![ALB](https://img.shields.io/badge/ALB-FF9900?style=flat-square&logo=awselasticloadbalancing&logoColor=white) | Application Load Balancer with path-based routing |
| ![CloudFormation](https://img.shields.io/badge/CloudFormation-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Infrastructure as Code (700+ lines) |
| ![ECR](https://img.shields.io/badge/ECR-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Docker image registry |
| ![Secrets Manager](https://img.shields.io/badge/Secrets_Manager-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Credential & secret management |
| ![SES](https://img.shields.io/badge/SES-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Production email delivery |
| ![X-Ray](https://img.shields.io/badge/X--Ray-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Distributed tracing |
| ![CloudWatch](https://img.shields.io/badge/CloudWatch-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Centralized log aggregation |
| ![Cloud Map](https://img.shields.io/badge/Cloud_Map-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Internal service discovery DNS |
| ![VPC](https://img.shields.io/badge/VPC-FF9900?style=flat-square&logo=amazonaws&logoColor=white) | Networking with public/private subnets across 2 AZs |
| ![Route 53](https://img.shields.io/badge/Route_53-FF9900?style=flat-square&logo=amazonroute53&logoColor=white) | DNS management (bookmeapp.ph) |

### DevOps & Tooling

| Technology | Purpose |
|:---|:---|
| ![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square&logo=docker&logoColor=white) | Multi-stage container builds |
| ![Docker Compose](https://img.shields.io/badge/Docker_Compose-2496ED?style=flat-square&logo=docker&logoColor=white) | Local development environment |
| ![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-2088FF?style=flat-square&logo=githubactions&logoColor=white) | CI/CD pipeline (build, push, deploy) |

### External Integrations

| Service | Purpose |
|:---|:---|
| ![Semaphore](https://img.shields.io/badge/Semaphore_SMS-green?style=flat-square) | SMS OTP delivery & booking notifications |
| ![AWS SES](https://img.shields.io/badge/AWS_SES-FF9900?style=flat-square) | Transactional email (confirmations, OTP, password resets) |

---

## Features

### Multi-Tenancy
- Shared database with per-request tenant isolation via `X-Tenant-Id` header
- Tenant resolution middleware on every request
- Creation codes for controlled tenant onboarding
- Tenant slugs for subdomain-based access

### Authentication & Security
- OTP-based login and registration (SMS + Email)
- JWT bearer tokens with configurable expiry
- Role-based access: **Global Admin** | **Tenant Admin** | **Provider** | **Customer**
- Account lockout after 5 failed attempts (15-minute cooldown)
- Booking action tokens for one-click email approval/rejection
- Rate limiting on authentication endpoints

### Scheduling & Availability
- Provider schedules with capacity-based slot management
- Configurable operating hours with day-specific break support
- Real-time availability calculator factors existing bookings
- Interactive calendar with day/week/month views
- Published/draft schedule states

### Booking Management
- Full lifecycle: Create, View, Approve, Reject, Cancel
- Status workflow: `Pending` &rarr; `Approved` / `Rejected` / `Cancelled`
- Provider notes on bookings
- Email + SMS notifications at every status change
- QR-coded booking action links in emails

### Subscription & Billing
- Tiered plans (Free, Basic, Pro) with usage limits
- Daily and monthly usage tracking
- Mid-cycle upgrade/downgrade with proration preview
- Automatic expiry and usage counter reset via background jobs
- Admin override controls (change plan, suspend, reactivate)

### Admin Dashboard
- Global platform analytics (tenants, revenue, active subscriptions)
- Per-tenant usage statistics
- Subscription and user management

### Notifications
- Real-time in-app notification bell with unread count
- Email via AWS SES (production) or SMTP (development)
- SMS via Semaphore API
- Booking action tokens with 3-hour expiry

---

## Project Structure

```
src/
├── BookingScheduleSystem.Api/              # Backend API
│   ├── Features/                           # Vertical slices by domain
│   │   ├── Analytics/                      #   Platform dashboard & stats
│   │   ├── Auth/                           #   Login, Register, OTP, Password Reset
│   │   ├── Bookings/                       #   CRUD, Approve, Reject, Cancel, Notes
│   │   ├── CreationCodes/                  #   Tenant signup codes
│   │   ├── Health/                         #   Health check
│   │   ├── Notifications/                  #   In-app notification management
│   │   ├── Schedules/                      #   Schedule CRUD + public browse
│   │   ├── SubscriptionPlans/              #   Plan management + seeding
│   │   ├── Subscriptions/                  #   Subscribe, upgrade, cancel
│   │   ├── Tenants/                        #   Tenant CRUD + subdomain lookup
│   │   └── Users/                          #   User management + provider roles
│   ├── Infrastructure/                     # Cross-cutting concerns
│   │   ├── Auth/                           #   JWT, password hashing, OTP
│   │   ├── BackgroundJobs/                 #   Subscription expiry, usage resets
│   │   ├── MultiTenancy/                   #   Tenant context & middleware
│   │   ├── Notifications/                  #   Email, SMS, booking action tokens
│   │   ├── Schedules/                      #   Availability calculator
│   │   ├── Subscriptions/                  #   Plan limits & proration
│   │   └── Telemetry/                      #   X-Ray integration
│   └── Dockerfile
│
├── BookingScheduleSystem.Web/              # Blazor Server frontend
│   ├── Components/
│   │   ├── Layout/                         # MainLayout, NavMenu, Footer
│   │   ├── Pages/ (27+)                    # Routable pages
│   │   │   ├── Admin/                      #   Dashboard, Subscriptions, Users
│   │   │   ├── Auth/                       #   Login, Register, Forgot Password
│   │   │   └── Onboarding/                 #   First Booking Wizard
│   │   └── Shared/ (15+)                   # Reusable components & dialogs
│   ├── Services/ (18)                      # Typed HTTP client services
│   └── Dockerfile
│
├── BookingScheduleSystem.Contracts/        # Shared DTOs & typed IDs
│   ├── Common/                             # TenantId, UserId, BookingId, ScheduleId
│   ├── Auth/                               # Auth request/response models
│   ├── Bookings/                           # Booking models
│   ├── Schedules/                          # Schedule models
│   ├── Subscriptions/                      # Plan & subscription models
│   └── Validators/                         # FluentValidation rules
│
infra/
└── main.yaml                               # CloudFormation (700+ lines)
│
.github/workflows/
└── ci-cd.yml                               # GitHub Actions CI/CD
│
docker-compose.yml                          # Local dev environment
```

---

## API Endpoints

**58+ endpoints** organized by domain:

| Domain | Count | Key Operations |
|:---|:---:|:---|
| Auth | 6 | Login, Register, OTP verify, Password Reset |
| Bookings | 8 | CRUD, Approve, Reject, Cancel, Notes |
| Schedules | 6 | CRUD, Public Browse, Availability |
| Tenants | 7 | CRUD, Subdomain Lookup |
| Users | 7 | Management, Provider Roles |
| Subscriptions | 8 | Subscribe, Upgrade, Cancel, Admin Actions |
| Subscription Plans | 5 | Plan CRUD, Seeding |
| Notifications | 4 | List, Read, Mark All Read |
| Analytics | 4 | Platform Summary, Revenue, Usage Stats |
| Creation Codes | 3 | Tenant Signup Codes |
| Health | 1 | Health Check |

---

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

### Ports

| Service | Port |
|:---|:---|
| Web App | `5288` |
| API | `5059` |
| PostgreSQL | `5432` |

---

## CI/CD Pipeline

```
  Push to main
       │
       ▼
┌──────────────┐    ┌──────────────┐    ┌─────────────────────┐
│  Build API   │    │  Build Web   │    │  Docker Multi-Stage  │
│  dotnet build│───►│  dotnet build│───►│  Build & Tag Images  │
└──────────────┘    └──────────────┘    └──────────┬──────────┘
                                                   │
                                                   ▼
                                        ┌─────────────────────┐
                                        │   Push to AWS ECR    │
                                        │  (SHA + latest tags) │
                                        └──────────┬──────────┘
                                                   │
                                                   ▼
                                        ┌─────────────────────┐
                                        │  Update ECS Tasks    │
                                        │  Force New Deploy    │
                                        │  Wait for Stability  │
                                        └─────────────────────┘
```

- Automatic deployment on push to `main`
- Production environment with approval gate
- Stack recovery for failed CloudFormation states
- Deployment summary with ALB URL and health check

---

## Deployment

Deployed to **AWS ECS Fargate** via **GitHub Actions**.

See [DEPLOYMENT.md](DEPLOYMENT.md) for application URLs, monitoring, and troubleshooting.

| Resource | Configuration |
|:---|:---|
| Compute | ECS Fargate Spot (80%) + On-Demand (20%) |
| Database | RDS PostgreSQL 16 (db.t4g.micro, encrypted) |
| Networking | VPC with public/private subnets across 2 AZs |
| Load Balancer | ALB with path-based routing |
| Service Discovery | Cloud Map (`api.bookmeapp.internal`) |
| Secrets | 7 secrets in AWS Secrets Manager |
| Logging | CloudWatch Logs + Serilog structured JSON |
| Tracing | OpenTelemetry &rarr; AWS X-Ray |
| Domain | bookmeapp.ph (Route 53) |

---

## License

Private - All rights reserved.
