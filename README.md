# Northwind Support Desk

A support ticket management system built with **.NET 10 Web API**, **React 19 / TypeScript**, and **SQL Server 2022**.

---

## Quick Start

* To read my approaching steps read the [APPROACH.md](APPROACH.md) document. 

### 1. Run with Docker
Start both the database and the API container:

```bash
docker compose up --build
```

- **API URL:** http://localhost:5080
- **Health Check:** http://localhost:5080/health (returns `200 Healthy`)
- **Swagger UI:** http://localhost:5080/swagger

In a second terminal, launch the React web application:
```bash
npm install
npm run web
```
- **Web App:** http://localhost:5173 *(automatically proxies `/api` requests to `http://localhost:5080`)*

---

### 2. Run Locally (Without Docker API)
If you prefer running the .NET API directly on your host machine:
```bash
npm install
npm run db:up                 # Starts SQL Server in Docker (port 1433)
npm run api                   # Terminal 1: Starts .NET API (https://localhost:7043)
npm run web                   # Terminal 2: Starts React frontend (http://localhost:5173)
```

---

## Demo Login Credentials

All demo support agents share the local development password seeded in `SupportDbSeeder.cs`:

- **Password:** `Northwind_Dev_Pass1!`

| Agent Name | Email Address | Max Open Tickets | Specializations | Status | Result on Login |
| :--- | :--- | :---: | :--- | :---: | :--- |
| **Alex Turner** | `alex.turner@northwind-support.example` | 10 | General, Technical | Active | Signs in successfully |
| **Ben Osei** | `ben.osei@northwind-support.example` | 8 | Technical, Outage | Active | Signs in successfully |
| **Priya Nair** | `priya.nair@northwind-support.example` | 8 | Outage, Security | Active | Signs in successfully |
| **Sara Lindqvist** | `sara.lindqvist@northwind-support.example` | 6 | General, Billing | Active | Signs in successfully |
| **Marcus Doyle** | `marcus.doyle@northwind-support.example` | 6 | Billing, Security | Active | Signs in (Open tickets at limit) |
| **Yuki Tanaka** | `yuki.tanaka@northwind-support.example` | 8 | Technical | Inactive | Rejected with 401 Unauthorized |

---

## What Was Implemented & Rules as Understood

### 1. Defect Fix & Database-Side Filtering
- Fixed search input focus loss (`DEFECT-117`) and added debouncing.
- Implemented full SQL-side filtering, sorting, and pagination across all ticket fields.

### 2. Automatic Triage, Escalation & SLA
- **Priority:** Categories configured with `ForcesCriticalPriority` (Security, Outage) are automatically set to `Critical`. Other tickets use the requested priority or default to `Medium`.
- **SLA Windows:** Base windows are Critical (4h), High (8h), Medium (24h), and Low (72h). Premium customers receive a 50% window reduction with a strict 1-hour floor.
- **Agent Assignment:** Selects active agents holding required specializations (when `RequiresSpecialist` is true) who are strictly below their open ticket limit. Ties are broken deterministically by agent ID. If no agent qualifies, the ticket is created unassigned with an explanatory reason (returning HTTP 201).
- **Escalation:** Raises priority by one level (`Low → Medium → High → Critical`), recomputes the due date from the moment of escalation, re-evaluates the assigned agent, and records an immutable audit record. Tickets that are `Critical` or `Resolved/Closed` cannot be escalated.
- **SLA Status:** Evaluated dynamically on every read (`WithinSla`, `AtRisk`, `Breached`, `Met`, `NotApplicable`) using an injectable clock; it is never persisted to the database.

### 3. Login and Authentication
- Agents authenticate via `POST /api/auth/login` and verify their profile with `GET /api/auth/me`.
- Passwords are encrypted with PBKDF2 (HMAC-SHA256, 100,000 iterations).
- A central fallback policy closes all API routes by default, requiring a valid Bearer JWT.
- React provides protected routing, session persistence in `localStorage`, and automatic logout/redirect on any 401 response.

