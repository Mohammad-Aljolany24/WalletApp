# WalletApp → MatchEngine → Trading Platform

## THE VISION (read this first)

Three phases:

- **Phase 1 — WalletApp** (complete): event-sourced wallet. Learn event sourcing + CQRS on a small domain. Working React frontend.
- **Phase 2 — MatchEngine** (next): event-sourced matching engine. The portfolio project. Reuses 100% of infrastructure.
- **Phase 3 — Full Trading Platform** (future): KYC, multiple symbols, charts, admin, SignalR, real market data.

**Rule adopted mid-Phase-1:** every phase ends with a working UI. Not backend-only.

Target market: Jordan fintech jobs asking for CQRS, Event Sourcing, SQL depth, JWT, React + TypeScript.

---

## STACK

### Backend
- **.NET 10** (ASP.NET Core Web API)
- **4 projects:** `WalletApp.API`, `WalletApp.Core`, `WalletApp.Data`, `WalletApp.Tests`
- **EF Core 10** + **SQL Server Express** (`.\SQLEXPRESS`, db `WalletAppDb`)
- **JWT auth** with BCrypt password hashing
- **Refresh tokens** — rotation, family revocation on reuse, silent frontend retry
- **ProblemDetails** (RFC 7807) via `IExceptionHandler`
- **API versioning** — `Asp.Versioning.Http` + `Asp.Versioning.Mvc.ApiExplorer`, URL-segment
- **Swagger UI** with Bearer auth, one document per API version
- **xUnit + FluentAssertions + Moq + WebApplicationFactory**
- **Serilog** — console + rolling file sinks, correlation IDs via LogContext
- **Rate limiting** — built-in `AddRateLimiter`, two policies
- **CORS** configured for `http://localhost:5173`

### Frontend
- **Vite 8** + **TypeScript 6** (strict)
- **React 19** — function components + hooks only
- **React Router 7**
- **React Hook Form** — form validation
- **`fetch` + custom wrapper** (no axios) — silent 401 retry via refresh token
- **React Context** — auth + refresh invalidation
- **MUI 9** — component library with responsive system
- **Inter** font via `@fontsource/inter`
- **Explicitly NOT used:** TanStack Query, Zustand, Zod. Build the small version yourself first, adopt later when you feel the pain.

### Infrastructure
- **GitHub Actions CI** — pending (last Phase 1.5 item)

---

## ARCHITECTURE

### Core principles
- **Event Sourcing** — events are truth, never updated, never deleted
- **CQRS** — write side (commands → events) separate from read side (queries → read models)
- **Write flow:** load events → rehydrate aggregate → business logic → append events → run projections
- **Read flow:** query → read model → return (NO rehydration)
- **Auth is NOT event-sourced** — Users live in a plain SQL table
- **Wallet ID = User ID.** Server reads user ID from JWT `sub` claim
- **DI lifetimes:** anything using `DbContext` is Scoped
- **API surface is versioned.** Every user-facing route is `/api/v1/...`. Infrastructure endpoints (`/health`, `/health/ready`) are not.

### Solution structure
```
WalletApp/
├── .gitignore
├── WalletApp.slnx
├── PROGRESS.md
├── README.md
├── WalletApp.API/
│   ├── Program.cs                  (has `public partial class Program { }` at bottom)
│   ├── Authorization/
│   │   └── PolicyHandlers.cs       (CanTrade, IsVerified, AccountNotFrozen)
│   ├── Exceptions/
│   │   └── GlobalExceptionHandler.cs
│   ├── Health/
│   │   └── DatabaseHealthCheck.cs
│   ├── Idempotency/
│   │   └── IdempotencyFilter.cs
│   ├── Middleware/
│   │   └── CorrelationIdMiddleware.cs
│   ├── RateLimiting/
│   │   ├── RateLimitPolicies.cs
│   │   └── RateLimitServiceCollectionExtensions.cs
│   ├── Versioning/
│   │   ├── ApiVersioningExtensions.cs
│   │   └── ConfigureSwaggerOptions.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── WalletApp.Core/
│   ├── Aggregates/Wallet.cs
│   ├── Events/ (IEvent, FundsDeposited, FundsWithdrawn)
│   ├── EventStore/ (IEventStore, ConcurrencyException)
│   ├── Exceptions/
│   │   ├── DomainException.cs
│   │   ├── ValidationException.cs
│   │   ├── NotFoundException.cs
│   │   ├── UnauthorizedException.cs
│   │   └── InsufficientFundsException.cs
│   ├── Pagination/
│   │   ├── Cursor.cs
│   │   ├── PagedResult.cs
│   │   └── StoredEvent.cs
│   ├── Auth/
│   │   ├── User.cs                 (Id, Email, PasswordHash, CreatedAt, Role, IsVerified, IsFrozen)
│   │   ├── IUserStore.cs
│   │   ├── IPasswordHasher.cs
│   │   ├── ITokenService.cs        (GenerateAccessToken, GenerateRefreshToken, HashRefreshToken)
│   │   ├── IRefreshTokenStore.cs
│   │   ├── RefreshTokenData.cs
│   │   ├── IAuthService.cs
│   │   ├── AuthService.cs
│   │   └── Requirements/
│   │       ├── CanTradeRequirement.cs
│   │       ├── IsVerifiedRequirement.cs
│   │       └── AccountNotFrozenRequirement.cs
│   ├── Projections/WalletProjection.cs
│   ├── Queries/ (GetBalanceQuery, GetBalanceQueryHandler)
│   └── ReadModels/ (WalletReadModel, IWalletReadStore)
├── WalletApp.Data/
│   ├── AppDbContext.cs
│   ├── AppDbContextFactory.cs      (design-time factory for EF CLI tools)
│   ├── Entities/
│   │   ├── EventRecord.cs
│   │   ├── WalletReadRecord.cs
│   │   ├── UserRecord.cs
│   │   ├── IdempotencyRecord.cs
│   │   └── RefreshTokenRecord.cs
│   ├── Auth/
│   │   ├── BcryptPasswordHasher.cs
│   │   ├── JwtTokenService.cs
│   │   ├── SqlUserStore.cs
│   │   └── SqlRefreshTokenStore.cs
│   ├── EventStore/SqlEventStore.cs
│   ├── ReadModels/SqlWalletReadStore.cs
│   └── Migrations/
├── WalletApp.Tests/
│   ├── Unit/ (WalletTests 15, WalletProjectionTests 6, AuthServiceTests 15)
│   └── Integration/
│       ├── CustomWebApplicationFactory.cs
│       ├── TestHelpers.cs
│       ├── AuthEndpointTests.cs
│       ├── WalletEndpointTests.cs
│       ├── AuthorizationTests.cs
│       ├── ConcurrencyTests.cs
│       └── NewEndpointsTests.cs
└── frontend/
    ├── index.html
    ├── package.json
    ├── tsconfig.json
    ├── vite.config.ts
    ├── .env.development            (VITE_API_URL=http://localhost:5138)
    └── src/
        ├── main.tsx                (ThemeProvider + CssBaseline + Inter imports)
        ├── App.tsx                 (BrowserRouter + AuthProvider + RefreshProvider + Routes)
        ├── theme.ts                (Slate & Ledger theme)
        ├── api/
        │   ├── client.ts           (fetch wrapper: JWT, idempotency key, 401 retry via refresh, ProblemDetails errors)
        │   ├── auth.ts             (register, login, me, logout — BASE = /api/v1/auth)
        │   ├── wallet.ts           (getBalance, deposit, withdraw, getTransactions — BASE = /api/v1/wallet)
        │   └── admin.ts            (getUsers, verifyUser, freezeUser — BASE = /api/v1/admin)
        ├── context/
        │   ├── AuthContext.tsx     (stores accessToken + refreshToken in localStorage)
        │   └── RefreshContext.tsx
        ├── hooks/useApi.ts
        ├── components/
        │   ├── Layout.tsx
        │   ├── ProtectedRoute.tsx
        │   ├── AdminRoute.tsx
        │   ├── BalanceCard.tsx
        │   ├── DepositForm.tsx
        │   ├── WithdrawForm.tsx
        │   └── TransactionList.tsx
        └── Pages/                  (currently capital P — standardize before CI)
            ├── LoginPage.tsx
            ├── RegisterPage.tsx
            ├── DashboardPage.tsx
            ├── AdminPage.tsx
            └── NotFoundPage.tsx
```

