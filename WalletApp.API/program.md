# WalletApp → MatchEngine → Trading Platform

## THE VISION (read this first)

This project has three phases:

### Phase 1 — WalletApp (CURRENT — nearly complete)
A simple event-sourced wallet application. Purpose: **learn event sourcing and CQRS on a small, safe domain** before applying it to something hard. Not a portfolio piece by itself — a training ground.

### Phase 2 — MatchEngine (NEXT)
An event-sourced **matching engine** — the backend of a stock/crypto exchange. This is the **portfolio project**. It reuses 100% of the infrastructure built in Phase 1 and adds:
- `OrderBook` aggregate with price-time priority matching
- `Order` aggregate
- Events: `OrderPlaced`, `OrderPartiallyFilled`, `OrderFullyFilled`, `OrderCancelled`, `TradeExecuted`
- Real-time order book via SignalR
- React frontend

### Phase 3 — Full Trading Platform (FUTURE — scalable target)
The MatchEngine grows into a **full trading website**:
- User accounts with KYC/verification workflows
- Order placement UI (market, limit, stop orders)
- Real-time order book display
- Portfolio and trade history
- Charts (candlesticks, volume)
- Deposit/withdrawal of funds
- Admin panel (freeze users, view all activity, audit logs)
- SignalR for live market data
- Optional: integration with free market data APIs (Finnhub, Alpaca paper trading) for real symbols

**Everything must be built so it scales into Phase 3.** That's why we're doing the wallet first — the patterns learned here apply 1:1 to the matching engine.

**Target market:** Jordan fintech job postings explicitly ask for CQRS, Event Sourcing, SQL depth, JWT, React + TypeScript. This project targets those roles specifically.

---

## STACK

- **.NET 10** (ASP.NET Core Web API)
- **4 projects:**
  - `WalletApp.API` — endpoints, `Program.cs`, config
  - `WalletApp.Core` — domain: aggregates, events, interfaces, projections, queries
  - `WalletApp.Data` — infrastructure: EF Core, SQL Server, JWT, BCrypt
  - `WalletApp.Tests` — unit + integration tests
- **EF Core 10** + **SQL Server Express** (instance `SQLEXPRESS`, database `WalletAppDb`)
- **JWT auth** with BCrypt password hashing
- **Swagger UI** with Bearer auth for manual testing
- **xUnit + FluentAssertions + Moq + WebApplicationFactory** for tests
- **Frontend (Phase 3):** React 18 + TypeScript + Vite + MUI + TanStack Query + Zustand + React Router
- **Real-time (Phase 2):** SignalR

---

## ARCHITECTURE

### Core principles
- **Event Sourcing** — events are the source of truth, never updated, never deleted
- **CQRS** — write side (commands → events) separate from read side (queries → read models)
- **Write side flow:** load events → rehydrate aggregate → run business logic → append events → run projections
- **Read side flow:** query → read model → return (NO rehydration)
- **Auth is NOT event-sourced** — Users live in a plain SQL table (`Users`)
- **Wallet ownership:** Wallet ID = User ID. Server reads user ID from JWT `sub` claim.
- **DI lifetimes:** anything using `DbContext` is `Scoped`. Stateless helpers can be `Singleton`.

