# WalletApp → MatchEngine → Trading Platform

## THE VISION (read this first)

Three phases:

- **Phase 1 — WalletApp** (nearly complete): event-sourced wallet. Learn event sourcing + CQRS on a small domain. Has a working React frontend.
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
- **Swagger UI** with Bearer auth
- **xUnit + FluentAssertions + Moq + WebApplicationFactory**
- **CORS** configured for `http://localhost:5173`

### Frontend (added Phase 1)
- **Vite + TypeScript (strict)**
- **React 18** — function components + hooks only
- **React Router v6**
- **React Hook Form** — form validation
- **`fetch` + custom wrapper** (no axios)
- **React Context** — auth + refresh invalidation
- **MUI** — component library with responsive system
- **Inter** font via `@fontsource/inter`
- **Explicitly NOT used:** TanStack Query, Zustand, Zod. Build the small version yourself first, adopt later when you feel the pain.

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

### Solution structure
```
WalletApp/
├── WalletApp.API/
│   ├── Program.cs                  (has `public partial class Program { }` at bottom)
│   ├── Authorization/
│   │   └── PolicyHandlers.cs       (CanTrade, IsVerified, AccountNotFrozen)
│   ├── appsettings.json
│   └── appsettings.Development.json
├── WalletApp.Core/
│   ├── Aggregates/Wallet.cs
│   ├── Events/ (IEvent, FundsDeposited, FundsWithdrawn)
│   ├── EventStore/ (IEventStore, ConcurrencyException)
│   ├── Auth/
│   │   ├── User.cs                 (Id, Email, PasswordHash, CreatedAt, Role, IsVerified, IsFrozen)
│   │   ├── IUserStore.cs
│   │   ├── IPasswordHasher.cs
│   │   ├── ITokenService.cs
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
│   ├── Entities/ (EventRecord, WalletReadRecord, UserRecord)
│   ├── Auth/ (BcryptPasswordHasher, JwtTokenService, SqlUserStore)
│   ├── EventStore/SqlEventStore.cs
│   ├── ReadModels/SqlWalletReadStore.cs
│   └── Migrations/
├── WalletApp.Tests/
│   ├── Unit/ (WalletTests 15, WalletProjectionTests 6, AuthServiceTests 9)
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
        │   ├── client.ts           (fetch wrapper: JWT, 401 vs 403 vs network)
        │   ├── auth.ts             (register, login, me)
        │   └── wallet.ts           (getBalance, deposit, getTransactions)
        ├── context/
        │   ├── AuthContext.tsx
        │   └── RefreshContext.tsx
        ├── hooks/useApi.ts
        ├── components/
        │   ├── Layout.tsx
        │   ├── ProtectedRoute.tsx
        │   ├── AdminRoute.tsx
        │   ├── BalanceCard.tsx
        │   └── DepositForm.tsx
        └── pages/                  (lowercase or Capital consistently — pick one)
            ├── LoginPage.tsx
            ├── RegisterPage.tsx
            ├── DashboardPage.tsx
            ├── AdminPage.tsx
            └── NotFoundPage.tsx
```

**Folder naming rule:** pick `pages` (lowercase) or `Pages` (capital) and use the same in every import. Windows treats them as the same folder; Linux treats them as different. Be consistent so a CI run on Linux doesn't break.

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
  - **Fix:** `options.MapInboundClaims = false` — the .NET 8+ replacement for the dead `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()`
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
  - `AuthContext` only clears token on **401**, keeps session on network errors
  - `ProtectedRoute` shows error screen with Retry + Log out when unreachable
- [x] **F3:** `useApi` hook + `BalanceCard` (three-state render pattern)
- [x] **F4:** `RefreshContext` + `DepositForm`
  - Deposit updates balance without page refresh
  - Validation for negative / zero / empty amounts
  - Frozen user handling

**Test count: 61 passing.**

---

## STEPS REMAINING

### Frontend — finish Phase 1
- [ ] **F5:** `WithdrawForm` — disabled if `!user.isVerified` or `user.isFrozen`; handles 403 / 409
- [ ] **F6:** `TransactionList` — consumes `/wallet/transactions`
- [ ] **F7:** `AdminPage` — user table + verify/freeze actions
- [ ] **F8:** Polish — loading skeletons, empty states, favicon, README, maybe dark mode toggle