**Folder naming rule:** `Pages` (capital) is the current state; standardize on lowercase `pages/` before CI runs on Linux. Windows treats them as the same folder; Linux treats them as different. Be consistent so a CI run on Linux doesn't break.

---

## STEPS COMPLETED

### Backend — Phase 1
- [x] **Step 1:** Solution setup, events
- [x] **Step 2:** In-memory event store (superseded)
- [x] **Step 3:** `Wallet` aggregate
- [x] **Step 4:** CQRS read side
- [x] **Step 5:** JWT auth
- [x] **Step 6:** SQL Server persistence
- [x] **Step 7:** Swagger with Bearer auth
- [x] **Step 7.5:** Users to SQL
- [x] **Step 8:** Unit tests (30)
- [x] **Step 9:** Integration tests (12)
- [x] **Step 10:** Optimistic concurrency
  - `Version` on `Wallet` + `EventRecord`
  - Unique index `(AggregateId, Version)`
  - `IEventStore.AppendAsync(guid, IReadOnlyCollection<IEvent>, int expectedVersion)`
  - `SqlEventStore` pre-check + unique constraint catch
  - `ConcurrencyException` → 409
  - Migration `AddEventVersion` with `ROW_NUMBER()` backfill
- [x] **Step 11:** Policy-based authorization
  - `Role`, `IsVerified`, `IsFrozen` on `User` / `UserRecord`
  - Migration `AddUserPolicyFields` (Role defaults to `"User"`)
  - `role` claim in JWT; `RoleClaimType = "role"` in `TokenValidationParameters`
  - `options.MapInboundClaims = false` — the .NET 8+ replacement for the dead `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()`
  - Requirements + handlers for `CanTrade`, `IsVerified`, `AccountNotFrozen`
  - `IsAdmin` uses `RequireRole("Admin")` (no DB hit)
  - Endpoints: `POST /admin/users/{id}/verify`, `POST /admin/users/{id}/freeze`
  - Deposit → `AccountNotFrozen`; Withdraw → `CanTrade`
  - `FrameworkReference Microsoft.AspNetCore.App` on `WalletApp.Core`

### Phase 1 backend extension (added for frontend)
- [x] **B1:** `GET /auth/me`
- [x] **B2:** `GET /wallet/transactions`
- [x] **B3:** `GET /admin/users`
- [x] `IUserStore.GetAllAsync()` added
- [x] Deleted dead `InMemoryEventStore.cs` and `InMemoryUserStore.cs`
- [x] **CORS policy "Frontend"** for `http://localhost:5173`
- [x] Auth endpoints catch exceptions and return proper 400 / 401

### Phase 1 frontend
- [x] **F0:** Vite + TS + React + MUI + Inter + Slate & Ledger theme
- [x] **F1:** React Router with Layout route, empty pages, 404, `/` → `/dashboard`
- [x] **F2:** `AuthContext` + Login/Register forms + `ProtectedRoute` + `AdminRoute` + navbar
- [x] **F3:** `useApi` hook + `BalanceCard` (three-state render pattern)
- [x] **F4:** `RefreshContext` + `DepositForm`
- [x] **F5:** `WithdrawForm` — disabled if `!user.isVerified` or `user.isFrozen`; handles 403 / 409
- [x] **F6:** `TransactionList` — consumes `/wallet/transactions`
- [x] **F7:** `AdminPage` — user table + verify/freeze actions with confirmation dialog
- [x] **F8:** Polish — favicon (SVG), page title, meta tags, README, admin empty state. (Skeletons + dark mode deferred to Phase 1.5+.)

### Phase 1.5 — Wallet hardening

- [x] **ProblemDetails global exception handler.**
  - `WalletApp.Core/Exceptions/` — `DomainException` base + `ValidationException`, `NotFoundException`, `UnauthorizedException`, `InsufficientFundsException`
  - `WalletApp.API/Exceptions/GlobalExceptionHandler.cs` — maps typed exceptions to 400/404/401/409
  - `Wallet` aggregate throws typed exceptions instead of plain `Exception`
  - Removed per-endpoint try/catch from wallet + auth endpoints
  - `client.ts` reads ProblemDetails `detail`/`title`
  - `WithdrawForm` drops the 500-insufficient-funds workaround
  - `AuthService` throws typed exceptions (last piece)

- [x] **Idempotency keys on write endpoints.**
  - `Idempotency-Key` header on `/wallet/deposit` and `/wallet/withdraw`
  - Composite PK `(UserId, Key)` in new `IdempotencyRecords` table
  - `IdempotencyFilter` — an `IEndpointFilter` caching response and replaying on duplicate
  - 422 on key reuse across endpoints
  - 24h TTL column exists; cleanup job deferred to Phase 2.5 Hangfire
  - Migration `AddIdempotencyRecords`
  - Frontend generates UUID per form session, rotates only on success

- [x] **Pagination on list endpoints.**
  - Cursor-based on `/wallet/transactions` and `/admin/users`
  - Both endpoints return `{ items, nextCursor }` instead of a bare array
  - Limit clamped 1–100 (default 20)
  - Monotonic `Id` cursor for events; composite `(CreatedAt, Id)` cursor for users
  - `WalletApp.Core/Pagination/` — `Cursor`, `PagedResult<T>`, `StoredEvent`
  - Frontend uses "Load more" button; list state managed locally (not via `useApi`)

- [x] **Serilog structured logging + correlation IDs.**
  - `Serilog.AspNetCore` + `Serilog.Sinks.File` + `Serilog.Sinks.Seq` (Seq not yet enabled)
  - Console + daily-rolling file sink (`logs/walletapp-*.log`, 7-day retention)
  - `CorrelationIdMiddleware` pushes `X-Correlation-Id` (or ASP.NET `TraceIdentifier`) into Serilog `LogContext`
  - Same ID appears in ProblemDetails `traceId`, `X-Correlation-Id` response header, and every log line for the request
  - `GlobalExceptionHandler` and JWT bearer events log through `ILogger`
  - Zero `Console.WriteLine` calls left in the API
  - Seq sink is config-ready

- [x] **Health checks.**
  - `/health` — liveness. No dependency checks (predicate `_ => false`). Returns 200 as long as the process can respond to HTTP.
  - `/health/ready` — readiness. Runs `SELECT 1` against SQL Server via custom `DatabaseHealthCheck`.
  - Both return JSON with per-check status, duration, description, and error.
  - Custom check (not `AddDbContextCheck<T>`) so the SQL exception message lands in the `error` field. Verified: readiness returns 503 with populated error when SQL Server is stopped; liveness stays 200.
  - Production deployments should suppress the exception message in the response body.

