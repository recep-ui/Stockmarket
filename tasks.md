# BIST Quant Scanner – Master Task Checklist

This document tracks every granular development step required to implement the complete BIST Quant Scanner platform. As tasks are executed, they are marked `[x]`.

---

## Phase 0: Planning & Documentation
- [x] Analyze comprehensive platform requirements and principles
- [x] Inspect local workspace, environment dependencies (.NET 10 SDK, Node.js v22, MSSQL 2025/2022)
- [x] Create system architecture document (`architecture.md`)
- [x] Create detailed phased implementation plan (`implementation.md`)
- [x] Create granular master checklist (`tasks.md`)

---

## Phase 1: Foundation & Solution Scaffolding
- [x] Create .NET solution file `BistQuant.sln`
- [x] Create `BistQuant.Domain` class library project (POCOs only, zero external dependencies)
- [x] Create `BistQuant.Application` class library project (DTOs, Interfaces, Service contracts)
- [x] Create `BistQuant.Infrastructure` class library project (EF Core, SQL Server, Redis, Providers)
- [x] Create `BistQuant.API` ASP.NET Core Web API project
- [x] Create `BistQuant.Worker` background service project
- [x] Create `BistQuant.Domain.Tests` xUnit project
- [x] Create `BistQuant.Application.Tests` xUnit project
- [x] Create `BistQuant.IntegrationTests` xUnit project
- [x] Add project references adhering strictly to Clean Architecture
- [x] Install required NuGet packages:
  - EF Core SqlServer, EF Core Tools, EF Core Design
  - Serilog.AspNetCore, Serilog.Sinks.Console, Serilog.Sinks.File
  - FluentValidation.AspNetCore
  - Microsoft.AspNetCore.Authentication.JwtBearer
  - StackExchange.Redis
  - Hangfire.Core, Hangfire.SqlServer, Hangfire.AspNetCore
  - Swashbuckle.AspNetCore
- [x] Configure Serilog structured logging in `BistQuant.API`
- [x] Implement `GlobalExceptionHandlingMiddleware` for standard RFC 7807 problem details
- [x] Configure `BistQuantDbContext` with Entity Framework Core
- [x] Configure Health Checks (`/health/live`, `/health/ready`) checking MSSQL connectivity and data freshness
- [x] Setup Swagger/OpenAPI with JWT Bearer security definitions (development gated)
- [x] Verify `dotnet build` succeeds across all projects with zero warnings/errors

---

## Phase 2: Market Data Layer & Ingestion
- [x] Implement `Market` entity and configuration
- [x] Implement `Symbol` entity with ticker, sector, industry, active status
- [x] Implement `PriceBar` entity with OHLCV fields and composite indexing
- [x] Create and apply initial EF Core migrations (`InitialCreate`) for Market Data entities
- [x] Define `IMarketDataProvider` interface (`GetSymbolsAsync`, `GetHistoricalBarsAsync`, `GetLatestBarAsync`)
- [x] Implement `MockMarketDataProvider` for synthetic data generation
- [x] Implement `CsvMarketDataProvider` for bulk historical bar ingestion
- [x] Implement `HistoricalDataSeeder` generating realistic OHLCV for BIST 30 symbols across Daily, 1H, and 15M timeframes
- [x] Implement `SymbolsController` (`GET /api/symbols`, `GET /api/symbols/{symbol}`)
- [x] Implement `MarketDataController` (`GET /api/market-data/{symbol}/history`, `GET /api/market-data/{symbol}/latest`)
- [x] Implement `AdminController` with `POST /api/admin/import/market-data` for CSV uploads (Admin role protected)
- [x] Write unit tests for CSV parsing and OHLCV constraint validation (`High >= Low`, `Volume >= 0`)

---