### Phase 1.5 — Wallet hardening (after F8, before MatchEngine)

Every item here gets copied to MatchEngine for free. Skipping it means copying a weaker base.

- [ ] **ProblemDetails global exception handler.** Replaces per-endpoint try/catch. `ValidationException`, `NotFoundException`, `AuthenticationException` map to 400/404/401 centrally. Wallet currently returns 500 on `Insufficient funds`.
- [ ] **Idempotency keys on write endpoints.** Client sends `Idempotency-Key` header; server dedupes. Prevents double-charge on retry.
- [ ] **Pagination on list endpoints.** `/wallet/transactions` and `/admin/users`. Cursor-based: `?afterId=...&limit=50`.
- [ ] **Rate limiting.** `AddRateLimiter` built-in. Per-user + per-IP.
- [ ] **Serilog structured logging + correlation IDs.**
- [ ] **Health checks.** `/health` (liveness), `/health/ready` (readiness).
- [ ] **API versioning.** `/api/v1/...` via `Asp.Versioning.Mvc`.
- [ ] **Refresh tokens.** Users currently re-login every hour.
- [ ] **Password reset.** Email-token flow.
- [ ] **GitHub Actions CI.** Build + test on push.

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
- [ ] **Hangfire background jobs.** Settlement polling, price alerts.
- [ ] **Audit log query endpoint.** Free from event sourcing.
- [ ] **CI/CD to a real environment.** Deploy pipeline.

### Phase 3 — Full Trading Platform

Full feature list and hardening items live in the **HARDENING PHASES** section above. Before starting Phase 3, complete Phase 1.5 and Phase 2.5.

Withdrawal model decision is deferred to Phase 3 — see the WITHDRAWAL section for the design.

Features:
- [ ] **Withdrawal Model B (fiat off-ramp)** — see WITHDRAWAL section
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

### Framing for interviews

> "The architecture is production-grade — event sourcing with optimistic concurrency, CQRS, policy-based auth with fresh-state handlers. The operational concerns — idempotency keys, structured logging, health checks, CI/CD — were added in Phase 1.5, after the wallet was working, precisely so MatchEngine could inherit them. I deliberately separated domain architecture from ops because getting event sourcing right is where projects fail, whereas the ops layer is a fixed, well-understood checklist."

---

## TESTING STRATEGY

**Test each piece right after it works, before moving on.**

### Current coverage (61 tests)

**Unit (30):** Wallet aggregate, WalletProjection, AuthService

**Integration (31):**
- `AuthEndpointTests` — register, login, errors
- `WalletEndpointTests` — auth requirement, full flow, user isolation
- `AuthorizationTests` — policy paths, freeze-immediate-effect, admin workflows
- `ConcurrencyTests` — real SQL Server, concurrent append, one wins
- `NewEndpointsTests` — `/auth/me`, `/wallet/transactions`, `/admin/users`

### Rules
- Unit tests for aggregates, services, projections
- Integration for endpoints via `WebApplicationFactory`
- Fakes for state, Moq for interaction
- **Don't test** getters, DTOs, framework code, trivial mappers

### Known gotcha — `CustomWebApplicationFactory`
Must remove **ALL** EF Core registrations before adding InMemory:
`DbContextOptions<T>`, `DbContextOptions`, `T`, `IDbContextOptionsConfiguration<T>`. Otherwise: "only a single database provider can be registered."

---

## DATABASE

### Connection
`Server=.\SQLEXPRESS;Database=WalletAppDb;Trusted_Connection=True;TrustServerCertificate=True;`

### Tables
- **`Events`** — `Id`, `AggregateId`, `EventType`, `Data`, `Version`, `OccurredAt`. Unique index on `(AggregateId, Version)`.
- **`WalletReadModel`** — `WalletId`, `Balance`
- **`Users`** — `Id`, `Email` (unique), `PasswordHash`, `CreatedAt`, `Role`, `IsVerified`, `IsFrozen`
- **`__EFMigrationsHistory`**

### Migrations applied
1. `InitialCreate`
2. `AddUsersTable`
3. `AddEventVersion` (with `ROW_NUMBER()` backfill — hand-edited)
4. `AddUserPolicyFields` (Role default `"User"` — hand-edited)

