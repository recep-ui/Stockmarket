export type SignalType = 
  | "StrongBuy"
  | "Buy"
  | "BuyCandidate"
  | "Watch"
  | "Weak"
  | "Sell"
  | "StrongSell";

export type Timeframe = "M1" | "M5" | "M15" | "M30" | "H1" | "H4" | "Daily" | "Weekly";

export const TimeframeValue: Record<Timeframe, number> = {
  M1: 1,
  M5: 5,
  M15: 15,
  M30: 30,
  H1: 60,
  H4: 240,
  Daily: 250,
  Weekly: 251,
};

export type OrderType = "Market" | "Limit" | "StopLoss";
export type OrderSide = "Buy" | "Sell";
export type OrderStatus = "Pending" | "Filled" | "Cancelled" | "Rejected";
export type NotificationChannel = "InApp" | "Telegram" | "WebPush" | "Email";
export type BacktestStatus = "Pending" | "Running" | "Completed" | "Failed";

export interface SymbolDto {
  id: number;
  marketId?: number;
  ticker: string;
  name: string;
  sector: string;
  industry: string;
  isActive: boolean;
  price?: number;
  changePercent?: number;
}

export interface ScoreBreakdown {
  trendScore: number;
  momentumScore: number;
  volumeScore: number;
  structureScore: number;
  totalScore: number;
  reasons: string[];
}

export interface SignalReasonDto {
  code: string;
  title: string;
  description: string;
  score: number;
  indicator: string;
  indicatorValue?: number | null;
}

export interface ScoresSummaryDto {
  trend: number;
  momentum: number;
  volume: number;
  structure: number;
}

export interface RiskSummaryDto {
  stopLoss: number;
  takeProfit1: number;
  takeProfit2: number;
  riskReward: number;
}

export interface IndicatorsSummaryDto {
  rsi?: number | null;
  adx?: number | null;
  volumeRatio?: number | null;
}

export interface SignalDto {
  symbol: string;
  price: number;
  score: number;
  signal: string;
  scores?: ScoresSummaryDto;
  indicators?: IndicatorsSummaryDto;
  risk?: RiskSummaryDto;
  reasons: SignalReasonDto[];
  createdAt: string;
  id?: number;
  symbolId?: number;
  ticker?: string;
  name?: string;
  timeframe?: string;
  signalType?: SignalType;
  stopLoss?: number;
  takeProfit1?: number;
  takeProfit2?: number;
  riskRewardRatio?: number;
}