### Solution structure
```
WalletApp/
├── WalletApp.API/
│   ├── Program.cs                  (has `public partial class Program { }` at bottom)
│   ├── appsettings.json
│   └── appsettings.Development.json
├── WalletApp.Core/
│   ├── Aggregates/
│   │   └── Wallet.cs
│   ├── Events/
│   │   ├── IEvent.cs
│   │   ├── FundsDeposited.cs
│   │   └── FundsWithdrawn.cs
│   ├── EventStore/
│   │   └── IEventStore.cs
│   ├── Auth/
│   │   ├── User.cs
│   │   ├── IUserStore.cs
│   │   ├── IPasswordHasher.cs
│   │   ├── ITokenService.cs
│   │   ├── IAuthService.cs
│   │   └── AuthService.cs
│   ├── Projections/
│   │   └── WalletProjection.cs
│   ├── Queries/
│   │   ├── GetBalanceQuery.cs
│   │   └── GetBalanceQueryHandler.cs
│   └── ReadModels/
│       ├── WalletReadModel.cs
│       └── IWalletReadStore.cs
├── WalletApp.Data/
│   ├── AppDbContext.cs
│   ├── Entities/
│   │   ├── EventRecord.cs
│   │   ├── WalletReadRecord.cs
│   │   └── UserRecord.cs
│   ├── Auth/
│   │   ├── BcryptPasswordHasher.cs
│   │   ├── JwtTokenService.cs
│   │   └── SqlUserStore.cs
│   ├── EventStore/
│   │   └── SqlEventStore.cs
│   ├── ReadModels/
│   │   └── SqlWalletReadStore.cs
│   └── Migrations/
└── WalletApp.Tests/
    ├── Unit/
    │   ├── WalletTests.cs              (15 tests)
    │   ├── WalletProjectionTests.cs    (6 tests)
    │   └── AuthServiceTests.cs         (9 tests)
    └── Integration/
        ├── CustomWebApplicationFactory.cs
        ├── TestHelpers.cs
        ├── AuthEndpointTests.cs        (5 tests)
        └── WalletEndpointTests.cs      (7 tests)
```

---

## STEPS COMPLETED

- [x] **Step 1:** Solution setup (3 projects), events (`IEvent`, `FundsDeposited`, `FundsWithdrawn`)
- [x] **Step 2:** In-memory event store (superseded by Step 6)
- [x] **Step 3:** `Wallet` aggregate (Deposit, Withdraw, Apply, Rehydrate, ClearUncommittedEvents)
- [x] **Step 4:** CQRS read side (`WalletReadModel`, `WalletProjection`, `GetBalanceQueryHandler`)
- [x] **Step 5:** JWT auth (`User`, `AuthService`, `JwtTokenService`, register/login)
- [x] **Step 6:** SQL Server persistence for events + read model (EF Core migrations)
- [x] **Step 7:** Swagger UI with JWT Bearer auth (paste-token-only UX)
- [x] **Step 7.5:** Persist users to SQL (`UserRecord`, `SqlUserStore`, migration `AddUsersTable`, DI lifetimes fixed)
- [x] **Step 8:** Test project setup + unit tests
  - 15 `WalletTests` (deposit, withdraw, rehydration, events, validation)
  - 6 `WalletProjectionTests` (state updates from events, using in-memory fake store)
  - 9 `AuthServiceTests` (register, login, validation, using Moq)
- [x] **Step 9:** Integration tests
  - `CustomWebApplicationFactory` — replaces SQL Server with in-memory DB
  - `TestHelpers.RegisterAndLoginAsync` helper
  - 5 `AuthEndpointTests` (register, login, errors)
  - 7 `WalletEndpointTests` (auth requirement, full flow, user isolation, business rules)
  - **42 tests total, all passing**

---

## STEPS REMAINING

### Phase 1 — WalletApp (finish these)

- [ ] **Step 10:** Optimistic concurrency
  - Add `Version` to `Wallet` aggregate (private set, incremented in `Apply`)
  - Add `Version` to `EventRecord` entity
  - Add unique index on `(AggregateId, Version)` in `AppDbContext`
  - Update `IEventStore.AppendAsync` signature to take `expectedVersion`
  - Update `SqlEventStore` to check version and throw `ConcurrencyException` on mismatch
  - Update endpoints to pass `expectedVersion` from rehydrated wallet
  - Create migration `AddEventVersion`
  - Add integration test: two clients write concurrently → one succeeds, one fails

