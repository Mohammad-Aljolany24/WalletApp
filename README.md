# WalletApp — Event-Sourced Wallet

An event-sourced wallet application built with ASP.NET Core, CQRS, and JWT authentication.

This project is the **foundation for a matching engine / trading platform** (in progress). The architecture, patterns, and infrastructure built here transfer 100% to the bigger system.

---

## What This Demonstrates

- **Event Sourcing** — events are the source of truth, never updated, never deleted
- **CQRS** — write side (commands → events) separated from read side (queries → read models)
- **JWT Authentication** with BCrypt password hashing
- **SQL Server persistence** via EF Core migrations
- **Clean Architecture** — API, Core (domain), Data (infrastructure), Tests
- **Testing discipline** — 42 tests (30 unit + 12 integration), all passing
- **Optimistic concurrency safety** — unique index on `(AggregateId, Version)`

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
└── WalletApp.Tests/        # Unit + integration tests
```

### Key Design Decisions

- **Events are facts.** Named in past tense (`FundsDeposited`), immutable, append-only.
- **State is derived.** Current balance is computed by replaying events, never stored directly.
- **Read models are disposable.** If lost, they can be rebuilt from the event log.
- **Commands rehydrate.** Every write loads events, rebuilds the aggregate, then appends new events.
- **Queries never touch the event store.** They read from pre-built read models.
- **Business rules live in aggregates.** Not in controllers, not in services.
- **Auth is not event-sourced.** Users live in a plain SQL table (`Users`). Auth is infrastructure.

---

## Stack

- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** + **SQL Server Express**
- **JWT** + **BCrypt.Net**
- **Swagger UI** with Bearer authentication
- **xUnit** + **FluentAssertions** + **Moq** + **WebApplicationFactory**
- **Swashbuckle** for OpenAPI documentation

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
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

### Testing

```bash
dotnet test
```

Expected: **42 tests passing** (30 unit + 12 integration).

---

## API Endpoints

### Public

| Method | Endpoint | Body | Response |
|--------|----------|------|----------|
| POST | `/auth/register` | `{ "email": "...", "password": "..." }` | `{ "id": "...", "email": "..." }` |
| POST | `/auth/login` | `{ "email": "...", "password": "..." }` | `{ "token": "..." }` |

### Protected (require `Authorization: Bearer <token>`)

| Method | Endpoint | Query Params | Response |
|--------|----------|--------------|----------|
| POST | `/wallet/deposit` | `?amount=100` | `{ "balance": 100 }` |
| POST | `/wallet/withdraw` | `?amount=50` | `{ "balance": 50 }` |
| GET | `/wallet/balance` | — | `{ "balance": 50 }` |

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

---

## Testing Strategy

| Layer | Type | What it covers |
|-------|------|----------------|
| `Wallet` aggregate | Unit | Business rules, event production, rehydration |
| `WalletProjection` | Unit | Read model updates from events |
| `AuthService` | Unit | Registration, login, validation (with mocks) |
| Auth endpoints | Integration | Full HTTP flow with in-memory DB |
| Wallet endpoints | Integration | Auth requirement, deposit/withdraw flow, user isolation |

**Tests are written alongside each feature, not batched at the end.**

---

## Roadmap

- [ ] **Optimistic concurrency** — version numbers on events (in progress)
- [ ] **Policy-based authorization** — `CanTrade`, `IsVerified`, `IsFrozen`, `IsAdmin`
- [ ] **React + TypeScript frontend** — Vite, MUI, TanStack Query, Zustand
- [ ] **Matching engine** — `OrderBook` aggregate, price-time priority matching
- [ ] **Real-time updates** — SignalR for live order book
- [ ] **Full trading platform** — multiple symbols, candlestick charts, KYC workflow

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

1. **JWT `sub` claim remapping** — ASP.NET Core renames `sub` to `ClaimTypes.NameIdentifier` by default. Disable with `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()`.
2. **DI lifetimes** — anything using `DbContext` must be `Scoped`. A `Singleton` depending on `Scoped` fails at startup.
3. **Integration test factory** — must remove *all* EF Core registrations before adding InMemory, or EF refuses to start with two providers registered.
4. **Event log isolation** — the `Events` table has no foreign keys, by design. Nothing can cascade-delete events.
5. **Concurrency** — a version number + unique index is the entire enforcement mechanism. No code can bypass it.

---

## Environment

- .NET 10 SDK
- SQL Server Express (instance `.\SQLEXPRESS`)
- Visual Studio Code + C# Dev Kit
- EF Core CLI tools (`dotnet-ef`)

---

## License

MIT