- [x] **Rate limiting.**
  - Built-in `AddRateLimiter`. Two policies:
    - **Global** — keyed on user ID (fallback IP), 100 req/min
    - **Auth** — keyed on IP only, 5 req/min, applied to `/auth/login` and `/auth/register`
  - Custom 429 ProblemDetails response with `Retry-After` header
  - Config-driven via `RateLimiting` section in `appsettings.json`
  - `RateLimiting:Enabled` flag is read **per-request** (inside the partition lambda), not at registration — required so the test factory's config override takes effect after `Program.cs` has run
  - Disabled in integration tests via `RateLimiting:Enabled = false` override in `CustomWebApplicationFactory`
  - **In-memory limiter, per-process.** Multi-instance deployments need a Redis-backed store (Phase 2.5)

- [x] **API versioning.**
  - `Asp.Versioning.Http` 10.2.2 + `Asp.Versioning.Mvc.ApiExplorer` 10.2.1
  - URL-segment versioning: every endpoint now lives under `/api/v1/...`
  - Health checks stay unversioned at `/health`, `/health/ready`
  - Swagger emits one document per version via `ConfigureSwaggerOptions` reading `IApiVersionDescriptionProvider`
  - `ReportApiVersions = true` adds the `api-supported-versions` response header
  - `AssumeDefaultVersionWhenUnspecified = false` — unversioned paths 404 rather than silently falling back
  - Frontend `api/*.ts` files use a `BASE` constant per resource
  - Adding v2 later is one `MapGroup(...).MapToApiVersion(v2)` per resource

- [x] **Refresh tokens.**
  - Short-lived access token (15 min via `Jwt:AccessTokenMinutes`) + opaque refresh token (7 days via `Jwt:RefreshTokenDays`)
  - `RefreshTokens` table: `Id`, `UserId`, `TokenHash` (SHA-256, unique index), `FamilyId`, `ExpiresAt`, `CreatedAt`, `RevokedAt`, `ReplacedByTokenId`
  - Migration `AddRefreshTokens`
  - **Rotation on every refresh:** old token revoked, new one issued in the same `FamilyId`
  - **Theft detection:** reuse of a revoked token revokes the entire family
  - `POST /api/v1/auth/refresh` — takes a refresh token, returns a new access + refresh pair
  - `POST /api/v1/auth/logout` — revokes the refresh token server-side, returns 204
  - Login response shape changed: `{ accessToken, refreshToken, accessTokenExpiresAt }` (was `{ token }`)
  - Frontend `client.ts` silently retries once on 401 by calling `/refresh`; on failure clears session
  - `ClockSkew = TimeSpan.Zero` on `TokenValidationParameters` — otherwise the 5-minute default masks expiry during testing
  - Refresh endpoint deliberately NOT under the strict auth rate limiter (256-bit random token; brute-force is infeasible). Global limiter still applies.
  - Access/refresh tokens stored in `localStorage` (`accessToken`, `refreshToken`)
  - Expired refresh tokens not purged — cleanup deferred to Phase 2.5 Hangfire

**Test count: 72 passing.**

---

## STEPS REMAINING

### Phase 1.5 (continued)

- [ ] **GitHub Actions CI.** Build + test on push. Runs on Linux, so needs case-consistency pass on `frontend/src/Pages/` first. ~1 hr.

**Case-consistency fix before CI:**
- [ ] Rename `frontend/src/Pages/` → `frontend/src/pages/` (or standardize on one casing) and update all imports. Windows tolerates the mismatch; Linux does not.

### Phase 2 — MatchEngine (during the build)

Reuses everything from WalletApp. New domain:

- [ ] **Step 12:** Option B — keep WalletApp as its own portfolio piece, start fresh `MatchEngine` copying infrastructure
- [ ] **Step 13:** `OrderBook` aggregate + matching algorithm (price-time priority)
- [ ] **Step 14:** `Order` aggregate
- [ ] **Step 15:** New projections: `OrderBookProjection`, `TradeHistoryProjection`
- [ ] **Step 16:** New queries: `GetOrderBookQuery`, `GetTradeHistoryQuery`, `GetPortfolioQuery`
- [ ] **Step 17:** SignalR for live order book
- [ ] **Step 18:** React frontend for MatchEngine (same stack as WalletApp frontend)

MatchEngine-specific additions:
- [ ] SignalR real-time updates
- [ ] Recharts for price history
- [ ] Load testing the matching engine
- [ ] Performance profiling under high throughput

### Phase 2.5 — MatchEngine hardening (after MatchEngine works, before Phase 3)