- [ ] **Step 11:** Policy-based authorization
  - Add to `User` and `UserRecord`: `Role` (string, default "User"), `IsVerified` (bool), `IsFrozen` (bool)
  - Migration `AddUserPolicyFields`
  - Define policies in `Program.cs`:
    - `CanTrade` — user is verified AND not frozen
    - `IsAdmin` — role-based
    - `IsVerified` — flag check
    - `AccountNotFrozen` — flag check
  - Create `IAuthorizationHandler` for `CanTrade` (fetches user from `IUserStore`)
  - Add admin endpoints: `POST /admin/users/{id}/verify`, `POST /admin/users/{id}/freeze`
  - Apply policies to wallet endpoints (`.RequireAuthorization("CanTrade")`)
  - Add integration tests for each policy scenario

### Phase 2 — MatchEngine (the portfolio project)

- [ ] **Step 12:** Rename solution to `MatchEngine`
  - Option A: Rename folder + projects + namespaces
  - Option B: Keep `WalletApp` as its own portfolio piece and start fresh `MatchEngine` copying infrastructure (recommended)
  - Reuse: `IEventStore`, `SqlEventStore`, `IWalletReadStore` pattern, projection pattern, JWT, migrations, test setup

- [ ] **Step 13:** Add `OrderBook` aggregate + matching algorithm
  - Events: `OrderPlaced`, `OrderPartiallyFilled`, `OrderFullyFilled`, `OrderCancelled`, `TradeExecuted`
  - Price-time priority matching: best price first, ties broken by earliest order
  - In-memory order book data structures (`SortedDictionary<decimal, Queue<Order>>` for bids and asks)
  - Support market and limit orders only in v1
  - **Unit test the matching algorithm heavily** — this is the core logic

- [ ] **Step 14:** Add `Order` aggregate
  - Track individual order lifecycle
  - Events per order

- [ ] **Step 15:** New projections
  - `OrderBookProjection` — keeps the read-model order book updated
  - `TradeHistoryProjection` — records all executed trades
  - `WalletProjection` — reuse from WalletApp

- [ ] **Step 16:** New queries
  - `GetOrderBookQuery` → returns bids/asks
  - `GetTradeHistoryQuery` → user's trades
  - `GetPortfolioQuery` → user's positions

- [ ] **Step 17:** SignalR for real-time order book updates
  - Hub: `OrderBookHub`
  - Clients subscribe to symbol updates
  - Server broadcasts on `OrderPlaced`, `TradeExecuted`, etc.

- [ ] **Step 18:** React frontend
  - Login / register
  - Order placement form
  - Live order book display
  - Portfolio view
  - Trade history
  - Charts (Recharts)

### Phase 3 — Full Trading Platform (future scope)

- [ ] KYC workflow (upload ID, admin approval)
- [ ] Multiple order types (stop-loss, IOC, FOK)
- [ ] Multiple symbols (BTC, ETH, AAPL)
- [ ] Candlestick charts (OHLC aggregation projection)
- [ ] Price alerts (background jobs)
- [ ] Admin dashboard (freeze users, view all activity, audit log query)
- [ ] Free market data integration (Finnhub, Alpaca paper trading)
- [ ] Email/SMS notifications on order fills
- [ ] Rate limiting per user
- [ ] API versioning (`/api/v1/...`)

### Enterprise layer (after Phase 3, for interviews)

- [ ] Serilog structured logging + correlation IDs
- [ ] Health checks (`/health`, `/health/ready`)
- [ ] ProblemDetails error responses (business errors return 400, not 500)
- [ ] Idempotency keys on write endpoints
- [ ] Docker + Docker Compose
- [ ] GitHub Actions CI/CD
- [ ] Redis caching
- [ ] Hangfire background jobs

---

## TESTING STRATEGY

### When to test — the rule
**Test each piece right after it works, before moving on.**
1. Write a method
2. Test manually (Swagger)
3. It works
4. **Write 2-3 tests for it immediately**
5. Run them. Pass. Move on.

### Current coverage (42 tests, all passing)

**Unit tests (30):**
- `Wallet` aggregate — happy paths, validation, event production, rehydration
- `WalletProjection` — deposit creates row, deposit adds, withdraw subtracts, unknown events ignored
- `AuthService` — register, login, email normalization, password hashing, validation

