# Multi-tenant Booking Scheduling System – Agent Skills & Guidelines

## TL;DR
- Use modern C# with Microsoft coding standards and naming conventions.
- Architect with SOLID principles and clear separation between API and UI.
- API: ASP.NET Core using FastEndpoints.
- UI: Blazor (Server or WASM as chosen per solution) consuming the API.
- Auth: IdentityServer + JWT (access tokens) with multi-tenant awareness.
- Keep code comments short, purposeful; avoid long, narrative comments.
- Do not use numbers in any C# identifier names.
- Support a global admin role that can oversee all tenants/companies and their users.
- Use secure, tenant-scoped creation codes for onboarding company users.

---

## General Engineering Principles
- Always follow official Microsoft C# coding conventions (bracing, spacing, naming, file layout).
- Apply SOLID principles consistently (SRP, OCP, LSP, ISP, DIP) in project and class design.
- Prefer composition over inheritance where possible.
- Keep methods small, cohesive, and focused on a single responsibility.
- Prefer dependency injection and interfaces for cross-cutting concerns and external integrations.

## Solution Architecture
- Maintain strict separation between API and UI:
  - API: Dedicated ASP.NET Core project using FastEndpoints.
  - UI: Dedicated Blazor project (e.g., Blazor Server or WebAssembly + host) consuming the API via HTTP.
- Use Vertical Slice Architecture for feature organization:
  - Group code by feature slice (e.g., Booking, Schedule, Tenant) rather than by traditional layers only.
  - Each slice contains its own endpoints, handlers, validators, and data access where appropriate.
- Use a shared library project for cross-cutting contracts and abstractions where appropriate:
  - DTOs / API contracts.
  - Shared primitives and interfaces that are stable across slices.
- Design the system as multi-tenant from the start with a shared database for all tenants:
  - Shared database with tenant-scoped data (for example, tenant column in relevant tables and documents).
  - Tenant identification (for example, host, header, or route segment) should be explicit and centralized.
  - Ensure every booking and scheduling operation is tenant-scoped.
  - Maximize use of Marten lightweight sessions for document storage and querying where applicable.
  - EF Core is acceptable where relational modeling is preferred; keep Marten and EF usage consistent and well-encapsulated.

## Admin And Tenant Management
- Provide a global admin role that is not bound to a single tenant and is used for platform-level oversight.
- Admin capabilities:
  - View all tenants/companies and aggregate counts of users per tenant.
  - Inspect a single tenant to view its users and related non-sensitive metadata.
  - Create new tenants/companies and manage high-level tenant lifecycle where required.
  - Generate tenant-scoped creation codes used for onboarding company users.
- Creation codes:
  - Secure, random tokens associated with a specific tenant/company.
  - Used during company user account creation to bind the user to the correct tenant.
  - Support configuration for maximum uses and optional expiration.
  - Track usage counts for each creation code for auditing and enforcement.
  - Validate creation codes strictly at the API boundary before user creation.
- Authorization:
  - Expose admin-only endpoints (for example, tenant overview, user counts, creation code management).
  - Protect admin endpoints with explicit admin authorization policies and role/claim checks.
  - Log and audit all admin actions affecting tenants, users, and creation codes.

## Domain Modeling
- Keep domain design and Ubiquitous Language in a dedicated domain planning document.
- Use this skills document as the technical and architectural guardrail, not the full domain specification.

## API (FastEndpoints) Requirements
- Implement all HTTP endpoints using FastEndpoints.
- Group endpoints logically by vertical slice and feature (for example, Booking, Schedule, Tenant, User).
- Use request and response DTOs; do not expose domain entities directly over the wire.
- Apply validation using FastEndpoints and/or FluentValidation patterns (feature-specific validators).
- Enforce multi-tenancy at the API boundary (middleware or FastEndpoints behaviors).
- Enforce admin-only access for platform-level operations (tenant lists, user counts, creation code management).
- Use async and await for all I/O-bound operations.
- For collection endpoints:
  - Support pagination, sorting, and filtering consistently.
  - Use clear, documented query parameters for paging and filtering.
- Follow RFC 7807 (problem details) for error responses in the API and surface the same shape to the UI.
- Consider and, where appropriate, implement:
  - Rate limiting and throttling for critical endpoints.
  - Idempotency for booking-related operations (for example, idempotency keys for create booking).