export interface ScannerItemDto {
  symbol: string;
  name: string;
  sector: string;
  price: number;
  dailyChangePercent: number;
  score: number;
  signal: string;
  trendScore: number;
  momentumScore: number;
  volumeScore: number;
  structureScore: number;
  rsi?: number | null;
  macd?: number | null;
  adx?: number | null;
  volumeRatio?: number | null;
  trend?: string;
  volume: number;
  signalTime?: string;
  symbolId?: number;
  ticker?: string;
  timeframe?: string;
  changePercent?: number;
  signalType?: SignalType;
  superTrend?: string;
  stopLoss?: number;
  takeProfit1?: number;
  takeProfit2?: number;
  riskRewardRatio?: number;
  isBreakout?: boolean;
  isVolumeSurge?: boolean;
  lastUpdated?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ScannerOverviewDto {
  totalSymbols?: number;
  totalScanned?: number;
  strongBuyCount: number;
  buyCount: number;
  candidateCount?: number;
  buyCandidateCount?: number;
  watchCount: number;
  sellCount: number;
  averageMarketScore?: number;
  topSignals: ScannerItemDto[];
  volumeSurges?: ScannerItemDto[];
  volumeLeaders?: ScannerItemDto[];
  breakouts?: ScannerItemDto[];
  breakoutStocks?: ScannerItemDto[];
  momentumLeaders?: ScannerItemDto[];
}

export interface IndicatorSnapshotDto {
  symbol: string;
  timeframe: number | string;
  timestamp: string;
  ema20?: number | null;
  ema50?: number | null;
  ema100?: number | null;
  ema200?: number | null;
  sma20?: number | null;
  sma50?: number | null;
  sma200?: number | null;
  rsi14?: number | null;
  macd?: number | null;
  macdSignal?: number | null;
  macdHistogram?: number | null;
  atr14?: number | null;
  superTrend?: number | null;
  superTrendDirection?: number | null;
  bollingerUpper?: number | null;
  bollingerMiddle?: number | null;
  bollingerLower?: number | null;
  averageVolume20?: number | null;
  volumeRatio?: number | null;
  obv?: number | null;
  support1?: number | null;
  support2?: number | null;
  resistance1?: number | null;
  resistance2?: number | null;
  isBreakout?: boolean | null;
}

export interface CandleBarDto {
  time: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  ema20?: number;
  ema50?: number;
  ema200?: number;
  rsi?: number;
  macd?: number;
  macdSignal?: number;
  macdHist?: number;
}

export interface StrategyRuleDto {
  id?: number;
  indicator: string;
  operator: string;
  value?: number | null;
  secondaryValue?: number | null;
  comparisonIndicator?: string | null;
  weight: number;
  ruleGroup: string;
  isRequired: boolean;
}

export interface StrategyDto {
  id: number;
  name: string;
  description: string;
  strategyType: string;
  timeframe: Timeframe | string;
  isActive: boolean;
  rules: StrategyRuleDto[];
  // Backwards compatibility alias
  targetTimeframe?: string;
  rulesCount?: number;
}

export interface CreateStrategyRequest {
  name: string;
  description: string;
  strategyType: string;
  timeframe: Timeframe | number;
  rules: StrategyRuleDto[];
}

export interface BacktestRunRequest {
  strategyId?: number | null;
  symbol: string;
  timeframe: Timeframe | number;
  initialCapital: number;
  commissionRate: number;
  slippageRate: number;
  startDate?: string;
  endDate?: string;
}

export interface EquityPointDto {
  date: string;
  equity: number;
  drawdownPercent: number;
  timestamp?: string; // alias
}

export interface BacktestTradeDto {
  id?: number;
  symbol: string;
  entryDate: string;
  entryPrice: number;
  exitDate: string;
  exitPrice: number;
  quantity: number;
  grossPnL: number;
  netPnL: number;
  returnPercent: number;
  exitReason: string;
  // Aliases for compatibility
  pnl?: number;
  pnlPercent?: number;
}

export interface BacktestResultDto {
  runId?: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRate: number;
  totalReturn: number;
  annualizedReturn: number;
  averageWin: number;
  averageLoss: number;
  profitFactor: number;
  maxDrawdown: number;
  sharpeRatio?: number | null;
  sortinoRatio?: number | null;
  expectancy: number;
  equityCurve: EquityPointDto[];
  // Aliases for compatibility
  totalReturnPercent?: number;
  maxDrawdownPercent?: number;
}

export interface BacktestRunDto {
  id: number;
  strategyId?: number;
  strategyName?: string;
  symbol: string;
  timeframe: Timeframe | string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  status: BacktestStatus | string;
  executedAt?: string;
  result?: BacktestResultDto;
  trades?: BacktestTradeDto[];
}

export interface PaperPortfolioDto {
  id: number;
  userId: number;
  name: string;
  initialBalance: number;
  cashBalance: number;
  portfolioValue: number;
  totalValue?: number; // alias
  totalPnL: number;
  totalPnLPercent: number;
  isAutoTradingEnabled: boolean;
  autoTradingMinScore: number;
  autoTradingMaxAllocationPercent: number;
}

export interface PaperPositionDto {
  id: number;
  symbolId: number;
  symbol: string;
  quantity: number;
  averagePrice: number;
  currentPrice: number;
  totalCost: number;
  currentValue: number;
  unrealizedPnL: number;
  unrealizedPnLPercent: number;
}

export interface CreatePaperOrderRequest {
  portfolioId: number;
  symbol: string;
  side: OrderSide;
  type: OrderType;
  quantity: number;
  limitPrice?: number | null;
  clientOrderId?: string | null;
}

export interface PaperTradeDto {
  id: number;
  symbolId: number;
  symbol: string;
  side: OrderSide;
  quantity: number;
  price: number;
  totalValue: number;
  realizedPnL: number;
  commission: number;
  executedAt: string;
  sourceSignalId?: number | null;
}

export interface CreateAlertRequest {
  symbol?: string | null;
  alertType: string;
  condition: string;
  value: number;
  channel: NotificationChannel;
}

export interface AlertSubscriptionDto {
  id: number;
  userId: number;
  symbolId?: number | null;
  symbol?: string | null;
  alertType: string;
  condition: string;
  value: number;
  channel: NotificationChannel;
  isActive: boolean;
  lastTriggeredAt?: string | null;
}

export interface NotificationDto {
  id: number;
  title: string;
  message: string;
  isDelivered: boolean;
  isRead: boolean;
  createdAt: string;
}

export interface WatchlistItemDto {
  id: number;
  symbolId: number;
  ticker: string;
  name: string;
  currentPrice?: number | null;
  changePercent?: number | null;
  // Aliases for compatibility
  symbol?: string;
  price?: number;
  score?: number | null;
  signalType?: SignalType | string;
  notes?: string;
  displayOrder?: number;
  addedAt?: string;
}

export interface WatchlistDto {
  id: number;
  userId: number;
  name: string;
  description?: string;
  isDefault?: boolean;
  items: WatchlistItemDto[];
}

export interface AddWatchlistItemRequest {
  symbol: string; // ticker
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface AuthResponseDto {
  token: string;
  expiresAt: string;
  user: UserProfileDto;
}

export interface UserProfileDto {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  createdAt: string;
}

// ==========================================
// Backfill & Market Data Coverage Types
// ==========================================

export type BackfillJobStatus =
  | "Pending"
  | "Running"
  | "Paused"
  | "Completed"
  | "CompletedWithErrors"
  | "Failed"
  | "Cancelled";

export interface CreateBackfillJobRequest {
  startDate: string;
  endDate: string;
  forceRevisionCheck?: boolean;
}

export interface BackfillJobDto {
  id: number;
  startDate: string;
  endDate: string;
  currentDate?: string | null;
  status: BackfillJobStatus;
  sessionsTotal: number;
  sessionsCompleted: number;
  sessionsSkipped: number;
  sessionsFailed: number;
  barsInserted: number;
  startedAt: string;
  completedAt?: string | null;
  lastError?: string | null;
  createdByUserId?: number | null;
}

export type MarketDataGapReason =
  | "NoTrade"
  | "Suspended"
  | "BulletinMissing"
  | "ImportFailed"
  | "Unknown";

export interface MarketDataGapDto {
  sessionDate: string;
  reason: MarketDataGapReason;
  description: string;
}

export interface SymbolGapsDto {
  ticker: string;
  totalGaps: number;
  gaps: MarketDataGapDto[];
}

export interface SymbolCoverageDto {
  ticker: string;
  name: string;
  firstBarDate?: string | null;
  lastBarDate?: string | null;
  dailyBarCount: number;
  expectedTradingSessions: number;
  missingSessions: number;
  coveragePercent: number;
  ema200Ready: boolean;
  latestBulletinDate?: string | null;
  hasCorporateActionWarning: boolean;
}

export interface UniverseCoverageSummaryDto {
  activeSymbols: number;
  symbolsWith300PlusBars: number;
  symbolsWith500PlusBars: number;
  averageCoveragePercent: number;
  totalMissingSessions: number;
  symbolsWithCorporateActionWarnings: number;
  symbolCoverages: SymbolCoverageDto[];
}

// ==========================================
// Forward Testing Performance Types
// ==========================================

export interface PerformanceMetricDto {
  initialCapital: number;
  equity: number;
  cash: number;
  openPositionValue: number;
  realizedPnL: number;
  unrealizedPnL: number;
  totalReturnPercent: number;
  winRatePercent: number;
  profitFactor: number;
  expectancy: number;
  maxDrawdownPercent: number;
  avgHoldingPeriodDays: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
}

export interface ScoreBracketBreakdownDto {
  scoreBracket: string;
  tradeCount: number;
  winningTrades: number;
  winRatePercent: number;
  realizedPnL: number;
  totalProfit: number;
}

export interface StrategyBreakdownDto {
  strategyName: string;
  tradeCount: number;
  winRatePercent: number;
  realizedPnL: number;
}

export interface SymbolBreakdownDto {
  symbol: string;
  tradeCount: number;
  winRatePercent: number;
  realizedPnL: number;
}

export interface MonthlyBreakdownDto {
  month: string;
  tradeCount: number;
  realizedPnL: number;
  returnPercent: number;
}

export interface CurvePointDto {
  date: string;
  equity: number;
  drawdownPercent: number;
}

export interface ForwardTestDailyReportDto {
  id: number;
  portfolioId: number;
  sessionDate: string;
  bulletinRevision: number;
  symbolsAnalyzed: number;
  signalsCreated: number;
  buySignals: number;
  sellSignals: number;
  ordersQueued: number;
  ordersFilled: number;
  ordersExpired: number;
  realizedPnL: number;
  unrealizedPnL: number;
  portfolioEquity: number;
  drawdownPercent: number;
  errors?: string | null;
  createdAt: string;
}

export interface ForwardTestPerformanceDto {
  portfolioId: number;
  portfolioName: string;
  startDate: string;
  isForwardTest: boolean;
  metrics: PerformanceMetricDto;
  scoreBrackets: ScoreBracketBreakdownDto[];
  strategies: StrategyBreakdownDto[];
  symbols: SymbolBreakdownDto[];
  monthly: MonthlyBreakdownDto[];
  equityCurve: CurvePointDto[];
  recentDailyReports: ForwardTestDailyReportDto[];
}
