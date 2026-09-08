# BIST Quant Scanner – Implementation Roadmap & Phased Execution Plan

This document outlines the step-by-step implementation guide for building the **BIST Quant Scanner** platform. Each phase is designed to deliver a verifiable, production-ready, and independently functional increment of the system.

---

## Phase 0: Architecture, Planning & Environment Verification

### Goal
Establish architectural integrity, define data models, verify local runtime environments (.NET 10 SDK, Node.js v22, MSSQL Server), and prepare foundational documentation.

### Backend Changes
* None (planning only).
### Database Changes
* Verify MSSQL 2025/2022 instance accessibility on `localhost:1433`.
### Frontend Changes
* None.
### Tests
* Environment validation commands (`dotnet --version`, `node -v`, connectivity test).
### Definition of Done
* `architecture.md`, `implementation.md`, and `tasks.md` exist and fully detail every component.

---

## Phase 1: Foundation & Solution Scaffolding

### Goal
Scaffold the multi-project .NET Clean Architecture solution (`Domain`, `Application`, `Infrastructure`, `API`, `Worker`, and unit test projects), configure Serilog structured logging, setup Swagger/OpenAPI, wire EF Core with MSSQL, and establish standard error-handling middleware.

### Backend Changes
* Create .NET solution `BistQuant.sln`.
* Scaffold `BistQuant.Domain`, `BistQuant.Application`, `BistQuant.Infrastructure`, `BistQuant.API`, `BistQuant.Worker`.
* Scaffold `BistQuant.Domain.Tests`, `BistQuant.Application.Tests`, `BistQuant.IntegrationTests`.
* Configure `BistQuantDbContext` with EF Core SqlServer.
* Add `GlobalExceptionHandlingMiddleware` producing RFC 7807 problem details.
* Configure Serilog with Console and Rolling File sinks.
* Add health check endpoint (`/health`) verifying database and memory status.
* Configure Swagger UI with JWT Bearer security schemes.

### Database Changes
* Initial EF Core migration creating database `BistQuantDb`.
* Create `Markets`, `Symbols`, and `Users` initial tables.

### Frontend Changes
* Initialize Next.js project with TypeScript, Tailwind CSS, and standard UI primitives.
* Establish modern dark terminal theme and base API client.

### Tests
* Integration test verifying `/health` returns HTTP 200 OK.
* Unit test verifying `GlobalExceptionHandlingMiddleware` formats errors correctly.

### Definition of Done
* `dotnet build` succeeds with zero errors.
* Database migrates successfully against MSSQL.
* `/swagger` renders in browser.

---

## Phase 2: Market Data Layer, OHLCV Ingestion & Seed Data

### Goal
Implement the data ingestion pipeline, market abstraction interfaces (`IMarketDataProvider`), CSV data importer, and realistic seed data for top BIST 30 equities.

### Backend Changes
* Define domain entities: `Market`, `Symbol`, `PriceBar`.
* Implement `IMarketDataProvider` interface with `MockMarketDataProvider`, `CsvMarketDataProvider`, and extensible `LiveBistMarketDataProvider`.
* Implement `HistoricalDataSeeder` generating realistic OHLCV bars (geometric Brownian motion with realistic volatility and trends) for BIST 30 symbols across Daily, 1-Hour, and 15-Minute timeframes.
* Implement `AdminController` with `POST /api/admin/import/csv` to upload custom historical data.
* Implement `SymbolsController` (`GET /api/symbols`, `GET /api/symbols/{ticker}`) and `MarketDataController` (`GET /api/market-data/{ticker}/history`).

### Database Changes
* Add `PriceBars` table with composite unique index `(SymbolId, Timeframe, Timestamp DESC)`.
* Populate initial seed: BIST market, top BIST 30 symbols (THYAO, ASELS, TUPRS, TOASO, SISE, EREGL, KCHOL, SAHOL, BIMAS, FROTO, etc.).

### Frontend Changes
* Add Symbols view and basic market overview table.
* Connect API client to fetch symbols and historical bars.

### Tests
* Unit tests for CSV parser validating header formats, date parsing, and price constraints (`High >= Low`, `Volume >= 0`).
* Data validation tests preventing duplicate candles.

