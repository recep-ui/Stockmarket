import {
  ScannerItemDto,
  ScannerOverviewDto,
  SignalDto,
  SymbolDto,
  StrategyDto,
  BacktestRunRequest,
  BacktestRunDto,
  BacktestTradeDto,
  PaperPortfolioDto,
  PaperPositionDto,
  PaperTradeDto,
  AlertSubscriptionDto,
  NotificationDto,
  CandleBarDto,
  IndicatorSnapshotDto,
  PagedResult,
  WatchlistDto,
  LoginRequest,
  RegisterRequest,
  AuthResponseDto,
  UserProfileDto,
  Timeframe,
  TimeframeValue
} from "@/types";

// Base API configuration: Supports relative /api behind Nginx, or env override
export const getApiBaseUrl = (): string => {
  if (process.env.NEXT_PUBLIC_API_URL) {
    return process.env.NEXT_PUBLIC_API_URL.replace(/\/+$/, "");
  }
  if (typeof window !== "undefined") {
    return "/api";
  }
  return "http://localhost:5000/api";
};

// Strict Mock Control: Defaults to false in production
export const isMockEnabled = (): boolean => {
  return process.env.NEXT_PUBLIC_USE_MOCK_DATA === "true";
};

// Authentication Token Management
const TOKEN_STORAGE_KEY = "bistquant_auth_token";

export const getAuthToken = (): string | null => {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_STORAGE_KEY);
};

export const setAuthToken = (token: string): void => {
  if (typeof window === "undefined") return;
  localStorage.setItem(TOKEN_STORAGE_KEY, token);
};

export const clearAuthToken = (): void => {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TOKEN_STORAGE_KEY);
};

/**
 * Ensures a valid JWT token exists. If none is found, attempts to authenticate
 * using default demo credentials ONLY IF NEXT_PUBLIC_DEMO_MODE === "true".
 */
