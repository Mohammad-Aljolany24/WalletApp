# WalletApp — Event-Sourced Wallet

An event-sourced wallet application built with ASP.NET Core, CQRS, and JWT authentication.

This project is **Phase 1** of a three-phase build: `WalletApp → MatchEngine → Trading Platform`. The architecture, patterns, and infrastructure built here transfer 100% to the bigger system — Phase 2 reuses every line of it.

---

## What This Demonstrates

- **Event Sourcing** — events are the source of truth, never updated, never deleted
- **CQRS** — write side (commands → events) separated from read side (queries → read models)
- **JWT Authentication** with BCrypt password hashing
- **SQL Server persistence** via EF Core migrations
- **Clean Architecture** — API, Core (domain), Data (infrastructure), Tests
- **Testing discipline** — 61 tests (30 unit + 31 integration), all passing
- **Optimistic concurrency safety** — unique index on `(AggregateId, Version)`
- **Policy-based authorization** — `CanTrade`, `IsVerified`, `AccountNotFrozen`, `IsAdmin`
- **React + TypeScript frontend** — Vite 8, React 19, MUI 9, React Router 7

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                  EVENT LOG (source of truth)                 │
│                                                              │
│  FundsDeposited(100) → FundsDeposited(50) → FundsWithdrawn(30) │
└────────────────────────┬─────────────────────────────────────┘
                         │
                         │  projections
                         ▼
┌──────────────────────────────────────────────────────────────┐
│              READ MODELS (fast, disposable)                  │
│                                                              │
│  WalletReadModel: W1 | Balance 120                           │
└──────────────────────────────────────────────────────────────┘

Write side:  Command → Handler → Aggregate → Events → Event Store
Read side:   Query → Query Handler → Read Model → Response
```

### Project Structure

```
WalletApp/
├── WalletApp.API/          # Endpoints, Program.cs, config
├── WalletApp.Core/         # Domain: aggregates, events, interfaces, projections, queries
├── WalletApp.Data/         # Infrastructure: EF Core, SQL Server, JWT, BCrypt
├── WalletApp.Tests/        # Unit + integration tests (61)
└── frontend/               # Vite + React + TS + MUI
```

### Key Design Decisions

- **Events are facts.** Named in past tense (`FundsDeposited`), immutable, append-only.
- **State is derived.** Current balance is computed by replaying events, never stored directly.
- **Read models are disposable.** If lost, they can be rebuilt from the event log.
- **Commands rehydrate.** Every write loads events, rebuilds the aggregate, then appends new events.
- **Queries never touch the event store.** They read from pre-built read models.
- **Business rules live in aggregates.** Not in controllers, not in services.
- **Auth is not event-sourced.** Users live in a plain SQL table (`Users`). Auth is infrastructure.
- **Policies read fresh state.** A user freeze takes effect on the *next request*, not the next login. The policy handler queries `IUserStore`, not the JWT.

---

## Stack

### Backend

- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** + **SQL Server Express**
- **JWT** + **BCrypt.Net**
- **Swagger UI** with Bearer authentication
- **xUnit** + **FluentAssertions** + **Moq** + **WebApplicationFactory**
- **Swashbuckle** for OpenAPI documentation

### Frontend

- **Vite 8** + **TypeScript 6** (strict)
- **React 19** — function components + hooks
- **React Router 7**
- **React Hook Form** — form validation
- **MUI 9** — components + responsive system
- **React Context** — auth state + refresh invalidation
- **`fetch` + custom wrapper** — no axios
- **Explicitly NOT used:** TanStack Query, Zustand, Zod. The small versions are hand-rolled on purpose; adopt later when the pain is real.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ and npm 11+
- SQL Server Express (or LocalDB / Docker SQL Server)
- Git

### Setup

```bash
git clone https://github.com/Mohammad-Aljolany24/WalletApp.git
cd WalletApp