- [ ] **Docker + Docker Compose.** API + SQL Server + Redis.
- [ ] **Redis caching for order books.**
- [ ] **Distributed rate limiting** (Redis-backed) so limits apply across instances.
- [ ] **Hangfire background jobs.** Settlement polling, price alerts, **idempotency record TTL cleanup** (`DELETE FROM IdempotencyRecords WHERE ExpiresAt < GETUTCDATE()`), **refresh token TTL cleanup** (`DELETE FROM RefreshTokens WHERE ExpiresAt < GETUTCDATE()`).
- [ ] **Audit log query endpoint.** Free from event sourcing.
- [ ] **CI/CD to a real environment.** Deploy pipeline.
- [ ] **Suppress exception messages in health check response body** for production.
- [ ] **Distributed refresh token revocation** — if multiple instances run, revocation in one instance must be visible to all (currently fine — the store is SQL Server, so it's already shared).

### Phase 3 — Full Trading Platform

Full feature list and hardening items live in the **HARDENING PHASES** section above. Before starting Phase 3, complete Phase 1.5 and Phase 2.5.

Withdrawal model decision is deferred to Phase 3 — see the WITHDRAWAL section for the design.

Features:
- [ ] **Withdrawal Model B (fiat off-ramp)** — see WITHDRAWAL section
- [ ] **Password reset** (if deferred from Phase 1.5)
- [ ] KYC workflow (multi-tier `VerificationLevel` instead of boolean)
- [ ] Multiple order types (stop-loss, IOC, FOK)
- [ ] Multiple symbols (BTC, ETH, AAPL)
- [ ] Candlestick charts (OHLC aggregation projection)
- [ ] Price alerts (Hangfire)
- [ ] Admin dashboard (freeze users, view all activity, audit log query)
- [ ] Free market data integration (Finnhub, Alpaca paper trading)
- [ ] Email/SMS notifications on order fills

### Deferred — Know, don't build

Mention in interviews, don't implement:

- Multi-tenancy
- Kafka / RabbitMQ event streaming
- Microservices split
- Saga pattern
- Database sharding
- Read replicas
- Polly circuit breakers
- Blue-green deploys
- Feature flags (LaunchDarkly-style)
- HTTPS termination (nginx / Caddy) — cloud providers handle it
- Password reset — needs an email provider, no portfolio value
- HTTP-only cookies for refresh tokens — localStorage is XSS-vulnerable but the cookie flow requires CSRF protection + CORS credentials + cookie config

### Framing for interviews

> "The architecture is production-grade — event sourcing with optimistic concurrency, CQRS, policy-based auth with fresh-state handlers, refresh tokens with rotation and theft detection. The operational concerns — idempotency keys, cursor pagination, structured logging with correlation IDs, health checks, rate limiting, API versioning — were added in Phase 1.5, after the wallet was working, precisely so MatchEngine could inherit them. I deliberately separated domain architecture from ops because getting event sourcing right is where projects fail, whereas the ops layer is a fixed, well-understood checklist."

---

## TESTING STRATEGY

**Test each piece right after it works, before moving on.**

### Current coverage (72 tests)

**Unit (36):** Wallet aggregate (15), WalletProjection (6), AuthService (15)

**Integration (36):**
- `AuthEndpointTests` — register, login, refresh, rotation, theft detection, logout, errors
- `WalletEndpointTests` — auth requirement, full flow, user isolation
- `AuthorizationTests` — policy paths, freeze-immediate-effect, admin workflows
- `ConcurrencyTests` — real SQL Server, concurrent append, one wins
- `NewEndpointsTests` — `/auth/me`, `/wallet/transactions` (incl. pagination), `/admin/users`

### Rules
- Unit tests for aggregates, services, projections
- Integration for endpoints via `WebApplicationFactory`
- Fakes for state, Moq for interaction
- **Don't test** getters, DTOs, framework code, trivial mappers

### Known gotcha — `CustomWebApplicationFactory`
Must remove **ALL** EF Core registrations before adding InMemory:
`DbContextOptions<T>`, `DbContextOptions`, `T`, `IDbContextOptionsConfiguration<T>`. Otherwise: "only a single database provider can be registered."

Also applies a `RateLimiting:Enabled = false` config override so the test suite isn't throttled by the auth limiter (5/min/IP shared across the whole run).

Also needs a design-time factory (`AppDbContextFactory`) because EF CLI tools can't reliably execute the API's complex `Program.cs` startup path.

---

## DATABASE

### Connection
`Server=.\SQLEXPRESS;Database=WalletAppDb;Trusted_Connection=True;TrustServerCertificate=True;`

### Tables
- **`Events`** — `Id`, `AggregateId`, `EventType`, `Data`, `Version`, `OccurredAt`. Unique index on `(AggregateId, Version)`.
- **`WalletReadModel`** — `WalletId`, `Balance`
- **`Users`** — `Id`, `Email` (unique), `PasswordHash`, `CreatedAt`, `Role`, `IsVerified`, `IsFrozen`
- **`IdempotencyRecords`** — composite PK `(UserId, Key)`; `Endpoint`, `StatusCode`, `ResponseBody`, `CreatedAt`, `ExpiresAt` (indexed for future TTL cleanup)
- **`RefreshTokens`** — `Id` (PK), `UserId` (indexed), `TokenHash` (unique index, SHA-256), `FamilyId` (indexed), `ExpiresAt`, `CreatedAt`, `RevokedAt` (nullable), `ReplacedByTokenId` (nullable, forensics)
- **`__EFMigrationsHistory`**

### Migrations applied
1. `InitialCreate`
2. `AddUsersTable`
3. `AddEventVersion` (with `ROW_NUMBER()` backfill — hand-edited)
4. `AddUserPolicyFields` (Role default `"User"` — hand-edited)
5. `AddIdempotencyRecords`
6. `AddRefreshTokens`

---

## API ENDPOINTS

**All user-facing endpoints are versioned under `/api/v1/`.** Health endpoints are unversioned.

### Public (`/api/v1`)
- `POST /api/v1/auth/register` — `{ email, password }` → `{ id, email }`
- `POST /api/v1/auth/login` — `{ email, password }` → `{ accessToken, refreshToken, accessTokenExpiresAt }` (401 on bad creds)
- `POST /api/v1/auth/refresh` — `{ refreshToken }` → new `{ accessToken, refreshToken, accessTokenExpiresAt }` (401 on invalid/revoked/expired; revokes family on reuse)
- `POST /api/v1/auth/logout` — `{ refreshToken }` → 204 (revokes token)

### Authenticated (`/api/v1`)
- `GET /api/v1/auth/me` → `{ id, email, role, isVerified, isFrozen }`
- `GET /api/v1/wallet/balance` → `{ balance }`
- `POST /api/v1/wallet/deposit?amount=100` — requires **AccountNotFrozen**; accepts `Idempotency-Key` header
- `POST /api/v1/wallet/withdraw?amount=50` — requires **CanTrade**; accepts `Idempotency-Key` header
- `GET /api/v1/wallet/transactions?cursor=<opaque>&limit=20` → `{ items: [{ type, amount, occurredAt }], nextCursor }`

### Admin (`/api/v1`)
- `POST /api/v1/admin/users/{id}/verify` — requires **IsAdmin**
- `POST /api/v1/admin/users/{id}/freeze` — requires **IsAdmin**
- `GET /api/v1/admin/users?cursor=<opaque>&limit=20` → `{ items: [{ id, email, role, isVerified, isFrozen, createdAt }], nextCursor }` — requires **IsAdmin**

### Health (unversioned, anonymous)
- `GET /health` — liveness, always 200 unless process is dead
- `GET /health/ready` — readiness, 503 when SQL Server unreachable

### Versioning headers
- Responses include `api-supported-versions: 1.0`
- Unversioned paths (`/auth/login`, `/wallet/balance`) return **404** — deliberate; `AssumeDefaultVersionWhenUnspecified = false`

### Error responses

All failures return RFC 7807 ProblemDetails:

| Exception | Status | When |
|---|---|---|
| `ValidationException` | 400 | Bad input |
| `InsufficientFundsException` | 400 | Withdrawal exceeds balance (extends `ValidationException`) |
| `NotFoundException` | 404 | Aggregate or user not found |
| `UnauthorizedException` | 401 | Missing/invalid identity, bad creds, invalid/revoked/expired refresh token |
| `ConcurrencyException` | 409 | Version conflict on append |
| Rate limiter | 429 | Too many requests (with `Retry-After`) |
| *(anything else)* | 500 | Message suppressed, full exception logged |

Every response body has a `traceId` extension matching the log's `CorrelationId`.

---

## WITHDRAWAL — design decision (deferred to Phase 3)

### Current state (Phase 1)

`POST /wallet/withdraw` is a **closed-loop ledger decrement.** The balance goes down, the event is stored, no money leaves the system. This is correct for learning the pattern. It is **not** a real withdrawal.

### The three models

**Model A — Internal ledger (current).**
Money leaves the user's balance to nowhere. Useful for: game currency, in-app rewards, internal settlement. Not useful for real money because there's no counterparty.

**Model B — Fiat off-ramp (recommended for Phase 3).**
User withdraws to their bank account via a payment processor.
- Requires: stored payment methods, KYC gate (you have `IsVerified`), asynchronous settlement, a payment processor (Stripe Payouts, Plaid ACH, Dwolla, Wise).
- New entities: `PaymentMethod`, `Withdrawal`.
- New events: `WithdrawalInitiated`, `WithdrawalCompleted`, `WithdrawalFailed`.
- New endpoint shape: `POST /wallet/withdrawals` returns **202 Accepted** with a withdrawal ID. Settlement is a background job that polls the processor and emits `Completed` or `Failed`.
- Frontend becomes: "my withdrawals" list with statuses, not a synchronous form.

**Model C — Crypto withdrawal.**
User pastes a destination wallet address, server broadcasts from a hot wallet, waits for confirmations. Requires blockchain node infrastructure, hot/cold wallet management, gas fee handling. Significantly more complex than Model B.

### Decision

**Stay on Model A for Phase 1 and Phase 2.** The withdraw endpoint is a training exercise for policy-gated money movement with concurrency safety. That's the pattern. Real settlement infrastructure is orthogonal.

**Adopt Model B when starting Phase 3.** Why B over C:
- Fiat is more universally understood by interviewers.
- No blockchain infrastructure to run.
- The asynchrony (202 + polling) is itself a valuable pattern to learn — it's how most real financial APIs work.
- Crypto can be layered on later as a third payment method type.

### What to say in interviews

> "Withdrawal to nowhere is fine for a training ledger. Real money movement means a payment processor, KYC gating, stored payment methods, and an asynchronous settlement flow. That's Phase 3. The pattern is the same — policy-gated money movement with an event-sourced audit trail — but settlement becomes a background job that polls the provider, not a synchronous response."

---

## FRONTEND DETAILS

### Design system — "Slate & Ledger"

Restrained, financial, dark-mode-ready. Neutral grays for chrome, one deep blue for actions, green/red only where money direction is involved.

**Light mode:**
| Token | Hex | Used for |
|---|---|---|
| `background.default` | `#F8FAFC` | Page background |
| `background.paper` | `#FFFFFF` | Cards, modals, navbar |
| `primary.main` | `#1E40AF` | Primary buttons, links |
| `primary.dark` | `#1E3A8A` | Button hover |
| `primary.light` | `#3B82F6` | Focus rings |
| `secondary.main` | `#475569` | Secondary buttons |
| `success.main` | `#059669` | Deposits, + amounts |
| `error.main` | `#DC2626` | Withdrawals, − amounts, frozen |
| `warning.main` | `#D97706` | Unverified status |
| `info.main` | `#0284C7` | Neutral notifications |
| `text.primary` | `#0F172A` | Headings, body |
| `text.secondary` | `#64748B` | Labels, captions |
| `divider` | `#E2E8F0` | Borders, table lines |

**Dark mode:** defined in `theme.ts` under `colorSchemes.dark`. Toggle not wired up yet.

**Typography:** Inter (400, 500, 600). Headings tight letter-spacing. Numbers use `fontVariantNumeric: "tabular-nums"`.

**Shape:** `borderRadius: 10` (MUI default is 4). Flat buttons (`disableElevation: true`). Sentence case (`textTransform: "none"`). Card shadow `0 1px 3px rgba(0,0,0,0.06)`.

**MUI v6+ rule:** all layout props go in `sx`, not as direct props. `alignItems`, `justifyContent`, `flexGrow`, `mt`, `p`, etc. — none of them are top-level props anymore. Only component-specific props (Stack's `direction`/`spacing`, Button's `variant`/`color`, etc.) stay as props.

### Auth flow
1. Login → server returns `{ accessToken, refreshToken, accessTokenExpiresAt }`
2. `AuthContext` stores `accessToken` and `refreshToken` in `localStorage`
3. On app load, if `accessToken` exists, `GET /api/v1/auth/me` fetches user
4. `client.ts` reads `accessToken` from `localStorage` and injects `Authorization: Bearer <token>`
5. **On 401:** `client.ts` calls `/api/v1/auth/refresh` once, rotates both tokens, retries the original request
6. **On refresh failure:** both tokens cleared, `AuthError` thrown, `AuthContext` redirects to `/login`
7. **Error handling:**
   - **401** (unrecoverable) → clear session, redirect to `/login`
   - **403** → show toast (do NOT log out) — used for policy violations
   - **5xx / network error** → keep session, show "server unreachable" screen with Retry + Log out
   - **409** → show "another operation in progress" toast
   - **429** → show ProblemDetails `detail` ("Rate limit exceeded. Please slow down and retry later.")
   - **400** → show ProblemDetails `detail` text

### Logout flow
1. User clicks logout
2. `AuthContext.logout()` fires `authApi.logout(refreshToken)` (fire-and-forget)
3. `localStorage` cleared, state reset, redirect to `/login`
4. Server revokes the refresh token; subsequent refresh attempts 401

### API versioning on the frontend
Each file under `src/api/` has a `const BASE = "/api/v1/<resource>"` and concatenates paths onto it. When v2 exists and v1 deprecates, change one constant per file.

### Three-layer enforcement
| Layer | Where | Enforces |
|---|---|---|
| Backend policy | `CanTrade` handler | Source of truth |
| Route guard | `ProtectedRoute` / `AdminRoute` | Navigation |
| UI affordance | Disabled withdraw button | User experience |

All three must exist. If you only did backend, users see buttons that 403. If you only did frontend, `curl` bypasses everything.

### RefreshContext pattern
A shared version counter. Deposit form increments it after success; `BalanceCard` includes `version` in its `useApi` deps. When version changes, the fetch re-runs. ~20 lines, replaces TanStack Query's `invalidateQueries` for this small app. `TransactionList` uses the same pattern but resets to page 1 (list state is managed locally).

### Env
`.env.development` at `frontend/`:
```
VITE_API_URL=http://localhost:5138
```
Read in code as `import.meta.env.VITE_API_URL`.

---

## COMMON COMMANDS

### Backend
```powershell
dotnet build
dotnet watch run --project WalletApp.API
dotnet test
dotnet test --filter "FullyQualifiedName~Unit"
dotnet test --filter "FullyQualifiedName~Integration"

dotnet ef migrations add MigrationName --project WalletApp.Data --startup-project WalletApp.API
dotnet ef database update --project WalletApp.Data --startup-project WalletApp.API
dotnet ef migrations list --project WalletApp.Data --startup-project WalletApp.API
```

### Frontend
```powershell
cd frontend
npm run dev                    # dev server on :5173
npm run build                  # production build
npm install <package>
```

### Health checks (manual)
```powershell
curl.exe http://localhost:5138/health -i
curl.exe http://localhost:5138/health/ready -i

# Stop SQL Server and re-check
Stop-Service 'MSSQL$SQLEXPRESS'
Start-Service 'MSSQL$SQLEXPRESS'
```

### Rate limit smoke test
```powershell
$body = '{\"email\":\"ratelimit@test.com\",\"password\":\"password123\"}'

1..7 | ForEach-Object {
    curl.exe -s -o $null -w "attempt $_ status=%{http_code}`n" `
      -X POST "http://localhost:5138/api/v1/auth/login" `
      -H "Content-Type: application/json" `
      -d $body
}
# Expect 401 for attempts 1-5, 429 for attempts 6-7
```

### Versioning smoke test
```powershell
# Old unversioned path → 404
curl.exe http://localhost:5138/auth/login -i -X POST `
  -H "Content-Type: application/json" `
  -d '{\"email\":\"x@x.com\",\"password\":\"x\"}'

# New versioned path → 401 (bad creds) or 200 (valid)
curl.exe http://localhost:5138/api/v1/auth/login -i -X POST `
  -H "Content-Type: application/json" `
  -d '{\"email\":\"x@x.com\",\"password\":\"x\"}'

# Supported-versions header
curl.exe http://localhost:5138/api/v1/auth/me -i `
  -H "Authorization: Bearer $token"