**Integration tests (12):**
- `AuthEndpointTests` — register, login, duplicate email, unknown email, wrong password
- `WalletEndpointTests` — 401 without token, full flow, multiple deposits, withdraw, insufficient funds, user isolation

### Testing tools
| Use case | Tool |
|---|---|
| Unit tests | xUnit + FluentAssertions |
| Mocking dependencies | Moq |
| In-memory state for unit tests | Hand-written Fake classes |
| HTTP integration tests | `WebApplicationFactory<Program>` |
| In-memory database for integration | EF Core `UseInMemoryDatabase` |

### What NOT to test
- Getters, setters, DTOs
- Framework code
- Trivial mappers

### The critical fix for integration tests
The `CustomWebApplicationFactory` must remove **ALL** EF Core registrations from the API before adding InMemory, otherwise EF throws "only a single database provider can be registered." See the file for the exact fix (removes `DbContextOptions<T>`, `DbContextOptions`, `T`, and `IDbContextOptionsConfiguration<T>`).

---

## DATABASE

### Connection string (`appsettings.json`)
```json
"ConnectionStrings": {
  "Default": "Server=.\\SQLEXPRESS;Database=WalletAppDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### JWT config (`appsettings.json`)
```json
"Jwt": {
  "Secret": "this-is-a-dev-secret-key-change-me-in-production-at-least-32-chars-long",
  "Issuer": "WalletApp",
  "Audience": "WalletAppUsers"
}
```

### SSMS connection
- Server: `.\SQLEXPRESS`
- Auth: Windows Authentication

### Tables
- **`Events`** — append-only. Columns: `Id (bigint identity PK)`, `AggregateId (guid)`, `EventType (string, 100)`, `Data (nvarchar max, JSON)`, `Version (int)`, `OccurredAt (datetime2)`. Unique index on `(AggregateId, Version)` planned for Step 10.
- **`WalletReadModel`** — `WalletId (guid PK)`, `Balance (decimal(18,2))`
- **`Users`** — `Id (guid PK)`, `Email (varchar 256, unique)`, `PasswordHash`, `CreatedAt`
- **`__EFMigrationsHistory`** — EF Core migration tracking

### Applied migrations
1. `InitialCreate` — creates `Events`, `WalletReadModel`
2. `AddUsersTable` — creates `Users`

---

## API ENDPOINTS

### Public
- `POST /auth/register` — body `{ "email": "...", "password": "..." }` → returns `{ "id": "...", "email": "..." }`
- `POST /auth/login` — body `{ "email": "...", "password": "..." }` → returns `{ "token": "..." }`

### Protected (`Authorization: Bearer <token>`)
- `POST /wallet/deposit?amount=100` → returns `{ "balance": 100 }`
- `POST /wallet/withdraw?amount=50` → returns `{ "balance": 50 }`
- `GET /wallet/balance` → returns `{ "balance": 50 }`

---

## COMMON COMMANDS

```powershell
# Build
dotnet build

# Run API with hot reload
dotnet watch run --project WalletApp.API

# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "FullyQualifiedName~Unit"

# Run only integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Create a migration
dotnet ef migrations add MigrationName --project WalletApp.Data --startup-project WalletApp.API

# Apply migrations
dotnet ef database update --project WalletApp.Data --startup-project WalletApp.API

# List migrations
dotnet ef migrations list --project WalletApp.Data --startup-project WalletApp.API

# Remove last migration (only if not applied)
dotnet ef migrations remove --project WalletApp.Data --startup-project WalletApp.API
```

**Always include `--project` and `--startup-project`.**

---

## MANUAL TESTING

### Swagger
1. Navigate to `http://localhost:5138/swagger`
2. `POST /auth/login` → copy `token` value (no quotes, no "Bearer" prefix)
3. Click **Authorize** (padlock icon) → paste token → **Authorize** → **Close**
4. All protected endpoints work

### PowerShell
```powershell
$r = Invoke-RestMethod -Method Post -Uri "http://localhost:5138/auth/login" `
  -ContentType "application/json" `
  -Body '{"email":"test@test.com","password":"password123"}'
$token = $r.token

Invoke-RestMethod -Method Post -Uri "http://localhost:5138/wallet/deposit?amount=100" `
  -Headers @{ Authorization = "Bearer $token" }