# Update the connection string in WalletApp.API/appsettings.json if needed
# Default expects: .\SQLEXPRESS

# Apply migrations
dotnet ef database update --project WalletApp.Data --startup-project WalletApp.API

# Run the API
dotnet watch run --project WalletApp.API
```

The API runs on `http://localhost:5138` (check the terminal for the actual port).

Navigate to `/swagger` for the interactive API documentation.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend runs on `http://localhost:5173`. Backend CORS is already configured for it. The frontend reads the API URL from `.env.development` (`VITE_API_URL=http://localhost:5138`) — adjust if your backend port differs.

**Both servers need to be running for the app to work.** API on `:5138`, frontend on `:5173`.

### Testing

```bash
dotnet test
```

Expected: **61 tests passing** (30 unit + 31 integration).

Filter to one layer:

```bash
dotnet test --filter "FullyQualifiedName~Unit"
dotnet test --filter "FullyQualifiedName~Integration"
```

---

## API Endpoints

### Public

| Method | Endpoint | Body | Response |
|--------|----------|------|----------|
| POST | `/auth/register` | `{ "email": "...", "password": "..." }` | `{ "id": "...", "email": "..." }` |
| POST | `/auth/login` | `{ "email": "...", "password": "..." }` | `{ "token": "..." }` |

### Protected (require `Authorization: Bearer <token>`)

| Method | Endpoint | Query Params | Policy |
|--------|----------|--------------|--------|
| GET | `/auth/me` | — | authenticated |
| GET | `/wallet/balance` | — | authenticated |
| POST | `/wallet/deposit` | `?amount=100` | `AccountNotFrozen` |
| POST | `/wallet/withdraw` | `?amount=50` | `CanTrade` (verified + not frozen) |
| GET | `/wallet/transactions` | — | authenticated |

### Admin

| Method | Endpoint | Policy |
|--------|----------|--------|
| GET | `/admin/users` | `IsAdmin` |
| POST | `/admin/users/{id}/verify` | `IsAdmin` |
| POST | `/admin/users/{id}/freeze` | `IsAdmin` |

### Using Swagger

1. Navigate to `http://localhost:5138/swagger`
2. Run `POST /auth/login` to get a token
3. Click the **Authorize** button (padlock icon)
4. Paste **only the token** (no `Bearer` prefix — Swagger adds it)
5. All protected endpoints are now accessible

---

## Database

### Tables

| Table | Purpose | Type |
|-------|---------|------|
| `Events` | Append-only event log | Source of truth |
| `WalletReadModel` | Fast balance lookups | Projection |
| `Users` | Auth (not event-sourced) | Infrastructure |
| `__EFMigrationsHistory` | EF Core migration tracking | Infrastructure |

### Key Constraints

- **Unique index on `(AggregateId, Version)`** in `Events` — prevents concurrent writes to the same wallet
- **Unique index on `Email`** in `Users` — prevents duplicate registrations
- **No foreign keys on `Events`** — the event log is isolated and cannot be cascaded

### Migrations

1. `InitialCreate`
2. `AddUsersTable`
3. `AddEventVersion` — hand-edited with a `ROW_NUMBER()` backfill for existing rows
4. `AddUserPolicyFields` — hand-edited to default `Role` to `"User"` for existing rows

**When a migration adds a non-nullable column to a populated table, hand-edit the migration.** Insert a `migrationBuilder.Sql("...")` backfill between `AddColumn` and `CreateIndex`. EF can't see your data.

---

## Testing Strategy

| Layer | Type | Files |
|-------|------|-------|
| `Wallet` aggregate | Unit | `WalletTests` (15) |
| `WalletProjection` | Unit | `WalletProjectionTests` (6) |
| `AuthService` | Unit | `AuthServiceTests` (9) |
| Auth endpoints | Integration | `AuthEndpointTests` |
| Wallet endpoints | Integration | `WalletEndpointTests` |
| Authorization | Integration | `AuthorizationTests` |
| Concurrency | Integration | `ConcurrencyTests` (real SQL Server) |
| Frontend-extension endpoints | Integration | `NewEndpointsTests` |