### 4. Containerisation
- Multi-stage Docker build utilizing layer caching for dependency restore.
- Runs as non-root user `app` on port 8080 (mapped to port 5080 on the host).
- Health endpoint (`GET /health`) actively verifies database reachability without requiring authentication.

---

## Design Decisions: What Was Chosen, Rejected & Why

### Token Lifetime and Session Storage
- **Decision:** Issued an 8-hour JWT stored in browser `localStorage`.
- **Rejected:** Short 15-minute tokens with refresh token rotation, or `HttpOnly` cookies.
- **Why:** 8 hours represents a full support agent shift, eliminating mid-workday session timeouts. Since refresh tokens were out of scope, 8 hours provides the best balance between usability and security. `localStorage` integrates cleanly with standard `Authorization: Bearer` headers across API endpoints.


---

## The Defect (`DEFECT-117`): Root Cause, Fix & Verification

- **Root Cause:** In `useTicketList`, typing into the search box triggered a state change that set `isLoading = true` on every keystroke. Because the filter bar had `disabled={isLoading}`, the HTML input element became disabled after the first character, causing the browser to immediately remove focus (blur). In addition, search inputs lacked debouncing, and backend query handlers were ignoring filter parameters.
- **Fix:** 
  1. Added a 250ms debounce to the search input so requests are only sent after the user pauses typing.
  2. Removed `disabled={isLoading}` from the search input so background data refetches do not steal user focus.
  3. Implemented full database-side filtering in `TicketQueries.cs`.
- **How to Verify:**
  - Automated test: Run `npm run test --workspace apps/web` to execute `useTicketList.test.ts`.
  - Manual test: Open the ticket list in the browser and type quickly into the search box. The cursor stays in the input without blurring, and the filtered results load cleanly.

---

## Database Filtering & Adding Future Filters

- **Where Applied:** In `TicketQueries.cs` via `ApplyFilters` using `IQueryable<TicketWithLabels>`.
- **SQL Verification:** Verified through EF Core SQL logging and xUnit integration tests. All filters (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `search`, `slaStatus`) combine with `AND` and execute in SQL Server before `.CountAsync()` and pagination (`Skip/Take`). `AsNoTracking()` is enabled on all queries to reduce memory consumption.
- **Adding a Future Filter:**
  1. Add the property to `TicketQuery` (e.g., `public string? Country { get; init; }`).
  2. Add one condition in `TicketQueries.ApplyFilters`:
     ```csharp
     if (!string.IsNullOrWhiteSpace(query.Country))
         source = source.Where(x => x.Customer.Country == query.Country);
     ```
  *No query restructuring, in-memory loops, or pagination changes are required.*

---

## Authentication & Security Architecture

- **Password Storage & Hashing:** Stored in `Agent.PasswordHash` using PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) with HMAC-SHA256, 100,000 iterations, a 16-byte cryptographically secure salt, and a 32-byte derived subkey. Verification uses `CryptographicOperations.FixedTimeEquals` to protect against timing attacks.
- **Token Signing & Payload:** Signed with HMAC-SHA256 (HS256) using a 32-byte secret key configured via `Jwt__SigningKey`. The payload embeds `sub` (Agent ID), `email`, `name`, and expiration timestamp (`exp`).
- **Escalation Actor Security:** When escalating a ticket, `escalatedBy` is read directly from verified JWT claims in `TicketsController.cs`, preventing callers from spoofing who performed the action.
- **Production Recommendations:** In production, store the signing key in Azure Key Vault or AWS Secrets Manager, enforce HTTPS, add login rate-limiting to mitigate brute-force attempts, and implement refresh token rotation.

---

## Docker Architecture & Migration Strategy