### Definition of Done
* `GET /api/symbols` returns seeded BIST stocks.
* `GET /api/market-data/THYAO/history?timeframe=Daily` returns OHLCV series.

---

## Phase 3: Technical Analysis & Quantitative Indicator Engine

### Goal
Build a deterministic, mathematically sound technical indicator library calculating all core metrics without third-party black-box dependencies.

### Backend Changes
* Create `ITechnicalAnalysisService` and specialized calculators:
  * `EmaCalculator` (EMA20, 50, 100, 200)
  * `SmaCalculator` (SMA20, 50, 200)
  * `RsiCalculator` (RSI14 using Wilder's smoothing)
  * `MacdCalculator` (MACD 12/26/9 line, signal, histogram)
  * `AtrCalculator` (ATR14)
  * `AdxCalculator` (ADX14, +DI, -DI)
  * `SuperTrendCalculator` (Period 10, Multiplier 3.0)
  * `BollingerBandsCalculator` (Period 20, StdDev 2.0)
  * `VolumeIndicators` (OBV, Volume MA20, Volume Ratio)
  * `StructureIndicators` (Support/Resistance levels, Swing Highs/Lows, 20-day breakout detection)
* Implement `IndicatorSnapshot` model and snapshot caching.
* Expose `GET /api/analysis/{symbol}/technical`.

### Database Changes
* Create `IndicatorSnapshots` table with indices on `(SymbolId, Timeframe, Timestamp DESC)`.

### Frontend Changes
* Integrate TradingView Lightweight Charts canvas component.
* Render candlestick charts with toggleable overlay lines (EMA20, EMA50, EMA200, Bollinger Bands) and sub-panes (Volume, RSI, MACD).

### Tests
* Rigorous unit tests comparing calculated indicator values against pre-computed textbook/TradingView reference series for RSI, EMA, MACD, and SuperTrend.

### Definition of Done
* All mathematical calculations pass unit tests within $10^{-4}$ tolerance.
* API returns complete indicator snapshots for any BIST stock.

---

## Phase 4: Stock Scoring Engine (0–100 Scale)

### Goal
Implement the multi-factor quantitative scoring algorithm producing a 0–100 Technical Score broken into Trend, Momentum, Volume, and Structure sub-scores.

### Backend Changes
* Implement `IScoringEngine`:
  * **Trend Score (Max 40 pts)**: Close > EMA20 (+5), EMA20 > EMA50 (+10), EMA50 > EMA200 (+10), SuperTrend BUY (+10), ADX > 20 (+5).
  * **Momentum Score (Max 30 pts)**: RSI 50–65 (+10), MACD > Signal (+10), Fresh MACD Bullish Cross (+5), Stochastic Bullish (+5).
  * **Volume Score (Max 15 pts)**: Volume > 1.2x Avg20 (+5), Volume > 1.5x Avg20 (+5), OBV Rising (+5).
  * **Price Structure (Max 15 pts)**: Resistance Breakout (+5), Higher High (+5), Higher Low (+5).
* Externalize score weights and thresholds in `appsettings.json` under `ScoringConfig`.

### Database Changes
* Store calculated scores in `Signals` and cached snapshots.

### Frontend Changes
* Visual score widgets: circular gauge / progress bar with color-coded tiers (Green: 75+, Yellow: 50–74, Red: <50).
* Sub-score breakdown cards (Trend, Momentum, Volume, Structure).

### Tests
* Unit tests testing boundary scores: 100 for perfectly bullish setups, 0 for breakdown/downtrend, and mid-range score combinations.

### Definition of Done
* Every stock receives a deterministic score with verifiable arithmetic sum matching sub-scores.

---

## Phase 5: Signal Classification & Explainable Reasons

### Goal
Classify scores into actionable signals (`STRONG_BUY`, `BUY`, `BUY_CANDIDATE`, `WATCH`, `WEAK`, `SELL`, `STRONG_SELL`), generate auditable `SignalReasons`, and compute automated risk/reward parameters.

### Backend Changes
* Implement `ISignalEngine`:
  * Maps total score to `SignalType` enum.
  * Generates concrete `SignalReason` items (e.g. `EMA_BULLISH`, `MACD_CROSS`, `VOLUME_SURGE`, `RESISTANCE_BREAKOUT`).
  * Computes automated Stop Loss (Entry - $2.0 \times ATR$ or Support level).
  * Computes Take Profit 1 ($1.5 \times Risk$) and Take Profit 2 ($2.5 \times Risk$ or Resistance levels).
  * Computes Risk/Reward Ratio.
* Expose `GET /api/analysis/{symbol}/signals`.

### Database Changes
* Create `Signals` and `SignalReasons` tables with relational constraints and cascading deletes.

### Frontend Changes
* "Why this signal?" explainable reasoning card listing all positive/negative factors with individual point contributions.
* Risk/Reward visual calculator showing Entry, Stop Loss, Target 1, Target 2, and R:R ratio.

### Tests
* Unit tests verifying signal classification boundaries.
* Unit tests verifying that $RR = \frac{TP1 - Entry}{Entry - StopLoss}$ is mathematically consistent and $StopLoss < Entry < TP1 < TP2$ for buy signals.

### Definition of Done
* Signals are generated with complete reasoning and risk targets.

---

## Phase 6: Market Scanner & Background Jobs

### Goal
Implement `MarketScannerService` with automated background execution to scan all BIST equities, rank candidates, filter results, and serve them via high-performance endpoints.

### Backend Changes
* Implement `MarketScannerService` with parallel processing and batch database writes.
* Setup background jobs via `IHostedService` / Hangfire executing scans on schedules (Daily, Hourly, 15-Minute).
* Implement `ScannerController` with sorting (`score`, `volume`, `priceChange`, `signalTime`), pagination, and multi-parameter filtering (timeframe, signal tier, minScore, sector, RSI range, volume ratio).
* Integrate Redis / In-Memory caching for scanner query results.

### Database Changes
* Optimized index on `Signals(Timeframe, CreatedAt DESC, Score DESC)`.

### Frontend Changes
* Build the primary BIST Scanner screen (`/scanner`):
  * Filter bar (Signal pill buttons, Score sliders, Sector dropdown, Timeframe switcher).
  * High-density data grid with real-time sortable columns, visual score pills, change percentages, and direct link to stock detail.
  * Quick-filter presets ("Strong Buy", "Volume Surge", "Breakouts", "Oversold Reversals").

### Tests
* Integration test for scanner filtering and sorting logic.
* Benchmark test verifying scanner can process 50+ symbols in under 2 seconds.

### Definition of Done
* The scanner table displays ranked BIST stocks with working filters and pagination.

---

## Phase 7: Stock Detail Page & Interactive Terminal

### Goal
Deliver an institutional-grade stock detail experience (`/stocks/[symbol]`) featuring interactive candlestick charts, technical overlay indicators, score gauges, risk levels, and explainable signal reasons.

### Backend Changes
* Expose unified stock detail endpoint `GET /api/stocks/{symbol}/overview` aggregating latest price, daily change, indicator snapshot, score breakdown, active signals, and support/resistance zones.

### Database Changes
* None (uses existing tables).

### Frontend Changes
* Construct `/stocks/[symbol]` route:
  * Header: Ticker, Company Name, Sector, Current Price, Change %, Volume, Technical Score badge, Signal badge.
  * Main Chart: Full TradingView Lightweight Candlestick Chart with Volume, EMA overlays (20/50/200), Support/Resistance horizontal lines.
  * Indicators Sub-Panes: RSI(14) with 30/70 overbought/oversold bands, MACD histogram.
  * Right Sidebar: Score Breakdown, Risk/Reward Card (Entry, Stop Loss, Target 1 & 2), "Why this signal?" itemized list.
  * Timeframe selector (Daily, 1H, 15M) with dynamic chart reload.

### Tests
* Frontend component rendering and chart mounting verification.
* API integration test for `/api/stocks/{symbol}/overview`.

### Definition of Done
* Users can navigate to any BIST ticker, inspect interactive charts with indicators, and review algorithmic reasoning.

---

## Phase 8: Dynamic Strategy Engine & Strategy Lab

### Goal
Empower users to build, customize, and execute algorithmic trading strategies dynamically without writing code.

### Backend Changes
* Implement `IStrategyEngine` capable of evaluating dynamic expressions:
  * Dynamic rule evaluation: `[Indicator] [Operator] [Value | ComparisonIndicator]` (e.g. `EMA20 > EMA50`, `RSI between 50 and 65`, `VolumeRatio > 1.5`).
  * Logical groups (`AND` / `OR`) and rule weights.
* Preload standard institutional strategies:
  * *Trend Following* (EMA Stack + SuperTrend + ADX)
  * *Momentum Surge* (RSI + MACD Bullish Cross + Volume)
  * *Breakout Hunter* (20-day High + ATR Expansion + Volume Surge)
  * *Oversold Reversal* (RSI < 30 + Support Rebound + Stochastic)
  * *Swing Trading* (Pullback to EMA50 in uptrend)
* Implement `StrategiesController` (`GET`, `POST`, `PUT`, `DELETE`).

### Database Changes
* Create `Strategies` and `StrategyRules` tables with foreign keys and cascade rules.

### Frontend Changes
* Build Strategy Lab page (`/strategies` and `/strategies/new`):
  * Strategy list with active status toggles.
  * Visual rule builder interface allowing users to pick indicators, comparison operators, values, and assign weights.
  * Live rule test runner previewing matching stocks in the current market.

### Tests
* Unit tests for dynamic rule evaluation engine testing various combinations of operators (`>`, `<`, `CrossAbove`, `CrossBelow`).

### Definition of Done
* Users can create custom strategies via UI and evaluate them against BIST stock data.

---

## Phase 9: Institutional Backtest Engine & Analytics

### Goal
Implement an uncompromised backtesting engine that enforces zero look-ahead bias, executes at next-bar open, factors in realistic commissions and slippage, and generates professional performance metrics and equity curves.

### Backend Changes
* Implement `IBacktestEngine`:
  * Iterates chronologically through historical OHLCV bars.
  * Evaluates strategy rules on bar $T$ close; places order for execution at bar $T+1$ open.
  * Monitors bar-by-bar high/low against Stop Loss and Take Profit levels.
  * Deducts commissions (0.15%) and slippage (0.10%).
  * Calculates quantitative metrics:
    * Total Return (%) & Annualized Return (%)
    * Total Trades, Winning Trades, Losing Trades, Win Rate (%)
    * Profit Factor, Average Win / Average Loss
    * Max Drawdown (%) & Max Drawdown Duration
    * Annualized Sharpe Ratio & Sortino Ratio
    * Mathematical Expectancy
  * Generates equity curve points and trade logs.
* Implement `BacktestController` (`POST /api/backtests`, `GET /api/backtests/{id}`, `GET /api/backtests/{id}/trades`).

### Database Changes
* Create `BacktestRuns`, `BacktestTrades`, `BacktestResults` tables.

### Frontend Changes
* Build Backtest interface (`/backtests` and `/backtests/[id]`):
  * Backtest configuration form (Strategy picker, Symbol(s) selector, Date range, Initial capital, Commission %, Slippage %).
  * Results Overview: KPI metric cards (Total Return, Win Rate, Profit Factor, Max Drawdown, Sharpe Ratio).
  * Interactive Equity Curve & Drawdown charts.
  * Detailed Trade History table with Entry Date/Price, Exit Date/Price, PnL %, and Exit Reason.

### Tests
* Unit tests with synthetic predictable price sequences verifying that next-candle open execution, slippage, and commissions are applied accurately.
* Metric verification tests for Sharpe, Profit Factor, and Drawdown.

### Definition of Done
* Users can launch backtests across historical data and inspect comprehensive performance charts and trade breakdowns.

---

## Phase 10: Multi-Channel Alert Engine & Telegram Integration

### Goal
Build an event-driven alert engine supporting In-App alerts and Telegram bot notifications with automated deduplication and timeframe-based cooldowns.

### Backend Changes
* Implement `IAlertEngine` and `INotificationProvider`.
* Implement `TelegramNotificationProvider` with structured markdown formatting (Score, Price, Targets, Risk/Reward).
* Implement `InAppNotificationProvider` with persistent database notifications.
* Implement Cooldown & Deduplication Manager (prevents re-alerting identical signals within cooldown periods: 15m, 1h, 1d).
* Implement `AlertsController` (`GET`, `POST`, `PUT`, `DELETE`).

### Database Changes
* Create `AlertSubscriptions` and `AlertNotifications` tables.

### Frontend Changes
* Build Alerts page (`/alerts`):
  * Configure custom alerts (e.g. "Alert when THYAO score >= 80" or "Signal becomes Strong Buy").
  * Telegram Bot setup helper (instructions to link Telegram Chat ID).
  * Notification history log with read/unread statuses.

### Tests
* Unit test verifying deduplication logic suppresses duplicate alerts during the cooldown window.
* Mock notification provider test verifying message formatting.

### Definition of Done
* Alerts fire correctly on trigger conditions without spamming duplicate signals.

---

## Phase 11: Paper Trading Simulation Engine

### Goal
Deliver a full virtual trading experience enabling users to practice trading BIST strategies with virtual capital without financial risk.

### Backend Changes
* Implement `IPaperTradingService`:
  * Manage multiple virtual portfolios (`CreatePortfolio`, `GetPortfolio`).
  * Process Orders (`Market`, `Limit`, `StopLoss`).
  * Track Positions with real-time average price, current market price, and unrealized PnL.
  * Record executed Trades with realized PnL.
  * Optional: Auto-Paper Trading rule execution (e.g., automatically buy when a stock hits Score >= 80 with 5% portfolio risk allocation).
* Implement `PaperTradingController`.

### Database Changes
* Create `PaperPortfolios`, `PaperPositions`, `PaperOrders`, `PaperTrades` tables.

### Frontend Changes
* Build Paper Trading dashboard (`/paper-trading`):
  * Portfolio summary card (Total Value, Cash Balance, Unrealized PnL, Realized PnL).
  * Open Positions table with real-time PnL badges.
  * Quick Order Execution modal (Buy/Sell, Shares or Position %, Order Type).
  * Order and Trade History tabs.

### Tests
* Unit tests for position accounting: cash deduction on buy, cash addition on sell, weighted average price calculation on multiple buys, and realized PnL on partial exits.

### Definition of Done
* Users can execute paper trades, monitor portfolio equity in real time, and review historical performance.

---

## Phase 12: Security, Authentication & Rate Limiting

### Goal
Harden the entire platform with JWT authentication, refresh tokens, role-based authorization, rate limiting, input validation, and secure headers.

### Backend Changes
* Implement ASP.NET Core Identity with password hashing and JWT token issuance.
* Implement Refresh Token rotation.
* Add rate-limiting middleware (fixed window / token bucket) on `/api/scanner`, `/api/backtests`, and `/api/market-data`.
* Configure CORS policies allowing only approved origins.
* Add FluentValidation validators for all incoming DTOs.
* Configure secure security headers (HSTS, Content-Security-Policy, X-Frame-Options).

### Database Changes
* Add Identity user tables and refresh token storage.

### Frontend Changes
* Build Login / Registration pages (`/login`).
* Add Auth Context / JWT token management with silent refresh.
* Protect private routes (`/paper-trading`, `/alerts`, `/strategies`).

### Tests
* Security tests verifying unauthorized requests to protected endpoints return HTTP 401.
* Rate-limiting test verifying burst thresholds return HTTP 429 Too Many Requests.

### Definition of Done
* User authentication and authorization operate securely across frontend and backend.

---

## Phase 13: Production Packaging, Dockerization & Optimization

### Goal
Package the complete system for automated deployment via Docker, Docker Compose, and Nginx with production environment variable configuration and health monitoring.

### Backend Changes
* Multi-stage production `Dockerfile` for `BistQuant.API` and `BistQuant.Worker`.
* Production `appsettings.Production.json` reading secrets from environment variables.
### Database Changes
* Automated database migration execution on container startup.
### Frontend Changes
* Multi-stage production `Dockerfile` for Next.js frontend (standalone output).
### Deployment Configuration
* `docker-compose.yml` defining `api`, `worker`, `mssql`, `redis`, and `frontend` services.
* `nginx.conf` reverse proxy routing `/api`, `/hangfire`, `/health`, and static assets.
* `.env.example` with clear documentation of all required secrets.
* Database backup documentation.

### Tests
* Verify complete stack boots cleanly via `docker compose up --build`.
* Verify Nginx correctly routes external requests to frontend and backend services.

### Definition of Done
* The entire platform can be launched with a single Docker command and passes all health checks.