## Phase 3: Technical Analysis & Quantitative Indicators
- [x] Implement `EmaCalculator` for configurable period Exponential Moving Averages (20, 50, 100, 200)
- [x] Implement `SmaCalculator` for Simple Moving Averages (20, 50, 200)
- [x] Implement `RsiCalculator` (Wilder's 14-period Relative Strength Index)
- [x] Implement `MacdCalculator` (MACD 12/26/9 line, signal line, histogram)
- [x] Implement `AtrCalculator` (14-period Average True Range)
- [x] Implement `AdxCalculator` (14-period Average Directional Index with +DI and -DI)
- [x] Implement `StochasticCalculator` (%K, %D 14/3/3 Stochastic Oscillator)
- [x] Implement `SuperTrendCalculator` (Period 10, Multiplier 3.0 trailing band calculation)
- [x] Implement `BollingerBandsCalculator` (Period 20, 2.0 Standard Deviations)
- [x] Implement `VolumeIndicators` (Volume MA20, Volume Surge Ratio, On-Balance Volume with delta evaluation)
- [x] Implement algorithmic Support / Resistance detector (swing highs/lows and local extrema)
- [x] Implement Breakout detector (20-day high/low breakout with volume confirmation)
- [x] Implement `IndicatorSnapshot` domain entity and database configuration
- [x] Implement unified `TechnicalAnalysisService` orchestrating all calculators
- [x] Expose `GET /api/analysis/{symbol}/technical`
- [x] Write comprehensive unit tests comparing indicator outputs against verified benchmarks

---

## Phase 4: Quantitative Scoring Engine (0–100 Scale)
- [x] Define `TechnicalScores` model with Trend, Momentum, Volume, Structure sub-scores
- [x] Create `IScoringEngine` and `ScoringEngine` implementation
- [x] Implement Trend scoring (max 40 pts: Close > EMA20, EMA20 > EMA50, EMA50 > EMA200, SuperTrend BUY, ADX > 20)
- [x] Implement Momentum scoring (max 30 pts: RSI 50–65, MACD > Signal, Bullish MACD Cross, Stochastic Bullish)
- [x] Implement Volume scoring (max 15 pts: Volume > 1.2x Avg20, Volume > 1.5x Avg20, OBV rising)
- [x] Implement Price Structure scoring (max 15 pts: Resistance Breakout, Higher High, Higher Low)
- [x] Externalize scoring weights into `appsettings.json` under `Scoring`
- [x] Eliminate free points for missing data and cap score at exactly 100
- [x] Write unit tests verifying total score arithmetic and boundary condition handling (0–100 range)

---

## Phase 5: Signal Classification & Explainable Reasons
- [x] Create `SignalType` enum (`StrongSell`, `Sell`, `Weak`, `Watch`, `BuyCandidate`, `Buy`, `StrongBuy`)
- [x] Create `Signal` domain entity and EF Core configuration
- [x] Create `SignalReason` domain entity storing Code, Title, Description, ScoreContribution, Indicator, Value
- [x] Implement `SignalEngine`:
  - Active Sell/StrongSell semantics requiring bearish evidence (lack of buy evidence returns Weak)
  - Auditable reason generator for each score contributor
  - Automated ATR-based and Support-based Stop Loss calculation with lower bounds
  - Automated Take Profit 1 and Take Profit 2 calculations above entry price
  - Risk / Reward Ratio calculation with division-by-zero protection
  - Timeframe expiration handling
- [x] Implement `GET /api/analysis/{symbol}/signals` endpoint
- [x] Implement `GET /api/analysis/{symbol}/reasons` endpoint
- [x] Write unit tests for signal classification thresholds and risk/reward formulas

---

## Phase 6: Market Scanner & Background Processing
- [x] Implement `MarketScannerService` to scan, evaluate, and rank the entire BIST universe
- [x] Wire `AlertEngine.ProcessAlertsForSignalAsync` on signal generation
- [x] Implement multi-criteria filtering (Signal tier, minScore, sector, timeframe, RSI range, volume ratio)
- [x] Implement sorting by Score, Volume, 24h Change, or Signal Time
- [x] Implement server-side pagination for scanner results
- [x] Setup background scanner jobs via `IHostedService` / Hangfire with configurable intervals (15m, 1h, Daily)
- [x] Implement Redis / In-Memory caching for rapid scanner queries
- [x] Implement `ScannerController` (`GET /api/scanner`, `GET /api/scanner/overview`, `POST /api/scanner/run`)
- [x] Write integration test verifying market scanner execution and ranking accuracy

---

## Phase 7: Interactive Frontend Dashboard & Stock Detail
- [x] Initialize Next.js 16 / React 19 frontend with TypeScript and Vanilla/Tailwind CSS
- [x] Remove mock data fallbacks in production (`NEXT_PUBLIC_USE_MOCK_DATA !== 'true'`)
- [x] Build shared navigation components: Sidebar, TopBar, Engine Status Bar (no fake 'Piyasa Açık' claims)
- [x] Connect Dashboard (`/dashboard`) to real ASP.NET Core `/api/scanner/overview`
- [x] Connect Screener (`/scanner`) to real ASP.NET Core `/api/scanner`
- [x] Connect Stock Detail (`/stocks/[symbol]`) to real ASP.NET Core `/api/market-data`, `/api/analysis`
- [x] Build unified `StateFeedback` components: `LoadingSkeleton`, `ErrorBanner` (with retry), `EmptyState`

---

## Phase 8: Strategy Engine & Strategy Lab
- [x] Create `Strategy` and `StrategyRule` domain entities
- [x] Implement `IStrategyEngine` for dynamic rule parsing and execution:
  - Added support for `CrossAbove`, `CrossBelow`, `Between` operators
  - Supported indicators: ADX, SuperTrend, Stochastic, VolumeRatio, Breakout
  - Throws `NotSupportedException` on invalid operator
- [x] Seed pre-defined institutional strategies
- [x] Implement `StrategiesController` (`GET /api/strategies`, `POST`, `PUT`, `DELETE`)
- [x] Connect Strategy Lab UI (`/strategies`) to real REST endpoints with loading, error, and empty states

---

## Phase 9: Institutional Backtest Engine & Analytics
- [x] Create `BacktestRun`, `BacktestTrade`, and `BacktestResult` entities
- [x] Implement `BacktestEngine`:
  - Strict cash accounting (Buy deducts positionCost + commission, Sell adds proceeds - commission)
  - Chronological multi-symbol timeline loop with capital competition
  - Strict look-ahead bias elimination (signal on bar $T$ close executes on bar $T+1$ open)
  - Realistic commission deduction (0.15%) and slippage modeling (0.10%)
  - Bar-by-bar High/Low monitoring against Stop Loss and Take Profit
  - Honest quantitative metrics computation: Sharpe and Sortino ratios return null when trades < 3 or variance == 0
  - Equity curve and drawdown curve generation
- [x] Implement `BacktestController` (`POST /api/backtests`, `GET /api/backtests/{id}`, `GET /api/backtests/{id}/trades`)
- [x] Connect Backtest UI (`/backtests`) to real REST endpoints with live symbol/strategy picker and trade log
- [x] Write unit tests verifying cash conservation and trade accounting invariants

---

## Phase 10: Multi-Channel Alert Engine & Telegram Integration
- [x] Create `AlertSubscription` and `AlertNotification` domain entities
- [x] Implement `IAlertEngine` and `INotificationProvider` interfaces
- [x] Implement channel isolation: Telegram alerts to Telegram only, InApp alerts to InApp only
- [x] Implement condition parity: `ScoreThreshold`, `StrongBuy`, `Buy`, `PriceBreakout`, `SupportBreakout`, `RsiExtreme`
- [x] Fail-closed behavior for unsupported alert conditions
- [x] User-claim filtering for subscriptions and notifications
- [x] Implement `AlertsController` (`GET /api/alerts`, `POST`, `DELETE`, `GET /api/alerts/notifications`)
- [x] Connect Alerts UI (`/alerts`) to real REST endpoints with loading, error, and empty states

---

## Phase 11: Paper Trading Simulation Engine
- [x] Create `PaperPortfolio`, `PaperPosition`, `PaperOrder`, and `PaperTrade` domain entities
- [x] Implement `IPaperTradingService`:
  - Enforce user claim extraction from JWT
  - Verify portfolio ownership (403 Forbidden on foreign portfolio)
  - Idempotency via `ClientOrderId` check
  - Limit order condition check (Buy requires marketPrice <= limitPrice; Sell requires marketPrice >= limitPrice)
  - Reject orders with stale or missing market prices
  - Persist `PaperOrder` with status `Filled`
- [x] Implement `PaperTradingController` (`GET /api/paper-portfolios`, `GET /positions`, `GET /trades`, `POST /orders`)
- [x] Connect Paper Trading UI (`/paper-trading`) to real REST endpoints with order execution and history tabs
- [x] Write integration tests for portfolio accounting and trade executions

---

## Phase 12: Watchlist MVP
- [x] Create `Watchlist` and `WatchlistItem` domain entities
- [x] Create `IWatchlistService` and `WatchlistService`
- [x] Implement `WatchlistsController` (`GET /api/watchlists`, `POST`, `POST /{id}/items`, `DELETE /{id}/items/{symbolId}`)
- [x] Enforce user ownership on watchlists (404/403 for unauthorized access)
- [x] Build frontend Watchlist screen (`/watchlists`) with CRUD, score gauges, and quick navigation

---

## Phase 13: Security, Authentication & Rate Limiting
- [x] Implement password hashing (PBKDF2) and JWT token issuance
- [x] Eliminate `[FromQuery] long userId = 1` across all controllers; extract claims strictly from JWT
- [x] Secure sensitive endpoints with `[Authorize]` and Admin endpoints with `[Authorize(Roles = "Admin")]`
- [x] Configure Rate Limiting middleware on endpoints
- [x] Seed default demo user in `DatabaseInitializer` (Development only)
- [x] Frontend JWT token caching in `localStorage` and automatic Bearer attachment

---

## Phase 14: Production Packaging, Docker & Network Isolation
- [x] Remove host port mapping for internal services (MSSQL 1433, Redis 6379, API 5000, Frontend 3000) in production compose
- [x] Ensure only Nginx is published on ports 80 and 443
- [x] Remove hardcoded passwords/secrets from `docker-compose.yml` and `docker-compose.prod.yml` (enforce via `:?`)
- [x] Provide `docker-compose.override.yml.example` for optional local dev port bindings
- [x] Update `.env.example` with secure templates and clear production warnings
- [x] Generate EF Core initial migration `InitialCreate` and replace `EnsureCreatedAsync` with `MigrateAsync`
- [x] Verify full release build (`dotnet test BistQuant.slnx -c Release`: 54/54 passed) and clean Next.js Turbopack build

---

## Phase 15: Final Integration & Contract Correction Sprint
- [x] Dual-provider EF Core migrations:
  - Implemented `ProviderSpecificMigrationsAssembly` for runtime provider resolution
  - Generated SQLite migrations in `Migrations/Sqlite/`
  - Generated SQL Server migrations in `Migrations/SqlServer/`
  - Applied and verified against running SQL Server container on port 1433
- [x] Frontend / Backend Contract Alignment:
  - Unified enum serialization with `[JsonConverter(typeof(JsonStringEnumConverter))]` across all platform and domain enums
  - Standardized Timeframe enum values (`Timeframe.Daily = 250`)
  - Aligned Backtest DTOs (`TotalReturn`, `MaxDrawdown`, `NetPnL`, `ReturnPercent`, `EquityCurve.Date`)
  - Aligned Paper Trading DTOs (`type` instead of `orderType`, `portfolioValue`)
  - Aligned Alerts DTOs (`symbol` ticker string, `NotificationChannel`)
  - Aligned Watchlists DTOs (`symbol` ticker in `AddWatchlistItemRequest`, `ticker` and `currentPrice` in `WatchlistItemDto`)
  - Aligned Strategies DTOs (`strategyType`, `timeframe`, `rules`)
- [x] Production Authentication & Security:
  - Built dedicated `/login` and `/register` frontend pages with modern typography and validation
  - Restricted demo login strictly to `NEXT_PUBLIC_DEMO_MODE=true` & `ASPNETCORE_ENVIRONMENT=Development`
  - Removed JWT secret fallback values; enforced production fail-fast on missing secret
  - Hardened CORS: disabled `AllowAnyOrigin()` in production, strictly reading from `Cors:AllowedOrigins`
  - Added user menu and dynamic logout flow in `Navbar.tsx`
- [x] Strategy & Indicator Parity:
  - Implemented shared `IndicatorCalculators.CalculateSnapshots` covering all 12 indicators (EMA20/50/100/200, RSI, MACD, ATR, ADX, SuperTrend, Stochastic, OBV, VolumeRatio, Support, Resistance, Breakout)
  - Refactored `TechnicalAnalysisService` and `BacktestEngine` to reuse identical calculation pipeline
  - Created `StrategyEvaluationPipeline` (`IndicatorEngine -> StrategyEngine -> Score/Signal Engine`)
- [x] Mathematical Logic Corrections:
  - Fixed Crossover evaluation: `prevLeft <= prevRight && currLeft > currRight` (returns false if no prior data)
  - Fixed Breakout evaluation: resistance strictly calculated on prior bars (`Take(Count - 1).TakeLast(20)`), eliminating lookahead bias
  - Annualized Return in backtests calculated as true CAGR: `(finalEquity / initialEquity) ^ (365 / days) - 1`
  - Gap-through stop loss orders executed at bar Open when gap occurs
- [x] Paper Trading Simulation Hardening:
  - Added unique database index on `PortfolioId + ClientOrderId`
  - Limit order fills execute at `marketPrice` when conditions are met
  - Entry commission preserved in position cost basis (`AveragePrice`)
  - Orders rejected if market price is missing or older than 7 days
- [x] Health Checks & Fail-Closed Readiness:
  - Added `RedisHealthCheck` and `WorkerScanHealthCheck`
  - `/health/ready` returns HTTP 503 when dependencies are degraded or worker is stale
- [x] Repository Hygiene:
  - Added root `.gitignore` excluding `bin/`, `obj/`, `node_modules/`, `.next/`, `.env*`, `*.db`, `Logs/`, `TestResults/`
- [x] Automated Verification:
  - Created `ContractIntegrationTests.cs` testing all 12 core flows against live test server
  - Created `StrategyParityTests.cs` proving 100% parity between live scanner and backtest
  - [x] Verified full solution test run: 60/60 tests passed (3 Domain, 26 Application, 31 Integration)
  - [x] Verified Next.js 16 Turbopack production build: 13/13 pages compiled with 0 errors

---

## Phase 16: Pre-Live Data Hardening & Security Audit Sprint
- [x] Secret Sanitization & Rotation:
  - Created `docs/SECURITY_SECRET_ROTATION.md` detailing rotation procedures for SQL Server, Redis, JWT, Telegram, and Demo user
  - Sanitized `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`, and `Worker/appsettings.json`
  - Removed plaintext SA password fallback from `DependencyInjection.cs`
  - Removed hardcoded JWT key fallback from `JwtService.cs` and enforced fail-fast startup in `Program.cs`
- [x] Deterministic Strategy & Scanner Parity:
  - Unified evaluation logic between live scanner (`MarketScannerService`) and backtester (`BacktestEngine`) via `IStrategyEvaluationPipeline`
  - Replaced ad-hoc in-memory assertions with `RealScannerBacktestParityTests.cs` using real database candles and DI service resolution
- [x] Alert Engine & Telegram Hardening:
  - Introduced `NotificationDeliveryResult` with explicit `Success`, `Channel`, `MessageId`, and `ErrorMessage`
  - Updated `AlertEngine.cs` to ensure `LastTriggeredAt` is strictly updated only upon confirmed notification success
  - Disabled silent simulation in `TelegramNotificationProvider`; simulation allowed only when `Telegram:SimulationMode = true`
- [x] Worker Heartbeat & Closed-Candle Scheduling:
  - Created `WorkerHeartbeat` entity, DbSet, and EF Core configurations across SQLite and SQL Server
  - Implemented `IMarketScanScheduler` with calendar-aware closed candle boundaries for M15, H1, and Daily timeframes
  - Updated `Worker.cs` to record execution metrics, duration, symbol count, and health heartbeats
- [x] Market Data Freshness Policy:
  - Implemented `IMarketDataFreshnessPolicy` with per-timeframe fail-closed thresholds (M1: 3m, M5: 15m, M15: 45m, H1: 3h, Daily: 4d)
  - Integrated freshness validation into `SignalEngine`, `PaperTradingService`, and `MarketDataFreshnessHealthCheck`
- [x] Health Checks False-Positive Elimination:
  - Hardened `RedisHealthCheck`, `WorkerScanHealthCheck`, and `MarketDataFreshnessHealthCheck`
  - `/health/ready` accurately reflects real operational readiness across all symbols and dependencies
- [x] Paper Auto-Trading Hardening:
  - Enforced signal expiration check (`ExpiresAt`), active symbol check (`Symbol.IsActive`), and portfolio cash limits
  - Enforced deterministic `ClientOrderId` (`AUTO-{portfolioId}-{signalId}`) with database-level duplicate prevention
- [x] Strategy Ownership & Access Control:
  - Added `UserId` and `IsSystem` columns to `Strategies` table
  - Enforced ownership authorization rules in `StrategyEngine` and `StrategiesController`
  - Protected system strategies from deletion
- [x] Production Auth Hardening:
  - Documented production hardening path in `docs/AUTH_PRODUCTION_HARDENING.md`
  - Added email, password, and display name validation in `AuthService`
  - Enforced active account checks (`IsActive`) on login
- [x] Dual-Database Migrations:
  - Created `20260908081135_HardeningUpdates` for SQLite and `20260908081224_HardeningUpdates` for SQL Server
  - Maintained provider-specific model snapshots in `BistQuant.Infrastructure`
- [x] CI/CD Pipeline & Audit:
  - Added `ci/ci.yml` (ready to place into `.github/workflows/ci.yml` with workflow-scoped token) with backend build/test, frontend lint/build, vulnerability audit, and secret scanning
  - Validated Docker Compose with `.env.example`
- [x] Test Suite & Build Verification:
  - 88/88 solution tests passing (3 Domain, 51 Application, 34 Integration)
  - Next.js 16 Turbopack production build: 13/13 routes compiled with 0 errors
  - ESLint: 0 errors