### Commands
```bash
docker compose up --build                       # Build and start services
docker compose down -v && docker compose up --build  # Reset database volume and seed fresh
```

### Environment Variables
| Variable | Default Value | Purpose |
| :--- | :--- | :--- |
| `API_PORT` | `5080` | Host port mapped to container port 8080 |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Enables Swagger, auto-migrations, and seed data |
| `ConnectionStrings__Default` | `Server=sqlserver,1433;Database=SupportDesk;...` | SQL Server connection string |
| `Jwt__SigningKey` | `local-dev-only-jwt-signing-key-not-for-production-32bytes!` | Secret key for JWT signing |

### Verification Details
1. **Layer Caching:** Project files, CPM props, and `global.json` are copied and restored before source files. Rebuilding after modifying `.cs` code reuses the cached restore layer.
2. **Security:** Runs as non-root user `app` (UID 1654) on HTTP port 8080. Verified via `docker compose exec api whoami` returning `app`.
3. **Image Size:** The runtime image (`aspnet:10.0`) is **~220 MB**, compared to **~850 MB** for the SDK build image, reducing container footprint by 74%.
4. **Health Check:** `GET /health` runs database reachability checks (`CanConnectAsync`) and is accessible without a token.
5. **Production Migration Strategy:** Automatic migration on application startup (`MigrateAndSeedAsync`) is convenient for local development. However, in production with multiple container replicas, running migrations on startup risks concurrent schema updates and database deadlocks. In production, migrations should be compiled into an idempotent script using `dotnet ef migrations bundle` and run inside a CI/CD pipeline or Kubernetes init-job prior to deploying new container versions.

---

## Database Changes & Indexing

### Migrations
1. `20261020045407_AddTicketEscalations`: Creates the `TicketEscalations` audit table.
2. `20261020073000_AddAgentPasswordHash`: Adds `PasswordHash` (`nvarchar(512)`) to `Agents`.

### Indexes Added
- **`IX_TicketEscalations_TicketId_EscalatedAtUtc` on `(TicketId, EscalatedAtUtc DESC)`:**  
  *Why:* Escalation history is queried by `TicketId` ordered newest-first (`GET /api/tickets/{id}/escalations`). This composite index satisfies both the filter and the sort directly from the index tree without a full table scan or in-memory sort.

---

## Testing

### Running the Test Suites
```bash
npm test                           # Runs both backend and frontend test suites
dotnet test                        # Backend tests only
npm run test --workspace apps/web  # Frontend tests only (Vitest)
```

### What Was Tested
- **Domain Invariants:** Escalation restrictions on closed/critical tickets, single-step priority progression, and exact boundary conditions for all SLA states (`WithinSla`, `AtRisk`, `Breached`, `Met`).
- **Application Triage:** Forced critical categories, specialist matching, agent capacity limits, deterministic tie-breaking, and unassigned ticket creation.
- **Security:** PBKDF2 password hashing, invalid credential handling, JWT signature and expiration verification, and fallback authorization enforcement.
- **Frontend:** Search debouncing and focus preservation (`DEFECT-117`), client-side reason validation preventing API calls, 409 conflict error rendering, accessible SLA badge rendering, and route redirection on 401.

### Deliberately Not Tested
- Framework plumbing (Kestrel socket bindings, CORS headers).
- Swagger UI generation.
- Pure CSS visual styles.

---

## AI Usage Statement

* AI used as an coding assitant to make the task completion faster. AI helped also to understand the logic behind the words of each tasks to make it easy to figure out how the things need to plan easily.

---

## Known Limitations

The following items were intentionally left out of scope per assignment instructions:
- User self-registration, password reset, and email verification.
- Refresh tokens and sliding session renewal (tokens expire after the 8-hour shift window).
- OAuth / SSO / OIDC integration and Multi-Factor Authentication (MFA).
- Granular role-based access control (all active agents have full triage permissions).
- Real-time push notifications (WebSockets / SignalR).