---

## API ENDPOINTS

### Public
- `POST /auth/register` — `{ email, password }` → `{ id, email }`
- `POST /auth/login` — `{ email, password }` → `{ token }` (401 on bad creds)

### Authenticated
- `GET /auth/me` → `{ id, email, role, isVerified, isFrozen }`
- `GET /wallet/balance` → `{ balance }`
- `POST /wallet/deposit?amount=100` — requires **AccountNotFrozen**
- `POST /wallet/withdraw?amount=50` — requires **CanTrade** (verified + not frozen)
- `GET /wallet/transactions` → `[{ type, amount, occurredAt }]` (newest first)

### Admin
- `POST /admin/users/{id}/verify` — requires **IsAdmin**
- `POST /admin/users/{id}/freeze` — requires **IsAdmin**
- `GET /admin/users` → `[{ id, email, role, isVerified, isFrozen, createdAt }]` — requires **IsAdmin**

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
1. Login → store token in `localStorage` via `AuthContext`
2. On app load, if token exists, `GET /auth/me` fetches user
3. `client.ts` reads token from `localStorage` and injects `Authorization: Bearer <token>`
4. **Error handling:**
   - **401** → clear token, redirect to `/login`
   - **403** → show toast (do NOT log out) — used for policy violations
   - **5xx / network error** → keep session, show "server unreachable" screen with Retry + Log out
   - **409** → show "another operation in progress" toast (F5)

### Three-layer enforcement
| Layer | Where | Enforces |
|---|---|---|
| Backend policy | `CanTrade` handler | Source of truth |
| Route guard | `ProtectedRoute` / `AdminRoute` | Navigation |
| UI affordance | Disabled withdraw button | User experience |

All three must exist. If you only did backend, users see buttons that 403. If you only did frontend, `curl` bypasses everything.

### RefreshContext pattern
A shared version counter. Deposit form increments it after success; `BalanceCard` includes `version` in its `useApi` deps. When version changes, the fetch re-runs. ~20 lines, replaces TanStack Query's `invalidateQueries` for this small app.

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

### React patterns
- **`useApi<T>` hook** — `{ data, loading, error, refetch }`. Replaces `useState + useEffect` boilerplate for every fetch.
- **Three-state render** — loading → spinner; error → alert; data → card. Write this every time.
- **Context for global state** — token + user in `AuthContext`. Refresh counter in `RefreshContext`.
- **`ProtectedRoute`** — checks `token` and `loading`. Redirects if no token. Shows error if unreachable. Renders `<Outlet />` on success.
- **401 vs 403 vs network error** — three different behaviors. Do not conflate them.

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

8. **`Insufficient funds` returns 500** (should be 400). Band-aided on auth endpoints only. Full fix is the ProblemDetails global handler (Phase 1.5).

9. **`InMemoryEventStore.cs` and `InMemoryUserStore.cs`:** deleted. Dead code from earlier steps. If you ever need an in-memory store for tests, write it in `WalletApp.Tests`, not `WalletApp.Data`.

---

## WHAT CARRIES TO MATCHENGINE

### Backend — copy as-is
- `IEventStore` / `SqlEventStore`
- `IWalletReadStore` pattern, projection pattern, query handler pattern
- 3-project structure + test project
- JWT auth + `Users` table + migrations
- Swagger config
- Testing setup (xUnit + WebApplicationFactory + Moq)
- `public partial class Program { }` in API
- `CustomWebApplicationFactory`
- Concurrency primitives (`Version`, unique index, `ConcurrencyException`)
- Policy system + handlers + `CanTrade` + `MapInboundClaims = false`

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

### Frontend — copy as-is
- Vite + TS + MUI + React Router + React Hook Form
- `theme.ts` (Slate & Ledger)
- `client.ts`, `useApi`, `AuthContext`, `RefreshContext`
- `ProtectedRoute`, `AdminRoute`, `Layout`
- Login / Register / NotFound pages
- Three-state render pattern

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

- [ ] **ProblemDetails global exception handler.** Replaces per-endpoint
      try/catch. Business exceptions (`ValidationException`,
      `NotFoundException`, `AuthenticationException`) map to 400/404/401
      centrally. Wallet endpoints currently return 500 on `Insufficient
      funds` (should be 400).