**Tests are written alongside each feature, not batched at the end.**

---

## Roadmap

- [x] **Optimistic concurrency** — `Version` on aggregate + event, unique index, `ConcurrencyException → 409`
- [x] **Policy-based authorization** — `CanTrade`, `IsVerified`, `AccountNotFrozen`, `IsAdmin`
- [x] **React + TypeScript frontend** — Vite, React 19, MUI, React Router 7
- [x] **Phase 1 complete** — wallet works end-to-end: auth, deposit, withdraw, transaction list, admin verify/freeze
- [ ] **Phase 1.5** — ProblemDetails handler, idempotency keys, pagination, rate limiting, Serilog, health checks, API versioning, refresh tokens, CI
- [ ] **Phase 2 — MatchEngine** — `OrderBook` + `Order` aggregates, price-time priority matching, SignalR
- [ ] **Phase 2.5** — Docker Compose, Redis order book cache, Hangfire background jobs
- [ ] **Phase 3 — Full trading platform** — KYC tiers, multiple symbols, candlestick charts, fiat off-ramp withdrawals

---

## Why Event Sourcing?

Every change is recorded as an immutable fact. This gives:

- **Full audit trail** — who did what, when
- **Time travel** — rebuild state at any point in history
- **Multiple read models** — derived from the same events, optimized for different queries
- **Natural fit for financial systems** — correctness and traceability matter more than storage

Traditional CRUD loses history on every `UPDATE`. Event sourcing keeps it forever.

---

## Learnings & Gotchas

Some things learned while building this:

1. **JWT claim mapping (.NET 8+).** `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` is dead code — it was removed from the validation pipeline. The replacement is `options.MapInboundClaims = false` on `JwtBearerOptions`. Symptom when you get it wrong: the JWT validates fine, `IsAuthenticated == true`, but `context.User.FindFirst("sub")` returns null and every policy returns 403.
2. **`RoleClaimType = "role"` is required** for `RequireRole("Admin")` to work with a custom JWT. Without it, `RequireRole` reads `ClaimTypes.Role` (a long URI), finds nothing, and 403s everyone.
3. **DI lifetimes** — anything using `DbContext` must be `Scoped`. A `Singleton` depending on `Scoped` fails at startup.
4. **Integration test factory** — must remove *all* EF Core registrations before adding InMemory, or EF refuses to start with two providers registered.
5. **Event log isolation** — the `Events` table has no foreign keys, by design. Nothing can cascade-delete events.
6. **Concurrency** — a version number + unique index is the entire enforcement mechanism. No code can bypass it.
7. **CORS ordering.** `app.UseCors("Frontend")` must come before `UseAuthentication` and `UseAuthorization`. Symptom when wrong: browser shows "blocked by CORS policy" but the backend logs nothing — the request never reached the endpoint.
8. **`FrameworkReference Microsoft.AspNetCore.App`** is required on any class library that declares `IAuthorizationRequirement`. Without it, custom requirements fail to compile in `WalletApp.Core`.

---

## What's Next

Phase 1.5 adds the operational layer — everything a senior engineer expects to see that a domain-focused build skips: ProblemDetails-based error responses, idempotency keys, pagination, rate limiting, structured logging with correlation IDs, health checks, API versioning, refresh tokens, and CI.

That layer is added *after* the wallet works, on purpose: it gets copied into Phase 2 (MatchEngine) for free, and it means every subsequent phase starts from a stronger base than the last.

---

## Environment

- .NET 10 SDK
- Node.js 24 LTS, npm 11.x
- SQL Server Express (instance `.\SQLEXPRESS`)
- Visual Studio Code + C# Dev Kit
- EF Core CLI tools (`dotnet-ef`)

---

## License

MIT