Invoke-RestMethod -Uri "http://localhost:5138/wallet/balance" `
  -Headers @{ Authorization = "Bearer $token" }
```

---

## KEY CONCEPTS LEARNED

### Event Sourcing
- Events are immutable facts (past tense: `FundsDeposited`)
- Event store is append-only (no UPDATE, no DELETE)
- Current state = replay of events
- Deletion = appending an event saying "deleted"
- Event log is source of truth; read models are derived and disposable
- Event log has no foreign keys and no external references — it's isolated

### CQRS
- Write side: commands → handlers → aggregates → events
- Read side: events → projections → read models → queries
- Queries never touch the event store
- Commands rehydrate every time (loop over events)
- Queries read from read models (no rehydration, O(1))

### Aggregates
- Hold business rules
- Produce events
- State changes only through `Apply` (private)
- Rehydrated from events on every command
- `private set` on state

### Write-side pattern (memorize this)
```
1. Load events from the event store
2. Rehydrate the aggregate
3. Call the business method
4. Append uncommitted events to the store
5. Run projections to update read models
```

### DI lifetimes
- **Singleton:** stateless helpers (can only depend on Singleton)
- **Scoped:** anything using `DbContext`
- **Rule:** if it uses `DbContext`, it must be Scoped

### Optimistic concurrency (Step 10, not yet)
- Version column on events + unique index on `(AggregateId, Version)`
- Prevents two commands from writing to same aggregate simultaneously
- DB enforces it — no code can bypass

### Snapshots (concept understood, not implemented)
- Cache aggregate state at a version
- Rehydrate: load snapshot + replay events after snapshot version
- Only needed when aggregates have thousands of events

### Testing
- Unit for aggregates, services, projections
- Integration for endpoints via `WebApplicationFactory`
- Fakes for state, Mocks for interaction
- Test each piece right after it works

---

## GOTCHAS LEARNED THE HARD WAY

1. **JWT `sub` claim remapping:** ASP.NET Core renames `sub` to `ClaimTypes.NameIdentifier` by default. Fix in `Program.cs`:
   ```csharp
   System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
   ```
   Plus `NameClaimType = "sub"` in `TokenValidationParameters`.

2. **Swagger Authorize:** With `SecuritySchemeType.Http` + `Scheme = "bearer"`, paste **ONLY the token** (no `Bearer ` prefix). The name in `AddSecurityDefinition` must exactly match the name in `AddSecurityRequirement`.

3. **EF Core CLI:** Requires both `--project` and `--startup-project`.

4. **`AddSingleton` vs `AddScoped`:** Stores using `AppDbContext` must be `AddScoped`. If a Singleton depends on a Scoped, DI fails at startup (including `dotnet ef` migrations).

5. **SQL Server instance:** `.\SQLEXPRESS` (SQL Express, not LocalDB). In JSON: `.\\SQLEXPRESS`.

6. **Users are NOT event-sourced.** Plain SQL table.

7. **Event type resolution for JSON:** `Type.GetType($"WalletApp.Core.Events.{record.EventType}, WalletApp.Core")`.

8. **`Microsoft.OpenApi` 2.x:** namespace is `Microsoft.OpenApi` (no `.Models`). `OpenApiSecurityScheme.Reference` is gone — use `OpenApiSecuritySchemeReference` and the delegate overload of `AddSecurityRequirement`.

9. **Integration tests need `public partial class Program { }` at end of `Program.cs`** for the test project to reference the API assembly.

10. **CRITICAL: `CustomWebApplicationFactory` must remove ALL EF Core registrations** before adding InMemory. Removing just `DbContextOptions<AppDbContext>` is not enough — EF Core 8+ registers multiple services (`DbContextOptions<T>`, `DbContextOptions`, `T`, `IDbContextOptionsConfiguration<T>`). Otherwise: "Services for database providers 'Microsoft.EntityFrameworkCore.SqlServer', 'Microsoft.EntityFrameworkCore.InMemory' have been registered."