- [ ] **Idempotency keys on write endpoints.** Client sends
      `Idempotency-Key` header; server dedupes on it.
- [ ] **Pagination on list endpoints.** `/wallet/transactions` and
      `/admin/users`. Cursor-based: `?afterId=...&limit=50`.
- [ ] **Rate limiting.** `AddRateLimiter` built-in. Per-user + per-IP.
- [ ] **Serilog structured logging + correlation IDs.**
- [ ] **Health checks.** `/health` (liveness), `/health/ready` (readiness).
- [ ] **API versioning.** `/api/v1/...` via `Asp.Versioning.Mvc`.
- [ ] **Refresh tokens.** Users currently re-login every hour.
- [ ] **Password reset.** Email-token flow.
- [ ] **GitHub Actions CI.** Build + test on push.

A 3-5 day chunk that turns WalletApp from "portfolio project" into "portfolio
project a senior engineer would nod at."

### Phase 2 — MatchEngine (during the build)

Reuses everything from WalletApp. New additions specific to MatchEngine:

- [ ] **SignalR** for real-time order book updates (inherent to MatchEngine)
- [ ] **Recharts** on the frontend for price history
- [ ] **Load testing the matching engine.** Concurrent order stress test.
- [ ] **Performance profiling.** Matching algorithm under high throughput.

### Phase 2.5 — MatchEngine hardening (after MatchEngine works, before Phase 3)

- [ ] **Docker + Docker Compose.** API + SQL Server + Redis on one command.
- [ ] **Redis caching for order books.** Hot read models.
- [ ] **Hangfire background jobs.** Settlement polling, price alerts.
- [ ] **Audit log query endpoint.** Free from event sourcing — the data is
      already there, just expose it.
- [ ] **CI/CD to a real environment.** Deploy pipeline, not just tests.

### Phase 3 — Full Trading Platform

Trading-specific features. Withdrawal design is deferred to this phase (see
WITHDRAWAL section).

- [ ] **Withdrawal Model B (fiat off-ramp).** See separate section.
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
- HTTPS termination (nginx / Caddy) — needed for real deploy, but cloud
  providers handle it for you

### Framing for interviews

> "The architecture is production-grade — event sourcing with optimistic
> concurrency, CQRS, policy-based auth with fresh-state handlers. The
> operational concerns — idempotency keys, structured logging, health checks,
> CI/CD — were added in Phase 1.5, after the wallet was working, precisely so
> MatchEngine could inherit them. I deliberately separated domain architecture
> from ops because getting event sourcing right is where projects fail,
> whereas the ops layer is a fixed, well-understood checklist."

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

- **61 backend tests passing**
- **Backend:** complete through Step 11 + 3 extension endpoints for the frontend
- **Frontend:** F0–F4 done — auth works end-to-end, balance displays, deposits update without refresh, error handling distinguishes 401 / 403 / network failure
- **Not yet built:** withdraw form, transaction list, admin page

---

## NEXT ACTION

**F5 — WithdrawForm.**

Key things this step covers:
- Disable button when `!user.isVerified` or `user.isFrozen`
- Show a helpful reason (banner or tooltip)
- Handle 403 (policy rejection — should be prevented by UI, but defensive)
- Handle 409 (concurrency conflict — user double-clicked)
- Handle 400 (insufficient funds — currently returns 500 until Phase 1.5)
- Refresh balance after success

After F5 → F6 (TransactionList) → F7 (AdminPage) → F8 (polish). Then Phase 1.5.

---

## IF STARTING A NEW CHAT

Paste this entire file and say:

> "Continue from where this PROGRESS.md leaves off. Current state: 61 backend tests passing. Frontend F0–F4 done — auth flow works, balance displays, deposits update live via RefreshContext. Next step is F5 — WithdrawForm, with disabled-when-unverified logic and 403/409 handling. Give me exact files to create/modify, plus what to test after."

Be specific about wanting:
- Exact file paths
- Full file contents (not snippets), or precise diffs against pasted files
- Exact commands
- How to test after
- What NOT to touch (the 61 tests should keep passing)

If a file shape is unknown, the assistant should ask you to paste it rather than assume.