export async function ensureAuthToken(): Promise<string | null> {
  const existing = getAuthToken();
  if (existing) return existing;

  if (process.env.NEXT_PUBLIC_DEMO_MODE !== "true") {
    return null;
  }

  try {
    const baseUrl = getApiBaseUrl();
    const res = await fetch(`${baseUrl}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "demo@bistquant.com", password: "Demo1234!" }),
      signal: AbortSignal.timeout(5000)
    });

    if (res.ok) {
      const json = await res.json();
      const token = json.data?.token || json.token;
      if (token) {
        setAuthToken(token);
        return token;
      }
    }
  } catch {
    // Non-fatal; continue unauthenticated
  }
  return null;
}

export interface ApiRequestOptions extends RequestInit {
  timeoutMs?: number;
  skipAuth?: boolean;
}

/**
 * Robust, typed HTTP client for ASP.NET Core REST API
 * Handles 10s timeouts, bearer auth headers, and standard ApiResponse<T> parsing.
 */
export async function fetchApi<T>(endpoint: string, options: ApiRequestOptions = {}): Promise<T> {
  const baseUrl = getApiBaseUrl();
  const url = endpoint.startsWith("http") ? endpoint : `${baseUrl}${endpoint.startsWith("/") ? "" : "/"}${endpoint}`;
  const timeoutMs = options.timeoutMs ?? 10000;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };

  if (!options.skipAuth) {
    let token = getAuthToken();
    if (!token && typeof window !== "undefined") {
      token = await ensureAuthToken();
    }
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const res = await fetch(url, {
      ...options,
      headers,
      signal: options.signal || controller.signal,
    });

    if (!res.ok) {
      let errorDetail = res.statusText;
      try {
        const errJson = await res.json();
        errorDetail = errJson.message || errJson.errors?.[0] || errorDetail;
      } catch {
        // use statusText
      }
      throw new Error(`API Hatası (${res.status}): ${errorDetail}`);
    }

    const json = await res.json();
    return json.data !== undefined ? json.data : json;
  } catch (err: any) {
    if (err.name === "AbortError") {
      throw new Error(`API Zaman Aşımı (${timeoutMs}ms): Sunucudan zamanında yanıt alınamadı.`);
    }
    throw err;
  } finally {
    clearTimeout(timeoutId);
  }
}

// ==============================================================================
// TYPED API SERVICES
// ==============================================================================

export const MarketDataApi = {
  getSymbols: async (search?: string, sector?: string): Promise<SymbolDto[]> => {
    let q = "/symbols";
    const params: string[] = [];
    if (search) params.push(`search=${encodeURIComponent(search)}`);
    if (sector && sector !== "ALL") params.push(`sector=${encodeURIComponent(sector)}`);
    if (params.length > 0) q += `?${params.join("&")}`;
    return fetchApi<SymbolDto[]>(q);
  },

  getSymbolDetail: async (symbol: string): Promise<SymbolDto> => {
    return fetchApi<SymbolDto>(`/symbols/${symbol}`);
  },

  getCandles: async (symbol: string, timeframe: string = "Daily", start?: string, end?: string): Promise<CandleBarDto[]> => {
    let q = `/market-data/${symbol}/history?timeframe=${timeframe}`;
    if (start) q += `&start=${encodeURIComponent(start)}`;
    if (end) q += `&end=${encodeURIComponent(end)}`;
    
    const bars = await fetchApi<any[]>(q);
    return bars.map(b => ({
      time: typeof b.timestamp === "string" ? b.timestamp.split("T")[0] : String(b.timestamp),
      open: Number(b.open),
      high: Number(b.high),
      low: Number(b.low),
      close: Number(b.close),
      volume: Number(b.volume),
      ema20: b.ema20,
      ema50: b.ema50,
      ema200: b.ema200,
      rsi: b.rsi,
      macd: b.macd,
      macdSignal: b.macdSignal,
      macdHist: b.macdHistogram
    }));
  }
};

export const ScannerApi = {
  getOverview: async (): Promise<ScannerOverviewDto> => {
    const data = await fetchApi<any>("/scanner/overview");
    return {
      totalSymbols: data.totalSymbols ?? 0,
      totalScanned: data.totalSymbols ?? 0,
      strongBuyCount: data.strongBuyCount ?? 0,
      buyCount: data.buyCount ?? 0,
      candidateCount: data.candidateCount ?? 0,
      buyCandidateCount: data.candidateCount ?? 0,
      watchCount: data.watchCount ?? 0,
      sellCount: data.sellCount ?? 0,
      averageMarketScore: data.averageMarketScore ?? 0,
      topSignals: (data.topSignals ?? []).map(normalizeScannerItem),
      volumeLeaders: (data.volumeSurges ?? []).map(normalizeScannerItem),
      volumeSurges: (data.volumeSurges ?? []).map(normalizeScannerItem),
      breakouts: (data.breakouts ?? []).map(normalizeScannerItem),
      breakoutStocks: (data.breakouts ?? []).map(normalizeScannerItem),
      momentumLeaders: (data.topSignals ?? []).filter((x: any) => (x.rsi ?? 0) >= 55 && (x.rsi ?? 0) <= 65).map(normalizeScannerItem)
    };
  },

  getScannerResults: async (params: {
    timeframe?: string;
    signal?: string;
    minScore?: number;
    sector?: string;
    sortBy?: string;
    sortDirection?: string;
    page?: number;
    pageSize?: number;
  } = {}): Promise<PagedResult<ScannerItemDto>> => {
    const qParams: string[] = [];
    if (params.timeframe) qParams.push(`timeframe=${params.timeframe}`);
    if (params.signal && params.signal !== "ALL") qParams.push(`signal=${params.signal}`);
    if (params.minScore && params.minScore > 0) qParams.push(`minScore=${params.minScore}`);
    if (params.sector && params.sector !== "ALL") qParams.push(`sector=${encodeURIComponent(params.sector)}`);
    if (params.sortBy) qParams.push(`sortBy=${params.sortBy}`);
    if (params.sortDirection) qParams.push(`sortDirection=${params.sortDirection}`);
    qParams.push(`page=${params.page || 1}`);
    qParams.push(`pageSize=${params.pageSize || 50}`);

    const res = await fetchApi<PagedResult<any>>(`/scanner?${qParams.join("&")}`);
    return {
      ...res,
      items: (res.items || []).map(normalizeScannerItem)
    };
  },

  runScan: async (timeframe: string = "Daily"): Promise<number> => {
    return fetchApi<number>(`/scanner/run?timeframe=${timeframe}`, { method: "POST" });
  }
};

function normalizeScannerItem(x: any): ScannerItemDto {
  const ticker = x.symbol || x.ticker || "";
  const sig = x.signal || x.signalType || "Watch";
  return {
    symbol: ticker,
    ticker: ticker,
    name: x.name || ticker,
    sector: x.sector || "Genel",
    price: Number(x.price ?? 0),
    dailyChangePercent: Number(x.dailyChangePercent ?? x.changePercent ?? 0),
    changePercent: Number(x.dailyChangePercent ?? x.changePercent ?? 0),
    score: Number(x.score ?? 0),
    signal: sig,
    signalType: sig as any,
    trendScore: Number(x.trendScore ?? 0),
    momentumScore: Number(x.momentumScore ?? 0),
    volumeScore: Number(x.volumeScore ?? 0),
    structureScore: Number(x.structureScore ?? 0),
    rsi: x.rsi !== undefined ? Number(x.rsi) : null,
    macd: x.macd !== undefined ? Number(x.macd) : null,
    adx: x.adx !== undefined ? Number(x.adx) : null,
    volumeRatio: x.volumeRatio !== undefined ? Number(x.volumeRatio) : null,
    trend: x.trend || "Neutral",
    superTrend: x.trend || "Neutral",
    volume: Number(x.volume ?? 0),
    signalTime: x.signalTime || new Date().toISOString(),
    isBreakout: Boolean(x.isBreakout || (x.structureScore ?? 0) >= 12),
    isVolumeSurge: Boolean(x.isVolumeSurge || (x.volumeRatio ?? 0) >= 1.4),
    lastUpdated: x.signalTime || new Date().toISOString(),
  };
}

export const AnalysisApi = {
  getTechnicalSnapshot: async (symbol: string, timeframe: string = "Daily"): Promise<IndicatorSnapshotDto> => {
    return fetchApi<IndicatorSnapshotDto>(`/analysis/${symbol}/technical?timeframe=${timeframe}`);
  },

  getSignal: async (symbol: string, timeframe: string = "Daily"): Promise<SignalDto> => {
    return fetchApi<SignalDto>(`/analysis/${symbol}/signals?timeframe=${timeframe}`);
  }
};

export const AuthApi = {
  login: async (req: LoginRequest): Promise<AuthResponseDto> => {
    const res = await fetchApi<AuthResponseDto>("/auth/login", {
      method: "POST",
      body: JSON.stringify(req),
      skipAuth: true
    });
    if (res?.token) {
      setAuthToken(res.token);
    }
    return res;
  },

  register: async (req: RegisterRequest): Promise<AuthResponseDto> => {
    const res = await fetchApi<AuthResponseDto>("/auth/register", {
      method: "POST",
      body: JSON.stringify(req),
      skipAuth: true
    });
    if (res?.token) {
      setAuthToken(res.token);
    }
    return res;
  },

  getMe: async (): Promise<UserProfileDto> => {
    return fetchApi<UserProfileDto>("/auth/me");
  },

  logout: (): void => {
    clearAuthToken();
    if (typeof window !== "undefined") {
      window.location.href = "/login";
    }
  }
};

export const StrategiesApi = {
  getStrategies: async (): Promise<StrategyDto[]> => {
    return fetchApi<StrategyDto[]>("/strategies");
  },

  getStrategy: async (id: number): Promise<StrategyDto> => {
    return fetchApi<StrategyDto>(`/strategies/${id}`);
  },

  createStrategy: async (data: {
    name: string;
    description: string;
    strategyType?: string;
    timeframe?: string | number;
    rules?: any[];
  }): Promise<StrategyDto> => {
    const tf = typeof data.timeframe === "number" ? data.timeframe : 250;
    return fetchApi<StrategyDto>("/strategies", {
      method: "POST",
      body: JSON.stringify({
        name: data.name,
        description: data.description,
        strategyType: data.strategyType || "Technical",
        timeframe: tf,
        rules: data.rules || []
      })
    });
  },

  deleteStrategy: async (id: number): Promise<boolean> => {
    return fetchApi<boolean>(`/strategies/${id}`, { method: "DELETE" });
  }
};

export const BacktestApi = {
  runBacktest: async (req: BacktestRunRequest): Promise<BacktestRunDto> => {
    const tf = typeof req.timeframe === "number" ? req.timeframe : (TimeframeValue[req.timeframe as Timeframe] ?? 250);
    return fetchApi<BacktestRunDto>("/backtests", {
      method: "POST",
      body: JSON.stringify({
        ...req,
        timeframe: tf
      })
    });
  },

  getTrades: async (id: number): Promise<BacktestTradeDto[]> => {
    return fetchApi<BacktestTradeDto[]>(`/backtests/${id}/trades`);
  }
};

export const PaperTradingApi = {
  getPortfolio: async (): Promise<PaperPortfolioDto> => {
    return fetchApi<PaperPortfolioDto>("/paper-portfolios");
  },

  getPositions: async (portfolioId: number): Promise<PaperPositionDto[]> => {
    return fetchApi<PaperPositionDto[]>(`/paper-portfolios/${portfolioId}/positions`);
  },

  getTrades: async (portfolioId: number): Promise<PaperTradeDto[]> => {
    return fetchApi<PaperTradeDto[]>(`/paper-portfolios/${portfolioId}/trades`);
  },

  executeOrder: async (order: {
    portfolioId: number;
    symbol: string;
    side: "Buy" | "Sell";
    type?: "Market" | "Limit" | "StopLoss";
    orderType?: "Market" | "Limit" | "StopLoss";
    quantity: number;
    limitPrice?: number | null;
    clientOrderId?: string | null;
  }): Promise<PaperTradeDto> => {
    return fetchApi<PaperTradeDto>("/paper-portfolios/orders", {
      method: "POST",
      body: JSON.stringify({
        portfolioId: order.portfolioId,
        symbol: order.symbol,
        side: order.side,
        type: order.type || order.orderType || "Market",
        quantity: order.quantity,
        limitPrice: order.limitPrice ?? null,
        clientOrderId: order.clientOrderId ?? null
      })
    });
  }
};

export const AlertsApi = {
  getSubscriptions: async (): Promise<AlertSubscriptionDto[]> => {
    return fetchApi<AlertSubscriptionDto[]>("/alerts");
  },

  createSubscription: async (data: {
    symbol?: string | null;
    symbolId?: number | null;
    alertType?: string;
    condition?: string;
    value: number;
    channel?: string;
  }): Promise<AlertSubscriptionDto> => {
    return fetchApi<AlertSubscriptionDto>("/alerts", {
      method: "POST",
      body: JSON.stringify({
        symbol: data.symbol || null,
        alertType: data.alertType || "ScoreThreshold",
        condition: data.condition || ">=",
        value: data.value,
        channel: data.channel || "InApp"
      })
    });
  },

  deleteSubscription: async (id: number): Promise<boolean> => {
    return fetchApi<boolean>(`/alerts/${id}`, { method: "DELETE" });
  },

  getNotifications: async (): Promise<NotificationDto[]> => {
    return fetchApi<NotificationDto[]>("/alerts/notifications");
  }
};

export const WatchlistsApi = {
  getWatchlists: async (): Promise<WatchlistDto[]> => {
    return fetchApi<WatchlistDto[]>("/watchlists");
  },

  getWatchlist: async (id: number): Promise<WatchlistDto> => {
    return fetchApi<WatchlistDto>(`/watchlists/${id}`);
  },

  createWatchlist: async (name: string, description?: string): Promise<WatchlistDto> => {
    return fetchApi<WatchlistDto>("/watchlists", {
      method: "POST",
      body: JSON.stringify({ name, description })
    });
  },

  addItem: async (watchlistId: number, symbol: string): Promise<WatchlistDto> => {
    return fetchApi<WatchlistDto>(`/watchlists/${watchlistId}/items`, {
      method: "POST",
      body: JSON.stringify({ symbol })
    });
  },

  removeItem: async (watchlistId: number, symbolId: number): Promise<boolean> => {
    return fetchApi<boolean>(`/watchlists/${watchlistId}/items/${symbolId}`, {
      method: "DELETE"
    });
  },

  deleteWatchlist: async (id: number): Promise<boolean> => {
    return fetchApi<boolean>(`/watchlists/${id}`, {
      method: "DELETE"
    });
  }
};

// ==============================================================================
// DEMO MOCK PROVIDERS (ONLY ACCESSED IF NEXT_PUBLIC_USE_MOCK_DATA === "true")
// ==============================================================================

export const MOCK_SYMBOLS: SymbolDto[] = [
  { id: 1, marketId: 1, ticker: "THYAO", name: "Türk Hava Yolları", sector: "Ulaştırma", industry: "Havacılık", isActive: true, price: 326.50, changePercent: 2.84 },
  { id: 2, marketId: 1, ticker: "ASELS", name: "Aselsan Elektronik", sector: "Savunma", industry: "Elektronik", isActive: true, price: 64.20, changePercent: 3.21 },
  { id: 3, marketId: 1, ticker: "TUPRS", name: "Tüpraş Türkiye Petrol", sector: "Enerji", industry: "Rafineri", isActive: true, price: 178.40, changePercent: 1.15 },
  { id: 4, marketId: 1, ticker: "TOASO", name: "Tofaş Türk Otomobil", sector: "Otomotiv", industry: "İmalat", isActive: true, price: 251.00, changePercent: -0.45 },
  { id: 5, marketId: 1, ticker: "SISE", name: "Türkiye Şişe ve Cam", sector: "Sanayi", industry: "Cam", isActive: true, price: 49.30, changePercent: 0.82 },
  { id: 6, marketId: 1, ticker: "EREGL", name: "Ereğli Demir Çelik", sector: "Metal", industry: "Demir Çelik", isActive: true, price: 53.10, changePercent: -1.20 },
  { id: 7, marketId: 1, ticker: "KCHOL", name: "Koç Holding", sector: "Holding", industry: "Holding", isActive: true, price: 214.50, changePercent: 1.90 },
  { id: 8, marketId: 1, ticker: "SAHOL", name: "Sabancı Holding", sector: "Holding", industry: "Holding", isActive: true, price: 93.80, changePercent: 2.10 },
  { id: 9, marketId: 1, ticker: "BIMAS", name: "BİM Birleşik Mağazalar", sector: "Perakende", industry: "Gıda Perakende", isActive: true, price: 488.00, changePercent: 0.65 },
  { id: 10, marketId: 1, ticker: "FROTO", name: "Ford Otosan", sector: "Otomotiv", industry: "İmalat", isActive: true, price: 1075.00, changePercent: 3.40 },
  { id: 11, marketId: 1, ticker: "AKBNK", name: "Akbank", sector: "Bankacılık", industry: "Bankacılık", isActive: true, price: 57.80, changePercent: 1.45 },
  { id: 12, marketId: 1, ticker: "GARAN", name: "Garanti BBVA", sector: "Bankacılık", industry: "Bankacılık", isActive: true, price: 112.90, changePercent: 2.30 },
  { id: 13, marketId: 1, ticker: "ISCTR", name: "Türkiye İş Bankası", sector: "Bankacılık", industry: "Bankacılık", isActive: true, price: 14.85, changePercent: 0.95 },
  { id: 14, marketId: 1, ticker: "YKBNK", name: "Yapı Kredi Bankası", sector: "Bankacılık", industry: "Bankacılık", isActive: true, price: 31.90, changePercent: 1.10 },
  { id: 15, marketId: 1, ticker: "PETKM", name: "Petkim Petrokimya", sector: "Kimya", industry: "Petrokimya", isActive: true, price: 22.40, changePercent: -0.30 },
];

export const MOCK_SCANNER_ITEMS: ScannerItemDto[] = [
  {
    symbol: "THYAO",
    ticker: "THYAO",
    symbolId: 1,
    name: "Türk Hava Yolları",
    sector: "Ulaştırma",
    timeframe: "Daily",
    price: 326.50,
    dailyChangePercent: 2.84,
    changePercent: 2.84,
    volume: 8450000,
    volumeRatio: 1.68,
    score: 92,
    signal: "StrongBuy",
    signalType: "StrongBuy",
    trendScore: 38,
    momentumScore: 28,
    volumeScore: 14,
    structureScore: 12,
    rsi: 61.4,
    macd: 4.82,
    superTrend: "Buy",
    stopLoss: 312.00,
    takeProfit1: 345.00,
    takeProfit2: 362.00,
    riskRewardRatio: 2.45,
    isBreakout: true,
    isVolumeSurge: true,
    lastUpdated: "2026-09-07T11:30:00Z"
  },
  {
    symbol: "ASELS",
    ticker: "ASELS",
    symbolId: 2,
    name: "Aselsan Elektronik",
    sector: "Savunma",
    timeframe: "Daily",
    price: 64.20,
    dailyChangePercent: 3.21,
    changePercent: 3.21,
    volume: 12500000,
    volumeRatio: 1.85,
    score: 88,
    signal: "StrongBuy",
    signalType: "StrongBuy",
    trendScore: 36,
    momentumScore: 26,
    volumeScore: 14,
    structureScore: 12,
    rsi: 58.7,
    macd: 1.25,
    superTrend: "Buy",
    stopLoss: 61.50,
    takeProfit1: 68.00,
    takeProfit2: 71.50,
    riskRewardRatio: 2.70,
    isBreakout: true,
    isVolumeSurge: true,
    lastUpdated: "2026-09-07T11:30:00Z"
  },
  {
    symbol: "FROTO",
    ticker: "FROTO",
    symbolId: 10,
    name: "Ford Otosan",
    sector: "Otomotiv",
    timeframe: "Daily",
    price: 1075.00,
    dailyChangePercent: 3.40,
    changePercent: 3.40,
    volume: 720000,
    volumeRatio: 1.55,
    score: 84,
    signal: "Buy",
    signalType: "Buy",
    trendScore: 34,
    momentumScore: 25,
    volumeScore: 13,
    structureScore: 12,
    rsi: 63.2,
    macd: 18.5,
    superTrend: "Buy",
    stopLoss: 1030.00,
    takeProfit1: 1140.00,
    takeProfit2: 1195.00,
    riskRewardRatio: 2.67,
    isBreakout: false,
    isVolumeSurge: true,
    lastUpdated: "2026-09-07T11:30:00Z"
  }
];

export const MOCK_OVERVIEW: ScannerOverviewDto = {
  totalSymbols: 15,
  totalScanned: 15,
  strongBuyCount: 2,
  buyCount: 3,
  candidateCount: 4,
  buyCandidateCount: 4,
  watchCount: 4,
  sellCount: 2,
  topSignals: MOCK_SCANNER_ITEMS,
  volumeLeaders: MOCK_SCANNER_ITEMS.filter(x => (x.volumeRatio ?? 0) > 1.3),
  volumeSurges: MOCK_SCANNER_ITEMS.filter(x => (x.volumeRatio ?? 0) > 1.3),
  breakouts: MOCK_SCANNER_ITEMS.filter(x => x.isBreakout),
  breakoutStocks: MOCK_SCANNER_ITEMS.filter(x => x.isBreakout),
  momentumLeaders: MOCK_SCANNER_ITEMS.filter(x => (x.rsi ?? 0) >= 55 && (x.rsi ?? 0) <= 65)
};

export const MOCK_STRATEGIES: StrategyDto[] = [
  { id: 1, name: "Trend Takipçisi (Trend Following)", description: "EMA stack (20 > 50 > 200) + SuperTrend AL + ADX > 20 ile güçlü trendleri yakalar.", strategyType: "Technical", timeframe: "Daily", targetTimeframe: "Daily", isActive: true, rulesCount: 4, rules: [] },
  { id: 2, name: "Momentum Patlaması (Momentum Surge)", description: "RSI 50-65 aralığında, MACD yukarı kesişim ve hacim artışı.", strategyType: "Technical", timeframe: "Daily", targetTimeframe: "Daily", isActive: true, rulesCount: 4, rules: [] },
  { id: 3, name: "Kırılım Avcısı (Breakout Hunter)", description: "20 günlük tepe kırılımı ve hacim patlaması (> 1.5x).", strategyType: "Technical", timeframe: "Daily", targetTimeframe: "Daily", isActive: true, rulesCount: 3, rules: [] }
];

export const MOCK_PORTFOLIO: PaperPortfolioDto = {
  id: 1,
  userId: 1,
  name: "Sanal BIST Portföyü",
  initialBalance: 100000,
  cashBalance: 58450,
  portfolioValue: 106250,
  totalValue: 106250,
  totalPnL: 6250,
  totalPnLPercent: 6.25,
  isAutoTradingEnabled: true,
  autoTradingMinScore: 82,
  autoTradingMaxAllocationPercent: 5
};

export const MOCK_POSITIONS: PaperPositionDto[] = [
  { id: 1, symbolId: 1, symbol: "THYAO", quantity: 60, averagePrice: 308.20, currentPrice: 326.50, totalCost: 18492, currentValue: 19590, unrealizedPnL: 1098, unrealizedPnLPercent: 5.94 },
  { id: 2, symbolId: 2, symbol: "ASELS", quantity: 250, averagePrice: 59.80, currentPrice: 64.20, totalCost: 14950, currentValue: 16050, unrealizedPnL: 1100, unrealizedPnLPercent: 7.36 }
];

export const MOCK_TRADES: PaperTradeDto[] = [
  { id: 1, symbolId: 1, symbol: "THYAO", side: "Buy", quantity: 60, price: 308.20, totalValue: 18492, realizedPnL: 0, commission: 27.74, executedAt: "2026-09-02T10:15:00Z" },
  { id: 2, symbolId: 2, symbol: "ASELS", side: "Buy", quantity: 250, price: 59.80, totalValue: 14950, realizedPnL: 0, commission: 22.43, executedAt: "2026-09-03T11:00:00Z" }
];

export const MOCK_ALERTS: AlertSubscriptionDto[] = [
  { id: 1, userId: 1, symbolId: 1, symbol: "THYAO", alertType: "ScoreThreshold", condition: ">=", value: 85, channel: "Telegram", isActive: true, lastTriggeredAt: "2026-09-07T10:00:00Z" },
  { id: 2, userId: 1, symbolId: 2, symbol: "ASELS", alertType: "StrongBuy", condition: "==", value: 1, channel: "InApp", isActive: true, lastTriggeredAt: "2026-09-07T09:35:00Z" }
];

export const MOCK_NOTIFICATIONS: NotificationDto[] = [
  { id: 1, title: "THYAO Sinyali", message: "Skor: 92/100 | Fiyat: 326.50 TL | Hedef: 345.00 TL", isDelivered: true, isRead: false, createdAt: "2026-09-07T10:00:00Z" },
  { id: 2, title: "ASELS Sinyali", message: "63.50 TL direnç kırılımı. Skor: 88/100", isDelivered: true, isRead: true, createdAt: "2026-09-07T09:35:00Z" }
];

export function generateCandleHistory(basePrice: number, count: number = 90): CandleBarDto[] {
  const result: CandleBarDto[] = [];
  let price = basePrice * 0.85;
  const now = new Date();

  for (let i = count; i >= 0; i--) {
    const d = new Date(now);
    d.setDate(d.getDate() - i);
    if (d.getDay() === 0 || d.getDay() === 6) continue;

    const drift = (Math.random() * 0.04 - 0.018);
    const open = Math.round(price * 100) / 100;
    const close = Math.round(open * (1 + drift) * 100) / 100;
    const high = Math.round(Math.max(open, close) * (1 + Math.random() * 0.015) * 100) / 100;
    const low = Math.round(Math.min(open, close) * (1 - Math.random() * 0.015) * 100) / 100;
    const volume = Math.floor(Math.random() * 8000000 + 1000000);
    const timeStr = d.toISOString().split("T")[0];

    result.push({
      time: timeStr,
      open,
      high,
      low,
      close,
      volume,
      ema20: Math.round(close * 0.98 * 100) / 100,
      ema50: Math.round(close * 0.95 * 100) / 100,
      ema200: Math.round(close * 0.90 * 100) / 100,
      rsi: Math.round((50 + Math.sin(i / 5) * 15) * 10) / 10,
      macd: Math.round(Math.sin(i / 6) * 3 * 100) / 100,
      macdSignal: Math.round(Math.sin((i - 2) / 6) * 2.8 * 100) / 100,
      macdHist: Math.round(Math.sin(i / 4) * 1.5 * 100) / 100,
    });

    price = close;
  }

  return result;
}