## UI (Blazor) Requirements
- Implement the user interface using Blazor.
- Use MudBlazor as the primary UI component framework.
- Use a component-based design:
  - Reusable components for common UI elements (lists, forms, dialogs).
  - Feature-specific components for booking creation, schedule views, tenant management, admin overviews, and related flows.
- Access the API via strongly typed HTTP clients (for example, HttpClient with typed client wrappers).
- Respect separation of concerns in UI:
  - UI logic in components.
  - Business and application logic in services.
- Surface RFC 7807 problem details cleanly in the UI for error display.
- Provide dedicated admin UI views for:
  - Tenant list and user counts.
  - Tenant detail and user list.
  - Creation code generation and basic usage status.

## Authentication & Authorization
- Use IdentityServer for identity and authorization server functionality.
- Use JWT access tokens for securing API calls.
- Integrate Blazor UI with IdentityServer for interactive login and logout.
- Secure API endpoints with appropriate authorization attributes/policies.
- Incorporate tenant information into authentication/authorization strategy (claims, scopes, or policies) so every request is tenant-aware.
- Define a clear global admin role/claim and associated authorization policies for admin-only operations.
- Ensure admin identities are clearly separated from tenant-bound users and can act across tenants only via secured admin endpoints.

## Security & Compliance
- Follow OWASP recommendations for input validation, output encoding, authentication, and authorization.
- Minimize and protect sensitive data; avoid logging secrets or confidential information.
- Ensure all APIs are secured with TLS, and apply defense in depth (for example, validation at both UI and API levels).

## Coding Style & Naming
- Follow Microsoft naming conventions:
  - Classes, records, structs, enums, methods, and properties: PascalCase.
  - Private and local variables, parameters, and fields: camelCase (with `_camelCase` for private fields if using that pattern consistently).
  - Interfaces: prefix with `I` followed by PascalCase.
- Do **not** use numbers in any C# identifier names (variables, fields, properties, methods, classes, etc.).
  - Example of disallowed names: `user1`, `booking2`, `v3`.
  - Use descriptive, word-based names instead.
- Use explicit and descriptive names that reveal intent; avoid abbreviations that are not widely understood.

## Comments & Documentation
- Do not add long narrative comments in code.
  - Keep comments short, focused, and necessary only where intent is not obvious from clean code.
- Prefer self-documenting code through clear naming, small methods, and clear abstractions.
- For any documentation files or design notes, always include a short TL;DR section at the top summarizing key points.

## Project & File Organization
- Organize code by feature or bounded context (e.g., `Booking`, `Schedule`, `Tenant`, `User`) rather than only by technical layer where it improves clarity.
- Use separate folders and namespaces for:
  - Endpoints (FastEndpoints-based).
  - Application services.
  - Domain entities and value objects.
  - Infrastructure (persistence, external services).
  - UI components, pages, and shared UI elements.
- Keep each class focused; avoid large "god" classes or overgrown utility classes.

## Observability, Configuration, and Operations
- Use structured logging (for example, Serilog) with correlation identifiers for requests.
- Add tracing and metrics for:
  - Booking success and failure counts.
  - Latency for critical operations and endpoints.
  - Tenant-level behavior where appropriate.
- Use the options pattern for environment-specific configuration.
- Manage secrets securely and outside of source control.
- Use feature toggles for gradually enabling capabilities or tenant-specific features.

## Testing & Quality
- Write unit tests for domain logic and critical application services.
- Prefer testing behavior rather than implementation details.
- Ensure tests respect the same naming and style conventions as production code.
- Where feasible, add integration tests for critical API flows (for example, booking creation, schedule retrieval, tenant isolation).
- Align CI and CD pipelines with expectations for:
  - Minimum code coverage thresholds.
  - Performance budgets for key endpoints and UI flows.
  - Automated checks for style, security, and basic performance where reasonable.

## Agent Behavior
- When generating code or architecture:
  - Respect all rules in this document strictly.
  - Default to patterns that maintain SOLID, separation of concerns, and multi-tenancy.
- When writing any new documentation within the repo, always include a TL;DR at the top.
- Do not introduce identifiers with numbers in C# code, even for temporary variables or test names.
- Avoid adding long comments; if extra explanation is required, place it in a markdown document rather than inline code comments.
- When interacting with this project via an AI coding assistant, prefer small, focused tasks and reference this skills document rather than restating rules.