11. **In-memory DB per factory instance:** Use `Guid.NewGuid()` in the DB name so test classes don't share state.

---

## WHAT CARRIES OVER 100% TO MATCHENGINE

**Keep as-is:**
- `IEventStore` / `SqlEventStore`
- `IWalletReadStore` / `SqlWalletReadStore` (pattern reused)
- Projection pattern
- Query handler pattern
- 3-project structure + test project
- JWT auth + `Users` table + migrations
- Swagger config
- Testing setup (xUnit + WebApplicationFactory + Moq)
- `public partial class Program { }` in API
- `CustomWebApplicationFactory` (rename to `MatchEngine` namespace)

**Replace (domain only):**
- `Wallet` aggregate → `OrderBook` + `Order` + `Wallet`
- Events → add `OrderPlaced`, `OrderPartiallyFilled`, `OrderFullyFilled`, `OrderCancelled`, `TradeExecuted`
- Add projections: `OrderBookProjection`, `TradeHistoryProjection`
- Add queries: `GetOrderBookQuery`, `GetTradeHistoryQuery`, `GetPortfolioQuery`

**Add (new to MatchEngine):**
- Matching algorithm (price-time priority)
- In-memory order book data structures (`SortedDictionary`, `Queue`)
- Multiple aggregates per command
- SignalR real-time updates
- React frontend
- Unit tests for matching algorithm (critical)

**Scalability to full trading platform:**
- Multiple symbols: add `Symbol` field, per-symbol order book (dictionary)
- Multiple order types: new events per type, same pattern
- Charts: new projection that aggregates trades into OHLC buckets
- Price alerts: background jobs (Hangfire)
- KYC: `User.IsVerified` flag + verification workflow (already in Step 11)
- Admin: policies already in place

---

## ENTERPRISE ROADMAP (post Phase 3)

### Tier 1 — matters for interviews (5-7 days)
1. Serilog structured logging + correlation IDs
2. Health checks: `/health` (liveness), `/health/ready` (readiness)
3. API versioning (`Asp.Versioning.Mvc`)
4. Rate limiting (.NET middleware)
5. Idempotency keys on write endpoints
6. CI/CD (GitHub Actions: build + test + Docker)
7. ProblemDetails for business errors (400, not 500)

### Tier 2 — separates good from great (5-7 days)
8. Docker + Docker Compose (API + SQL Server + Redis)
9. Redis caching for order books
10. Hangfire background jobs
11. Audit log query endpoint (free from event sourcing)

### Tier 3 — know, don't build
Multi-tenancy, Kafka/RabbitMQ, microservices, Saga pattern, sharding, read replicas, Polly circuit breakers, blue-green deploy, feature flags

---

## ENVIRONMENT

- .NET SDK: **.NET 10**
- VS Code extensions: C# Dev Kit, C# (Microsoft), mssql, REST Client
- Note: C# Dev Kit had an activation error at one point. Using CLI only — no IDE features. Everything builds and runs.
- SQL Server Express (`MSSQL$SQLEXPRESS`, running)
- SSMS connects to `.\SQLEXPRESS` with Windows Auth
- API runs on `http://localhost:5138` (may change — check terminal)

---

## NEXT ACTION

**Step 10 — Optimistic Concurrency.** See "Steps remaining" for the full checklist.

After Step 10 → Step 11 (Policies) → Step 12 (Rename to MatchEngine) → Step 13 (OrderBook + matching algorithm).

---

## IF STARTING A NEW CHAT

Paste this entire file and say:

> "Continue from where this PROGRESS.md leaves off. The current state: 42 tests passing. Next step is Step 10 — Optimistic Concurrency. Give me the exact files to create/modify, plus migration command, plus integration test for the concurrent write scenario."

Be specific about wanting:
- Exact file paths
- Exact code (full files, not snippets)
- Exact commands
- How to test after
- What NOT to touch (the existing 42 tests should keep passing)