# look for: api-supported-versions: 1.0
```

### Refresh token smoke test
```powershell
# 1. Login → capture accessToken and refreshToken
curl.exe -X POST "http://localhost:5138/api/v1/auth/login" `
  -H "Content-Type: application/json" `
  -d '{\"email\":\"<user>\",\"password\":\"<pass>\"}'

# 2. Refresh → new pair
curl.exe -X POST "http://localhost:5138/api/v1/auth/refresh" `
  -H "Content-Type: application/json" `
  -d '{\"refreshToken\":\"<the-refresh-token>\"}'

# 3. Replay the original refresh token → 401 (theft detection)
# 4. Try the rotated token → also 401 (family revoked)
# 5. Logout with a fresh token → 204, then refresh → 401
```

### Access-token expiry test (in-browser)
1. Set `"AccessTokenMinutes": 1` in `appsettings.json`
2. Restart the API
3. Log out, log in fresh
4. Do a manual request (hard reload or deposit)
5. Wait 65 seconds
6. Do another manual request → expect 401 → silent refresh → 200 (three-request pattern in DevTools Network)
7. Restore `"AccessTokenMinutes": 15`

### Both servers running at once
- Terminal 1: `dotnet run --project WalletApp.API` (port 5138)
- Terminal 2: `cd frontend && npm run dev` (port 5173)

---

## KEY CONCEPTS LEARNED

### Event Sourcing
- Events are immutable facts. Append-only. Current state = replay.
- Deletion = appending a "deleted" event.
- Event log is source of truth; read models are derived and disposable.

### CQRS
- Write: commands → handlers → aggregates → events
- Read: events → projections → read models → queries
- Queries never touch the event store.

### Optimistic Concurrency
- `Version` = the number of events that have happened to an aggregate.
- Unique index `(AggregateId, Version)` — DB enforces it, not code.
- On conflict: DB rejects the second write → `ConcurrencyException` → 409.
- Client is expected to reload and retry.
- **Not just for negative balances.** A version conflict blocks any write based on stale data, even a legal one. The server can't tell the difference.

### Aggregates
- Hold business rules. Produce events.
- State changes only through `Apply` (private).
- Rehydrated on every command.
- `private set` on state.

### Policies (authorization)
- Named rules registered in `Program.cs` via `AddPolicy`.
- Custom handlers implement `IAuthorizationHandler<TRequirement>`.
- Handlers fetch **fresh user state** from `IUserStore` — not the token. A freeze must take effect on the next request, not the next login.
- `RoleClaimType = "role"` + `MapInboundClaims = false` makes JWT role claims work in .NET 8+.
- Policies ≠ roles ≠ permissions. Policy = a named rule. Role = identity. Permission = capability. Yours are policy + role.

### Idempotency keys
- Client sends `Idempotency-Key: <uuid>` header on non-idempotent writes.
- Server stores `(UserId, Key) → response` and replays on duplicate.
- Key generated once per form session, rotated only on success. **Never regenerate per click** — that defeats the whole mechanism.
- 422 on reuse across different endpoints.
- TTL column, cleanup deferred to a background job.

### Cursor pagination
- Client passes opaque `?cursor=`; server translates to a `WHERE` clause.
- Correct under concurrent inserts (unlike `OFFSET`).
- Monotonic `Id` for events (unique); composite `(CreatedAt, Id)` for users (CreatedAt isn't unique).
- Fetch `limit + 1` to know if there's more; return `nextCursor = null` on the last page.
- **Offset pagination is wrong for append-only data.** Duplicates appear as new rows shift the window.

### Structured logging
- `Log.Information("Deposit {Amount} for {UserId}", amount, userId)` — Serilog captures the fields, not the interpolated string.
- `LogContext.PushProperty("CorrelationId", id)` flows the ID into every log in the request scope.
- Sinks are configuration, not code. Changing destination (console → file → Seq → Datadog) is a config-file change.

### Health checks
- **Liveness** (`/health`) = process alive, no dependency checks. Restart signal.
- **Readiness** (`/health/ready`) = can serve traffic, checks DB. Route signal.
- Conflating them → restart loops during transient DB blips.
- Custom check (not `AddDbContextCheck<T>`) so the exception message surfaces in the response.

### Rate limiting
- **Global limiter** keyed on user ID (fallback IP) protects against a single client hammering the API.
- **Auth limiter** keyed on IP only protects `/auth/login` from brute force. Keying on user ID would let an attacker rotate usernames to bypass.
- Fixed window is simple but allows burst at boundaries. Sliding window / token bucket fix that — MatchEngine may want token bucket for order submission.
- In-memory, per-process. Multi-instance needs Redis-backed store (`IRateLimiterStore`).
- Config values that can be overridden by tests/env vars must be read **lazily**, at request time, not at registration.

### API versioning
- URL-segment versioning is the simplest, most debuggable scheme. One reader, one obvious URL shape.
- `MapGroup("/api/v{version:apiVersion}/<resource>")` + `.MapToApiVersion(v1)` — the prefix is defined once per resource.
- Health checks stay unversioned — infrastructure endpoints don't have API contracts.
- `AssumeDefaultVersionWhenUnspecified = false` — unversioned paths 404, not silently fall back.
- One Swagger document per version, discovered via `IApiVersionDescriptionProvider`.
- Adding v2 later is additive: same group helper, new `.MapToApiVersion(v2)`.

### Refresh tokens
- **Access token:** short-lived JWT (15 min). Baked with user claims. Stateless.
- **Refresh token:** opaque 256-bit random string (base64url). Stored server-side as SHA-256 hash. Never plaintext.
- **Rotation:** every successful refresh revokes the old token and issues a new one in the same family.
- **Family:** all tokens issued from one login share a `FamilyId`.
- **Theft detection:** reuse of a revoked token revokes the entire family.
- **Why SHA-256 not BCrypt:** refresh tokens are high-entropy random values. Brute force is infeasible, so BCrypt's slowness adds nothing.
- **Why not HTTP-only cookies:** XSS-vulnerable in localStorage, but cookie flow requires CSRF + CORS credentials + cookie config. Portfolio-appropriate tradeoff.
- **Why `/refresh` skips the strict auth rate limiter:** the refresh token itself is the credential (256-bit random). Global limiter still applies.

### React patterns
- **`useApi<T>` hook** — `{ data, loading, error, refetch }`. Replaces `useState + useEffect` boilerplate for every fetch.
- **Three-state render** — loading → spinner; error → alert; data → card. Write this every time.
- **Context for global state** — token + user in `AuthContext`. Refresh counter in `RefreshContext`.
- **`ProtectedRoute`** — checks `token` and `loading`. Redirects if no token. Shows error if unreachable. Renders `<Outlet />` on success.
- **401 vs 403 vs 429 vs network error** — four different behaviors. Do not conflate them.
- **Paginated lists** — `useApi` replaces `data` on refetch; paged lists need append semantics, so `TransactionList` and `AdminPage` manage state directly with a `nextCursor` in state.
- **Versioned API base** — one `const BASE` per `api/*.ts` file. Change once if the version bumps.
- **Silent refresh in `client.ts`** — one-shot retry on 401. Guarded by a `retried` flag so it can't loop. Auth endpoints (`/login`, `/register`, `/refresh`, `/logout`) are excluded from retry to prevent recursion.

### MUI v6+ gotchas
- All layout props go in `sx`. `alignItems`, `justifyContent`, `flexGrow`, `mt`, etc. are not top-level.
- `CssBaseline` must be inside `<ThemeProvider>` and applied once.
- Fonts via `@fontsource/<font>/<weight>.css` imports (one per weight).

---

## GOTCHAS LEARNED THE HARD WAY

1. **JWT claim mapping (.NET 8+):** `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` is dead code. Use `options.MapInboundClaims = false;` on `JwtBearerOptions`. Symptom: JWT validates fine, `IsAuthenticated == true`, but `context.User.FindFirst("sub")` returns null and every policy returns 403.

2. **`RoleClaimType = "role"`** required for `RequireRole("Admin")` to work with a custom JWT. Without it, `RequireRole` reads `ClaimTypes.Role` (a long URI), finds nothing, and 403s everyone.

3. **`FrameworkReference Microsoft.AspNetCore.App`** on any class library that uses `IAuthorizationRequirement`. Without it, `CanTradeRequirement : IAuthorizationRequirement` fails to compile in Core.

4. **EF migrations on populated tables:** hand-edit any migration that adds a non-nullable column with a unique index. Insert a `migrationBuilder.Sql("...")` backfill between `AddColumn` and `CreateIndex`. EF can't see your data.

5. **Casing on Windows:** `Pages` and `pages` are the same folder on Windows, different on Linux. Be consistent in imports. When you rename a folder case-only, Windows won't fire a file-watcher event; VS Code's TS server keeps the old path until you run `TypeScript: Restart TS Server`.

6. **React Hook Form + MUI v6:** layout props go in `sx`, not as props.

7. **CORS:** `app.UseCors("Frontend")` must come before `UseAuthentication` and `UseAuthorization`. Symptom: browser console "blocked by CORS policy" but backend logs nothing.

8. **`GlobalExceptionHandler` belongs in the API project, not Core.** It uses `IExceptionHandler`, `HttpContext`, `StatusCodes`, `ProblemDetails` — all ASP.NET Core types. Core is a class library; the Web SDK's implicit usings don't apply. Rule of thumb: Core declares *what* went wrong, API decides *how* to tell the client.

9. **Stale test expectations after status-code changes.** When an endpoint's status code changes, grep the integration tests for the old code. This bit twice: `AuthEndpointTests` (500 → 400/401 for auth) and `WalletEndpointTests.Withdraw_MoreThanBalance_ReturnsError` (500 → 400 once ProblemDetails landed). Same remedy each time: update the assertion.

10. **AuthService threw plain `Exception` until ProblemDetails landed.** Register/login had endpoint-level try/catch to compensate. Once `GlobalExceptionHandler` existed, the try/catch became redundant. Rule: no endpoint-level try/catch, no plain `throw new Exception`.

11. **Build artifacts already tracked.** Adding `.gitignore` alone doesn't untrack files. Must `git rm -r --cached . -q`, then `git add .`, then verify with `git ls-files | Select-String -Pattern "bin/|obj/|node_modules/"`. The "849 files changed" commit was mostly build-artifact deletions.

12. **`AddDbContextCheck<T>` returns unhealthy with no detail.** It calls `CanConnectAsync()`, which returns `false` silently on failure — no exception, no description. Custom `IHealthCheck` that runs `SELECT 1` throws on failure, and the exception lands in the response's `error` field. Worth 20 lines.

13. **ASP.NET Core config is layered and late-bound.** Reading `builder.Configuration` during `Program.cs` sees only the sources registered so far — test factory overrides, environment variables, and user secrets apply later, during `builder.Build()`. Symptom: an integration test sets a config value but the app doesn't honor it, because the read already happened. Rule: if a value can be overridden by a source you don't control (feature flags, kill switches, environment-specific behavior), read it **lazily — at request time, not registration time.** This bit the rate limiter `Enabled` flag.

14. **`AssumeDefaultVersionWhenUnspecified = false` is the deliberate choice.** Setting it to `true` makes unversioned paths silently resolve to the default version. Convenient at first, painful later: you can never tell which version a client is actually using, and you can never remove the fallback without breaking them. Force the version to be explicit — every request must name its version. 404 on unversioned paths is correct behavior, not a bug.

15. **Moving code around in `Program.cs` can orphan local functions.** When restructuring endpoint mapping into version groups, the `GetUserId` and `WriteHealthResponse` static local functions lived *inside* the section that got replaced. My first pass replaced them without re-declaring them, producing 7 CS0103 errors. Fix: keep helpers in their own section before all endpoint groups. Rule of thumb: if a refactor moves code, list every identifier used later and confirm it still exists.

16. **`app.DescribeApiVersions()` silences AV0027.** Minimal API version groups created with `NewApiVersionSet()` trigger the `AV0027` warning unless the versioning library is told to describe versions. One line above `NewApiVersionSet()` clears it. Not required at runtime, but the warning is noisy and the fix is trivial.

17. **JWT clock skew defaults to 5 minutes.** `TokenValidationParameters.ClockSkew` is 5 minutes by default, so a token that "expired" 4 minutes ago is still accepted. Symptom: testing token expiry with a short-lived access token (e.g. `AccessTokenMinutes: 1`) shows no 401 for ~6 minutes, so the refresh flow never fires and you think it's broken. Fix: `ClockSkew = TimeSpan.Zero` (or a small buffer like 30 seconds) explicitly in the config. Left unset, every test of the refresh flow gives a false negative.

18. **EF CLI tools can't reliably run the API's startup.** `dotnet ef migrations add` tries to construct `AppDbContext` by starting the API project and reading its DI container. Complex `Program.cs` files — with Serilog bootstrap, try/catch around `app.Run()`, and versioning setup — break this. Fix: add an `AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>` with the connection string hardcoded. Design-time only; not used in production. Symptom: `Unable to resolve service for type 'DbContextOptions<AppDbContext>' while attempting to activate 'AppDbContext'`.

19. **DI can't resolve primitive constructor parameters.** `AddScoped<IAuthService, AuthService>()` fails if `AuthService` has an `int refreshTokenDays` parameter — DI has no `int` registered. Use a factory lambda: `AddScoped<IAuthService>(sp => new AuthService(..., refreshTokenDays))`. Symptom: `Unable to resolve service for type 'System.Int32'`.

20. **Don't look up what you just saved.** `IssueTokensAsync` originally created a refresh record, saved it, then `RefreshAsync` had to re-query by hash to get the record's `Id` for linking the old token to its successor. Two problems: extra DB round-trip, and it breaks under mocking (mock `SaveAsync` doesn't populate anything so the subsequent lookup returns null). Fix: have the helper return `(AuthTokens, Guid RefreshTokenId)`. Rule of thumb: if a function creates something, it should return the identifier.

---

## WHAT CARRIES TO MATCHENGINE

### Backend — copy as-is
- `IEventStore` / `SqlEventStore`
- `IWalletReadStore` pattern, projection pattern, query handler pattern
- 3-project structure + test project
- JWT auth + refresh tokens + `Users` + `RefreshTokens` tables + migrations
- Swagger config + per-version documents
- Testing setup (xUnit + WebApplicationFactory + Moq)
- `public partial class Program { }` in API
- `CustomWebApplicationFactory`
- Concurrency primitives (`Version`, unique index, `ConcurrencyException`)
- Policy system + handlers + `CanTrade` + `MapInboundClaims = false`
- `GlobalExceptionHandler` + typed `DomainException` hierarchy
- `IdempotencyFilter` + `IdempotencyRecords` table (MatchEngine needs it *more*)
- `Cursor` / `PagedResult<T>` / `StoredEvent` + cursor pagination pattern
- Serilog config + `CorrelationIdMiddleware`
- Health checks (`/health`, `/health/ready` + custom `DatabaseHealthCheck`)
- Rate limiting policies + config-driven `Enabled` flag pattern
- API versioning (`ApiVersioningExtensions`, `ConfigureSwaggerOptions`, `MapGroup` pattern)
- Refresh token flow (`ITokenService`, `IRefreshTokenStore`, `AuthService` rotation + theft detection)
- `AppDbContextFactory` for EF CLI tools

### Backend — replace domain
- `Wallet` → `OrderBook` + `Order` + `Wallet`
- Add events: `OrderPlaced`, `OrderPartiallyFilled`, `OrderFullyFilled`, `OrderCancelled`, `TradeExecuted`
- Add projections: `OrderBookProjection`, `TradeHistoryProjection`
- Add queries: `GetOrderBookQuery`, `GetTradeHistoryQuery`, `GetPortfolioQuery`

### Backend — new
- Matching algorithm (price-time priority)
- `SortedDictionary<decimal, Queue<Order>>` for bids/asks
- Multiple aggregates per command
- SignalR real-time updates
- Token-bucket rate limiting for order submission (fixed window allows bursts)
- Possible `NewVersionedApi("MarketData")` split if market-data endpoints evolve on a different cadence

### Frontend — copy as-is
- Vite + TS + MUI + React Router + React Hook Form
- `theme.ts` (Slate & Ledger)
- `client.ts` (with silent 401 retry via refresh), `useApi`, `AuthContext`, `RefreshContext`
- `ProtectedRoute`, `AdminRoute`, `Layout`
- Login / Register / NotFound pages
- Three-state render pattern
- Paginated-list pattern (local state + Load more button)
- Versioned API base (`const BASE = "/api/v1/..."`)

### Frontend — new
- Order book display (table, real-time via SignalR)
- Order placement form (market / limit selector)
- Portfolio view
- Charts (add Recharts — justified)
- SignalR replaces `RefreshContext` for live data

---

## HARDENING PHASES (between main phases)

The enterprise items are not all deferred to the end. They're distributed
between phases, at the point where the current phase has proven the concept
and the next phase needs a stronger foundation.

### Phase 1.5 — Wallet hardening (after F8, before MatchEngine)

Reason for placement: MatchEngine copies WalletApp's infrastructure. Every
hardening item done here gets copied for free. Skipping it means copying a
weaker base.

- [x] **ProblemDetails global exception handler.**
- [x] **Idempotency keys on write endpoints.**
- [x] **Pagination on list endpoints.**
- [x] **Serilog structured logging + correlation IDs.**
- [x] **Health checks.**
- [x] **Rate limiting.**
- [x] **API versioning.** `/api/v1/...` via `Asp.Versioning.Http` + `.Mvc.ApiExplorer`.
- [x] **Refresh tokens.** Access + refresh, rotation, family revocation on reuse, silent frontend retry.
- [ ] **Password reset.** Email-token flow. **Deferred to Phase 3** (needs provider).
- [ ] **GitHub Actions CI.** Build + test on push. Runs last.

### Phase 2 — MatchEngine (during the build)

Reuses everything from WalletApp. New additions specific to MatchEngine:

- [ ] **SignalR** for real-time order book updates (inherent to MatchEngine)
- [ ] **Recharts** on the frontend for price history
- [ ] **Load testing the matching engine.** Concurrent order stress test.
- [ ] **Performance profiling.** Matching algorithm under high throughput.

### Phase 2.5 — MatchEngine hardening (after MatchEngine works, before Phase 3)

- [ ] **Docker + Docker Compose.** API + SQL Server + Redis on one command.
- [ ] **Redis caching for order books.** Hot read models.
- [ ] **Distributed rate limiting** (Redis-backed) so limits apply across instances.
- [ ] **Hangfire background jobs.** Settlement polling, price alerts, idempotency record TTL cleanup, refresh token TTL cleanup.
- [ ] **Audit log query endpoint.** Free from event sourcing.
- [ ] **CI/CD to a real environment.** Deploy pipeline.
- [ ] **Suppress exception messages in health check response body.**

### Phase 3 — Full Trading Platform

Trading-specific features. Withdrawal design is deferred to this phase (see WITHDRAWAL section).

- [ ] **Withdrawal Model B (fiat off-ramp).** See separate section.
- [ ] **Password reset** (if deferred from Phase 1.5)
- [ ] KYC workflow (multi-tier `VerificationLevel` instead of boolean)
- [ ] Multiple order types (stop-loss, IOC, FOK)
- [ ] Multiple symbols (BTC, ETH, AAPL)
- [ ] Candlestick charts (OHLC aggregation projection)
- [ ] Price alerts (Hangfire)
- [ ] Admin dashboard (freeze users, view all activity, audit log query)
- [ ] Free market data integration (Finnhub, Alpaca paper trading)
- [ ] Email/SMS notifications on order fills

### Deferred — Know, don't build

Mention these in interviews, don't implement them:

- Multi-tenancy
- Kafka / RabbitMQ event streaming
- Microservices split
- Saga pattern
- Database sharding
- Read replicas
- Polly circuit breakers
- Blue-green deploys
- Feature flags (LaunchDarkly-style)
- HTTPS termination (nginx / Caddy) — cloud providers handle it

### Framing for interviews

> "The architecture is production-grade — event sourcing with optimistic
> concurrency, CQRS, policy-based auth with fresh-state handlers, refresh
> tokens with rotation and theft detection. The operational concerns —
> idempotency keys, cursor pagination, structured logging with correlation
> IDs, health checks, rate limiting, API versioning — were added in Phase 1.5,
> after the wallet was working, precisely so MatchEngine could inherit them. I
> deliberately separated domain architecture from ops because getting event
> sourcing right is where projects fail, whereas the ops layer is a fixed,
> well-understood checklist."

---

## ENVIRONMENT

- .NET SDK: **.NET 10**
- Node.js: **24 LTS**, npm **11.x**
- VS Code extensions: C# Dev Kit, C# (Microsoft), mssql, REST Client, ESLint
- SQL Server Express (`MSSQL$SQLEXPRESS`)
- SSMS connects to `.\SQLEXPRESS` with Windows Auth
- API: `http://localhost:5138` (check terminal — may change)
- Frontend: `http://localhost:5173`

---

## CURRENT STATE

- **72 backend tests passing**
- **Backend:** Phase 1 complete + Phase 1.5 (ProblemDetails, idempotency, pagination, Serilog, health checks, rate limiting, API versioning, refresh tokens)
- **Frontend:** F0–F8 complete + refresh token flow — auth works end-to-end, balance displays, deposits/withdrawals update without refresh, transactions paginate, admin table works with verify/freeze. Silent 401 retry via refresh token verified. All API calls under `/api/v1/`.
- **Phase 1.5:** 8 of 9 items done. Only CI remaining. Password reset deferred to Phase 3.
- **Repo hygiene:** `.gitignore` in place, build artifacts untracked, README up to date, PROGRESS.md tracked.

---

## NEXT ACTION

**Two small items before Phase 2.**

1. **Case-consistency fix on the frontend.** Rename `frontend/src/Pages/` → `frontend/src/pages/` and update every import. ~15 min. This is a hard prerequisite for CI — Linux treats the two folder names as different and the build will fail on GitHub's runners.

2. **GitHub Actions CI.** Add `.github/workflows/ci.yml` running `dotnet test` and `npm run build` on push to main. ConcurrencyTests (`RequiresSql` tagged) get skipped on CI — they need real SQL Server. ~1 hr.

After both → **Phase 2 — MatchEngine.** Copy WalletApp infrastructure, build `OrderBook` + `Order` aggregates, price-time priority matching, SignalR, React frontend.

---

## IF STARTING A NEW CHAT

Paste this entire file and say:

> "Continue from where this PROGRESS.md leaves off. Current state: 72 backend tests passing. Phase 1 complete (WalletApp backend + frontend). Phase 1.5 nearly complete: ProblemDetails, idempotency keys, cursor pagination, Serilog + correlation IDs, health checks, rate limiting, API versioning, and refresh tokens are all done. Only GitHub Actions CI remains, and it's blocked on the frontend `Pages` → `pages` case-consistency fix. Next step is the case-consistency fix, then CI. After that we start Phase 2 — MatchEngine. Give me exact files to create/modify, plus what to test after."

Be specific about wanting:
- Exact file paths
- Full file contents (not snippets), or precise diffs against pasted files
- Exact commands
- How to test after
- What NOT to touch (the 72 tests should keep passing)

If a file shape is unknown, the assistant should ask you to paste it rather than assume.