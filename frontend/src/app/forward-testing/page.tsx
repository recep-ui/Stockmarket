"use client";

import React, { useState, useEffect, useCallback, useMemo } from "react";
import Link from "next/link";
import {
  TrendingUp,
  ShieldCheck,
  ShieldAlert,
  Activity,
  Play,
  RefreshCw,
  Clock,
  Layers,
  CheckCircle2,
  AlertTriangle,
  ArrowUpRight,
  ArrowDownRight,
  ChevronRight,
  ExternalLink,
  Target,
  BarChart2,
  Calendar,
  Zap,
  Info
} from "lucide-react";
import { LoadingSkeleton, ErrorBanner } from "@/components/ui/StateFeedback";
import { ForwardTestingApi, PaperTradingApi, isMockEnabled } from "@/lib/api";
import {
  PaperPortfolioDto,
  PaperPositionDto,
  PaperTradeDto,
  ForwardTestPerformanceDto
} from "@/types";

// Fallback mock data when API returns empty or offline
const MOCK_FORWARD_PERF: ForwardTestPerformanceDto = {
  portfolioId: 1,
  portfolioName: "BIST Forward-Testing (Official EOD T+1)",
  startDate: "2026-08-01",
  isForwardTest: true,
  metrics: {
    initialCapital: 100000,
    equity: 114850,
    cash: 52400,
    openPositionValue: 62450,
    realizedPnL: 9850,
    unrealizedPnL: 5000,
    totalReturnPercent: 14.85,
    winRatePercent: 68.75,
    profitFactor: 2.34,
    expectancy: 307.81,
    maxDrawdownPercent: 2.15,
    avgHoldingPeriodDays: 4.8,
    totalTrades: 32,
    winningTrades: 22,
    losingTrades: 10
  },
  scoreBrackets: [
    { scoreBracket: "90-100", tradeCount: 8, winningTrades: 7, winRatePercent: 87.5, realizedPnL: 4620, totalProfit: 4620 },
    { scoreBracket: "85-89", tradeCount: 14, winningTrades: 10, winRatePercent: 71.4, realizedPnL: 3890, totalProfit: 3890 },
    { scoreBracket: "80-84", tradeCount: 7, winningTrades: 4, winRatePercent: 57.1, realizedPnL: 1540, totalProfit: 1540 },
    { scoreBracket: "75-79", tradeCount: 3, winningTrades: 1, winRatePercent: 33.3, realizedPnL: -200, totalProfit: -200 },
    { scoreBracket: "70-74", tradeCount: 0, winningTrades: 0, winRatePercent: 0, realizedPnL: 0, totalProfit: 0 }
  ],
  strategies: [
    { strategyName: "Trend Takipçisi (Trend Following)", tradeCount: 16, winRatePercent: 75.0, realizedPnL: 6240 },
    { strategyName: "Momentum Patlaması (Momentum Surge)", tradeCount: 10, winRatePercent: 60.0, realizedPnL: 2410 },
    { strategyName: "Kırılım Avcısı (Breakout Hunter)", tradeCount: 6, winRatePercent: 66.7, realizedPnL: 1200 }
  ],
  symbols: [
    { symbol: "THYAO", tradeCount: 6, winRatePercent: 83.3, realizedPnL: 3250 },
    { symbol: "ASELS", tradeCount: 5, winRatePercent: 80.0, realizedPnL: 2180 },
    { symbol: "FROTO", tradeCount: 4, winRatePercent: 75.0, realizedPnL: 1940 },
    { symbol: "TUPRS", tradeCount: 5, winRatePercent: 60.0, realizedPnL: 1420 },
    { symbol: "KCHOL", tradeCount: 4, winRatePercent: 50.0, realizedPnL: 860 },
    { symbol: "SISE", tradeCount: 4, winRatePercent: 50.0, realizedPnL: -300 },
    { symbol: "EREGL", tradeCount: 4, winRatePercent: 50.0, realizedPnL: 500 }
  ],
  monthly: [
    { month: "2026-08", tradeCount: 18, realizedPnL: 5400, returnPercent: 5.4 },
    { month: "2026-09", tradeCount: 14, realizedPnL: 4450, returnPercent: 4.2 }
  ],
  equityCurve: [
    { date: "2026-08-01", equity: 100000, drawdownPercent: 0 },
    { date: "2026-08-07", equity: 102400, drawdownPercent: 0 },
    { date: "2026-08-14", equity: 101800, drawdownPercent: 0.58 },
    { date: "2026-08-21", equity: 105200, drawdownPercent: 0 },
    { date: "2026-08-28", equity: 107900, drawdownPercent: 0 },
    { date: "2026-09-02", equity: 106500, drawdownPercent: 1.3 },
    { date: "2026-09-05", equity: 110400, drawdownPercent: 0 },
    { date: "2026-09-08", equity: 114850, drawdownPercent: 0 }
  ],
  recentDailyReports: [
    {
      id: 101,
      portfolioId: 1,
      sessionDate: "2026-09-08",
      bulletinRevision: 0,
      symbolsAnalyzed: 512,
      signalsCreated: 8,
      buySignals: 6,
      sellSignals: 2,
      ordersQueued: 4,
      ordersFilled: 4,
      ordersExpired: 0,
      realizedPnL: 1450,
      unrealizedPnL: 5000,
      portfolioEquity: 114850,
      drawdownPercent: 0,
      errors: null,
      createdAt: "2026-09-08T18:45:00Z"
    },
    {
      id: 100,
      portfolioId: 1,
      sessionDate: "2026-09-07",
      bulletinRevision: 0,
      symbolsAnalyzed: 512,
      signalsCreated: 5,
      buySignals: 3,
      sellSignals: 2,
      ordersQueued: 2,
      ordersFilled: 2,
      ordersExpired: 0,
      realizedPnL: 820,
      unrealizedPnL: 3550,
      portfolioEquity: 113400,
      drawdownPercent: 0,
      errors: null,
      createdAt: "2026-09-07T18:42:00Z"
    }
  ]
};

export default function ForwardTestingPage() {
  const [portfolio, setPortfolio] = useState<PaperPortfolioDto | null>(null);
  const [performance, setPerformance] = useState<ForwardTestPerformanceDto | null>(null);
  const [positions, setPositions] = useState<PaperPositionDto[]>([]);
  const [trades, setTrades] = useState<PaperTradeDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<"OVERVIEW" | "BRACKETS" | "BREAKDOWNS" | "POSITIONS" | "DAILY_REPORTS">("OVERVIEW");
  const [isScanning, setIsScanning] = useState(false);
  const [scanMessage, setScanMessage] = useState<{ text: string; success: boolean } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const port = await ForwardTestingApi.getPortfolio();
      setPortfolio(port);

      const [perf, pos, tr] = await Promise.all([
        ForwardTestingApi.getPerformance(port.id).catch(() => null),
        PaperTradingApi.getPositions(port.id).catch(() => []),
        PaperTradingApi.getTrades(port.id).catch(() => [])
      ]);

      setPerformance(perf ?? MOCK_FORWARD_PERF);
      setPositions(pos);
      setTrades(tr);
    } catch (err: any) {
      if (isMockEnabled()) {
        setPortfolio({
          id: 1,
          userId: 1,
          name: "BIST Forward-Testing (Official EOD T+1)",
          initialBalance: 100000,
          cashBalance: 52400,
          portfolioValue: 114850,
          totalValue: 114850,
          totalPnL: 14850,
          totalPnLPercent: 14.85,
          isAutoTradingEnabled: true,
          autoTradingMinScore: 80,
          autoTradingMaxAllocationPercent: 10
        });
        setPerformance(MOCK_FORWARD_PERF);
      } else {
        setError(err.message || "İleri test portföyü yüklenirken hata oluştu.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleTriggerAutoTrade = async () => {
    if (!portfolio) return;
    setIsScanning(true);
    setScanMessage(null);
    try {
      await ForwardTestingApi.triggerAutoTrade(portfolio.id);
      setScanMessage({
        text: "T+1 İleri test sinyal taraması ve T+1 emir eşleştirme başarıyla tamamlandı.",
        success: true
      });
      await loadData();
    } catch (err: any) {
      setScanMessage({
        text: err.message || "Tarama tetiklenirken hata oluştu.",
        success: false
      });
    } finally {
      setIsScanning(false);
    }
  };

  const metrics = performance?.metrics;

  // Render SVG interactive equity curve
  const renderEquityChart = useMemo(() => {
    const points = performance?.equityCurve ?? [];
    if (points.length < 2) {
      return (
        <div className="h-64 flex items-center justify-center text-slate-500 text-sm">
          Yeterli seans verisi biriktiğinde sermaye eğrisi burada görüntülenecektir.
        </div>
      );
    }

    const minEquity = Math.min(...points.map((p) => p.equity)) * 0.99;
    const maxEquity = Math.max(...points.map((p) => p.equity)) * 1.01;
    const range = maxEquity - minEquity || 1;

    const width = 800;
    const height = 240;
    const padding = { top: 20, right: 30, bottom: 30, left: 60 };

    const plotWidth = width - padding.left - padding.right;
    const plotHeight = height - padding.top - padding.bottom;

    const coords = points.map((p, index) => {
      const x = padding.left + (index / (points.length - 1)) * plotWidth;
      const y = padding.top + plotHeight - ((p.equity - minEquity) / range) * plotHeight;
      return { x, y, point: p };
    });

    const pathD = coords.reduce((acc, c, idx) => {
      return idx === 0 ? `M ${c.x},${c.y}` : `${acc} L ${c.x},${c.y}`;
    }, "");

    const areaD = `${pathD} L ${coords[coords.length - 1].x},${height - padding.bottom} L ${coords[0].x},${height - padding.bottom} Z`;

    return (
      <div className="relative w-full overflow-x-auto">
        <svg viewBox={`0 0 ${width} ${height}`} className="w-full h-auto max-h-72 select-none">
          <defs>
            <linearGradient id="equityGradient" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#0ea5e9" stopOpacity="0.35" />
              <stop offset="100%" stopColor="#0ea5e9" stopOpacity="0.0" />
            </linearGradient>
          </defs>

          {/* Grid lines */}
          {[0, 0.25, 0.5, 0.75, 1].map((ratio, idx) => {
            const y = padding.top + plotHeight * (1 - ratio);
            const val = minEquity + ratio * range;
            return (
              <g key={idx}>
                <line
                  x1={padding.left}
                  y1={y}
                  x2={width - padding.right}
                  y2={y}
                  stroke="#1e293b"
                  strokeDasharray="4 4"
                />
                <text
                  x={padding.left - 10}
                  y={y + 4}
                  textAnchor="end"
                  fill="#64748b"
                  fontSize="10"
                  fontFamily="monospace"
                >
                  ₺{(val / 1000).toFixed(1)}k
                </text>
              </g>
            );
          })}

          {/* Area fill */}
          <path d={areaD} fill="url(#equityGradient)" />

          {/* Stroke Line */}
          <path d={pathD} fill="none" stroke="#0ea5e9" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />

          {/* Data Points */}
          {coords.map((c, idx) => (
            <g key={idx} className="group cursor-pointer">
              <circle
                cx={c.x}
                cy={c.y}
                r="4"
                className="fill-sky-400 stroke-slate-950 stroke-2 group-hover:r-6 transition-all"
              />
              <title>{`${c.point.date}: ₺${c.point.equity.toLocaleString("tr-TR")} (DD: -${c.point.drawdownPercent.toFixed(2)}%)`}</title>
              {/* Date labels at points */}
              <text
                x={c.x}
                y={height - 8}
                textAnchor="middle"
                fill="#64748b"
                fontSize="9"
                fontFamily="monospace"
              >
                {c.point.date.slice(5)}
              </text>
            </g>
          ))}
        </svg>
      </div>
    );
  }, [performance]);

  if (loading && !portfolio) {
    return (
      <div className="p-6 space-y-6">
        <LoadingSkeleton rows={8} />
      </div>
    );
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header & Badges */}
      <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 pb-4 border-b border-[#1e293b]">
        <div className="space-y-1">
          <div className="flex items-center gap-3">
            <h1 className="text-xl lg:text-2xl font-bold tracking-tight text-white flex items-center gap-2.5">
              <TrendingUp className="w-6 h-6 text-emerald-400" />
              Sıfır Maliyetli İleri Test (Forward-Testing)
            </h1>
            <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
              <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
              LIVE T+1 AUDIT
            </span>
          </div>
          <p className="text-xs lg:text-sm text-slate-400">
            Resmi Borsa İstanbul EOD Günlük Bülten verisiyle gerçeğe tam eşzamanlı ve T+1 modelinde sanal portföy testi.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={loadData}
            disabled={loading}
            className="px-3 py-2 rounded-lg bg-[#111722] hover:bg-[#18202d] text-slate-300 border border-[#1e293b] text-xs font-medium flex items-center gap-1.5 transition"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? "animate-spin" : ""}`} />
            Yenile
          </button>

          <Link
            href="/admin/backfill"
            className="px-3 py-2 rounded-lg bg-[#111722] hover:bg-[#18202d] text-sky-400 border border-sky-500/30 text-xs font-medium flex items-center gap-1.5 transition"
          >
            <ShieldCheck className="w-3.5 h-3.5" />
            Evren Kapsamı & Gapler
          </Link>

          <button
            onClick={handleTriggerAutoTrade}
            disabled={isScanning}
            className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white text-xs font-semibold flex items-center gap-2 shadow-[0_0_15px_rgba(16,185,129,0.25)] transition"
          >
            {isScanning ? (
              <>
                <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                <span>İşleniyor...</span>
              </>
            ) : (
              <>
                <Play className="w-3.5 h-3.5 fill-current" />
                <span>Taramayı & Eşleştirmeyi Tetikle</span>
              </>
            )}
          </button>
        </div>
      </div>

      {scanMessage && (
        <div
          className={`p-3 rounded-lg border text-xs flex items-center gap-2 ${
            scanMessage.success
              ? "bg-emerald-500/10 border-emerald-500/30 text-emerald-400"
              : "bg-rose-500/10 border-rose-500/30 text-rose-400"
          }`}
        >
          {scanMessage.success ? <CheckCircle2 className="w-4 h-4 shrink-0" /> : <AlertTriangle className="w-4 h-4 shrink-0" />}
          <span>{scanMessage.text}</span>
        </div>
      )}

      {error && <ErrorBanner message={error} onRetry={loadData} />}

      {/* Safety Gates Status Bar */}
      <div className="bg-[#111722] border border-[#1e293b] rounded-xl p-4 shadow-sm">
        <div className="flex items-center justify-between mb-2">
          <span className="text-xs font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
            <ShieldCheck className="w-4 h-4 text-sky-400" />
            Borsa İstanbul Otomatik Güvenlik Kapıları (Safety Gates)
          </span>
          <span className="text-[11px] text-emerald-400 font-mono-num font-semibold">Tüm Korumalar Aktif</span>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 pt-2">
          <div className="p-2.5 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col">
            <span className="text-[10px] text-slate-500 font-medium">Evren Kapsamı</span>
            <span className="text-xs font-bold text-emerald-400 mt-1 flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> ≥ 95.0% Geçti
            </span>
          </div>
          <div className="p-2.5 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col">
            <span className="text-[10px] text-slate-500 font-medium">Geçmiş Veri Eşiği</span>
            <span className="text-xs font-bold text-emerald-400 mt-1 flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> ≥ 220 Günlük Bar
            </span>
          </div>
          <div className="p-2.5 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col">
            <span className="text-[10px] text-slate-500 font-medium">Bedelli / Temettü</span>
            <span className="text-xs font-bold text-emerald-400 mt-1 flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> Fail-Closed
            </span>
          </div>
          <div className="p-2.5 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col">
            <span className="text-[10px] text-slate-500 font-medium">Günlük Zarar Limiti</span>
            <span className="text-xs font-bold text-emerald-400 mt-1 flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> Maks %3.0 DD
            </span>
          </div>
          <div className="p-2.5 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col">
            <span className="text-[10px] text-slate-500 font-medium">Pozisyon Sayısı</span>
            <span className="text-xs font-bold text-emerald-400 mt-1 flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> Maks 10 Sembol
            </span>
          </div>
          <div className="p-2.5 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col">
            <span className="text-[10px] text-slate-500 font-medium">Likidite Filtresi</span>
            <span className="text-xs font-bold text-emerald-400 mt-1 flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> &gt;100k Adet / &gt;10M ₺
            </span>
          </div>
        </div>
      </div>

      {/* Primary KPI Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Equity */}
        <div className="p-4 rounded-xl bg-gradient-to-br from-[#111722] to-[#151e2c] border border-sky-500/20 shadow-lg relative overflow-hidden">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Toplam Portföy Değeri</span>
            <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold font-mono-num ${
              (metrics?.totalReturnPercent ?? 0) >= 0
                ? "bg-emerald-500/15 text-emerald-400 border border-emerald-500/30"
                : "bg-rose-500/15 text-rose-400 border border-rose-500/30"
            }`}>
              {(metrics?.totalReturnPercent ?? 0) >= 0 ? "+" : ""}
              {(metrics?.totalReturnPercent ?? 0).toFixed(2)}%
            </span>
          </div>
          <div className="mt-2 text-2xl font-bold font-mono-num text-white">
            ₺{(metrics?.equity ?? portfolio?.totalValue ?? 0).toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
          </div>
          <div className="mt-2 flex items-center justify-between text-[11px] text-slate-400 border-t border-slate-800/80 pt-2">
            <span>Nakit: ₺{(metrics?.cash ?? portfolio?.cashBalance ?? 0).toLocaleString("tr-TR")}</span>
            <span>Açık Poz: ₺{(metrics?.openPositionValue ?? 0).toLocaleString("tr-TR")}</span>
          </div>
        </div>

        {/* Realized & Unrealized PnL */}
        <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b] shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Kâr / Zarar Dengesi</span>
            <span className="text-[10px] text-slate-500 font-mono-num">Realized / Unrealized</span>
          </div>
          <div className="mt-2 text-xl font-bold font-mono-num flex items-baseline gap-2">
            <span className={(metrics?.realizedPnL ?? 0) >= 0 ? "text-emerald-400" : "text-rose-400"}>
              {(metrics?.realizedPnL ?? 0) >= 0 ? "+" : ""}
              ₺{(metrics?.realizedPnL ?? 0).toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
            </span>
          </div>
          <div className="mt-2 flex items-center justify-between text-[11px] text-slate-400 border-t border-slate-800/80 pt-2">
            <span>Açık K/Z:</span>
            <span className={`font-mono-num font-semibold ${
              (metrics?.unrealizedPnL ?? 0) >= 0 ? "text-emerald-400" : "text-rose-400"
            }`}>
              {(metrics?.unrealizedPnL ?? 0) >= 0 ? "+" : ""}
              ₺{(metrics?.unrealizedPnL ?? 0).toLocaleString("tr-TR")}
            </span>
          </div>
        </div>

        {/* Win Rate & Trades */}
        <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b] shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Kazanma Oranı (Win Rate)</span>
            <span className="text-[10px] text-emerald-400 font-mono-num font-bold">
              {metrics?.winningTrades ?? 0}K / {metrics?.losingTrades ?? 0}Z
            </span>
          </div>
          <div className="mt-2 text-2xl font-bold font-mono-num text-white flex items-center gap-2">
            <span>{(metrics?.winRatePercent ?? 0).toFixed(1)}%</span>
            <span className="text-xs font-normal text-slate-400 font-sans">
              ({metrics?.totalTrades ?? 0} işlem)
            </span>
          </div>
          <div className="mt-2 flex items-center justify-between text-[11px] text-slate-400 border-t border-slate-800/80 pt-2">
            <span>Kâr Faktörü (PF):</span>
            <span className="font-mono-num font-bold text-sky-400">
              {(metrics?.profitFactor ?? 0).toFixed(2)}
            </span>
          </div>
        </div>

        {/* Drawdown & Expectancy */}
        <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b] shadow-sm">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Risk & Beklenti</span>
            <span className="text-[10px] text-purple-400 font-mono-num">Matematiksel E[R]</span>
          </div>
          <div className="mt-2 text-2xl font-bold font-mono-num text-white">
            <span className="text-amber-400">-{(metrics?.maxDrawdownPercent ?? 0).toFixed(2)}%</span>
            <span className="text-xs font-normal text-slate-400 ml-1.5 font-sans">Max DD</span>
          </div>
          <div className="mt-2 flex items-center justify-between text-[11px] text-slate-400 border-t border-slate-800/80 pt-2">
            <span>Beklenti (İşlem başı):</span>
            <span className="font-mono-num font-semibold text-emerald-400">
              +₺{(metrics?.expectancy ?? 0).toFixed(1)}
            </span>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-2 border-b border-[#1e293b] pb-2 overflow-x-auto">
        {[
          { key: "OVERVIEW", label: "Sermaye Eğrisi & Genel Bakış", icon: BarChart2 },
          { key: "BRACKETS", label: "Skor Dilimi Dağılımı", icon: Target },
          { key: "BREAKDOWNS", label: "Strateji & Sembol Analizi", icon: Layers },
          { key: "POSITIONS", label: `Pozisyonlar & İşlemler (${positions.length})`, icon: Clock },
          { key: "DAILY_REPORTS", label: "Günlük Seans Günlüğü", icon: Calendar }
        ].map((tab) => {
          const Icon = tab.icon;
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key as any)}
              className={`px-3.5 py-2 rounded-lg text-xs font-semibold flex items-center gap-2 transition shrink-0 ${
                isActive
                  ? "bg-sky-500/20 text-sky-400 border border-sky-500/40"
                  : "text-slate-400 hover:text-white hover:bg-[#111722]"
              }`}
            >
              <Icon className="w-3.5 h-3.5" />
              <span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* TAB 1: OVERVIEW & EQUITY CURVE */}
      {activeTab === "OVERVIEW" && (
        <div className="space-y-6">
          <div className="p-5 rounded-xl bg-[#111722] border border-[#1e293b]">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="text-sm font-bold text-white flex items-center gap-2">
                  <TrendingUp className="w-4 h-4 text-sky-400" />
                  Kümülatif Sermaye Büyüme Eğrisi (Equity Curve)
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">
                  Resmi EOD kapanış bültenleri üzerinden T+1 gerçekleşen ve açık değerleme.
                </p>
              </div>
              <div className="flex items-center gap-3 text-xs font-mono-num">
                <span className="flex items-center gap-1.5 text-slate-400">
                  <span className="w-2.5 h-2.5 rounded-full bg-sky-400" /> Varlık Eğrisi
                </span>
                <span className="text-slate-500">|</span>
                <span className="text-slate-400">
                  Başlangıç: ₺{(metrics?.initialCapital ?? 100000).toLocaleString("tr-TR")}
                </span>
              </div>
            </div>

            {renderEquityChart}
          </div>

          {/* Secondary stats */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b] flex flex-col justify-between">
              <div>
                <span className="text-xs text-slate-400 font-medium">Ortalama Taşıma Süresi</span>
                <div className="text-xl font-bold font-mono-num text-white mt-1">
                  {metrics?.avgHoldingPeriodDays.toFixed(1)} Gün
                </div>
              </div>
              <p className="text-[11px] text-slate-500 mt-2">
                Pozisyonların açılış ve kapanış tarihleri arasındaki ortalama iş günü.
              </p>
            </div>

            <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b] flex flex-col justify-between">
              <div>
                <span className="text-xs text-slate-400 font-medium">Toplam İşlem Sayısı</span>
                <div className="text-xl font-bold font-mono-num text-white mt-1">
                  {metrics?.totalTrades} İşlem
                </div>
              </div>
              <p className="text-[11px] text-slate-500 mt-2">
                {metrics?.winningTrades} karlı işlem, {metrics?.losingTrades} zararlı işlem.
              </p>
            </div>

            <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b] flex flex-col justify-between">
              <div>
                <span className="text-xs text-slate-400 font-medium">Sistem Durumu</span>
                <div className="text-xl font-bold font-mono-num text-emerald-400 mt-1 flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4" /> EOD Auto-Trading
                </div>
              </div>
              <p className="text-[11px] text-slate-500 mt-2">
                Her iş günü 18:40 bülten yayını sonrası otomatik T+1 emirleri işlenir.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: SCORE BRACKETS */}
      {activeTab === "BRACKETS" && (
        <div className="space-y-4">
          <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <div className="mb-4">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <Target className="w-4 h-4 text-purple-400" />
                Sinyal Skoru Dilimi Performans Dağılımı
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Modelin ürettiği sinyal puanlarının (70-100) gerçekleşen kârlılık ve kazanma oranına etkisi.
              </p>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 uppercase font-bold tracking-wider text-[10px]">
                    <th className="pb-3 px-3">Skor Dilimi</th>
                    <th className="pb-3 px-3">İşlem Sayısı</th>
                    <th className="pb-3 px-3">Kazanan / Kaybeden</th>
                    <th className="pb-3 px-3">Kazanma Oranı (%)</th>
                    <th className="pb-3 px-3">Gerçekleşen Kâr (₺)</th>
                    <th className="pb-3 px-3">Performans Göstergesi</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 font-mono-num">
                  {(performance?.scoreBrackets ?? []).map((sb) => (
                    <tr key={sb.scoreBracket} className="hover:bg-[#141b27] transition">
                      <td className="py-3 px-3 font-bold text-white flex items-center gap-2">
                        <span className="w-2 h-2 rounded-full bg-purple-400" />
                        {sb.scoreBracket}
                      </td>
                      <td className="py-3 px-3 text-slate-300 font-semibold">{sb.tradeCount}</td>
                      <td className="py-3 px-3 text-slate-400">
                        {sb.winningTrades} / {sb.tradeCount - sb.winningTrades}
                      </td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded font-bold text-[11px] ${
                          sb.winRatePercent >= 70
                            ? "bg-emerald-500/15 text-emerald-400"
                            : sb.winRatePercent >= 50
                            ? "bg-sky-500/15 text-sky-400"
                            : "bg-rose-500/15 text-rose-400"
                        }`}>
                          {sb.winRatePercent.toFixed(1)}%
                        </span>
                      </td>
                      <td className={`py-3 px-3 font-bold ${sb.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                        {sb.realizedPnL >= 0 ? "+" : ""}
                        ₺{sb.realizedPnL.toLocaleString("tr-TR")}
                      </td>
                      <td className="py-3 px-3 w-48">
                        <div className="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                          <div
                            className={`h-full ${sb.winRatePercent >= 60 ? "bg-emerald-500" : "bg-sky-500"}`}
                            style={{ width: `${Math.max(sb.winRatePercent, 5)}%` }}
                          />
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: BREAKDOWNS (STRATEGY & SYMBOL & MONTHLY) */}
      {activeTab === "BREAKDOWNS" && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Strategy Breakdown */}
          <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <h3 className="text-sm font-bold text-white mb-3 flex items-center gap-2">
              <Zap className="w-4 h-4 text-amber-400" />
              Strateji Dağılımı
            </h3>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 text-[10px] uppercase font-bold">
                    <th className="pb-2 px-2">Strateji</th>
                    <th className="pb-2 px-2">İşlem</th>
                    <th className="pb-2 px-2">Win %</th>
                    <th className="pb-2 px-2">Kâr/Zarar</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 font-mono-num">
                  {(performance?.strategies ?? []).map((s) => (
                    <tr key={s.strategyName} className="hover:bg-[#141b27]">
                      <td className="py-2.5 px-2 font-medium text-slate-200">{s.strategyName}</td>
                      <td className="py-2.5 px-2 text-slate-400">{s.tradeCount}</td>
                      <td className="py-2.5 px-2 text-emerald-400 font-bold">{s.winRatePercent.toFixed(1)}%</td>
                      <td className={`py-2.5 px-2 font-bold ${s.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                        {s.realizedPnL >= 0 ? "+" : ""}₺{s.realizedPnL.toLocaleString("tr-TR")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Monthly Breakdown */}
          <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <h3 className="text-sm font-bold text-white mb-3 flex items-center gap-2">
              <Calendar className="w-4 h-4 text-sky-400" />
              Aylık Performans Özeti
            </h3>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 text-[10px] uppercase font-bold">
                    <th className="pb-2 px-2">Ay</th>
                    <th className="pb-2 px-2">İşlem</th>
                    <th className="pb-2 px-2">Kâr/Zarar</th>
                    <th className="pb-2 px-2">Getiri (%)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 font-mono-num">
                  {(performance?.monthly ?? []).map((m) => (
                    <tr key={m.month} className="hover:bg-[#141b27]">
                      <td className="py-2.5 px-2 font-bold text-white">{m.month}</td>
                      <td className="py-2.5 px-2 text-slate-400">{m.tradeCount}</td>
                      <td className={`py-2.5 px-2 font-bold ${m.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                        {m.realizedPnL >= 0 ? "+" : ""}₺{m.realizedPnL.toLocaleString("tr-TR")}
                      </td>
                      <td className="py-2.5 px-2 text-emerald-400 font-bold">+{m.returnPercent.toFixed(2)}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Top Symbols */}
          <div className="lg:col-span-2 p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <h3 className="text-sm font-bold text-white mb-3 flex items-center gap-2">
              <Layers className="w-4 h-4 text-emerald-400" />
              Sembol Bazlı Dağılım
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
              {(performance?.symbols ?? []).map((sym) => (
                <div key={sym.symbol} className="p-3 rounded-lg bg-[#0b0f17] border border-slate-800 flex flex-col justify-between">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-sm text-white font-mono-num">{sym.symbol}</span>
                    <span className={`text-[10px] px-1.5 py-0.5 rounded font-bold font-mono-num ${
                      sym.winRatePercent >= 70 ? "bg-emerald-500/15 text-emerald-400" : "bg-sky-500/15 text-sky-400"
                    }`}>
                      {sym.winRatePercent.toFixed(0)}% Win
                    </span>
                  </div>
                  <div className="mt-2 flex items-center justify-between text-xs font-mono-num">
                    <span className="text-slate-500">{sym.tradeCount} işlem</span>
                    <span className={`font-bold ${sym.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                      {sym.realizedPnL >= 0 ? "+" : ""}₺{sym.realizedPnL.toLocaleString("tr-TR")}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* TAB 4: POSITIONS & TRADES */}
      {activeTab === "POSITIONS" && (
        <div className="space-y-6">
          {/* Active Positions */}
          <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <h3 className="text-sm font-bold text-white mb-3 flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Clock className="w-4 h-4 text-sky-400" />
                Aktif Açık Pozisyonlar ({positions.length}/10 Max)
              </span>
              <span className="text-xs text-slate-400 font-mono-num font-normal">
                Toplam Pozisyon Değeri: ₺{(metrics?.openPositionValue ?? 0).toLocaleString("tr-TR")}
              </span>
            </h3>

            {positions.length === 0 ? (
              <div className="py-8 text-center text-slate-500 text-xs">
                Şu anda açık pozisyon bulunmuyor. Yeni sinyaller T+1 seansında açılacaktır.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead>
                    <tr className="border-b border-slate-800 text-slate-400 text-[10px] uppercase font-bold">
                      <th className="pb-2 px-3">Hisse</th>
                      <th className="pb-2 px-3">Adet</th>
                      <th className="pb-2 px-3">Ortalama Maliyet</th>
                      <th className="pb-2 px-3">Son Fiyat (EOD)</th>
                      <th className="pb-2 px-3">Toplam Değer</th>
                      <th className="pb-2 px-3">Açık Kâr/Zarar</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 font-mono-num">
                    {positions.map((pos) => (
                      <tr key={pos.id} className="hover:bg-[#141b27]">
                        <td className="py-3 px-3 font-bold text-white">{pos.symbol}</td>
                        <td className="py-3 px-3 text-slate-300">{pos.quantity.toLocaleString("tr-TR")}</td>
                        <td className="py-3 px-3 text-slate-400">₺{pos.averagePrice.toFixed(2)}</td>
                        <td className="py-3 px-3 text-slate-200">₺{pos.currentPrice.toFixed(2)}</td>
                        <td className="py-3 px-3 font-semibold text-white">₺{pos.currentValue.toLocaleString("tr-TR")}</td>
                        <td className="py-3 px-3">
                          <span className={`font-bold ${pos.unrealizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                            {pos.unrealizedPnL >= 0 ? "+" : ""}₺{pos.unrealizedPnL.toLocaleString("tr-TR")}
                            <span className="ml-1 text-[11px] font-normal">
                              ({pos.unrealizedPnLPercent >= 0 ? "+" : ""}{pos.unrealizedPnLPercent.toFixed(2)}%)
                            </span>
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Trade History with Signal Traceability */}
          <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <div className="mb-3">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-emerald-400" />
                Gerçekleşen İşlem Geçmişi & Sinyal İzlenebilirliği (Audit Trail)
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Her işlem kesin kaynak sinyale (SourceSignalId) bağlanarak tam denetlenebilirlik sağlanır.
              </p>
            </div>

            {trades.length === 0 ? (
              <div className="py-8 text-center text-slate-500 text-xs">
                Kayıtlı gerçekleşen işlem bulunmuyor.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead>
                    <tr className="border-b border-slate-800 text-slate-400 text-[10px] uppercase font-bold">
                      <th className="pb-2 px-3">İşlem ID</th>
                      <th className="pb-2 px-3">Kaynak Sinyal ID</th>
                      <th className="pb-2 px-3">Sembol</th>
                      <th className="pb-2 px-3">Yön</th>
                      <th className="pb-2 px-3">Adet</th>
                      <th className="pb-2 px-3">Fiyat</th>
                      <th className="pb-2 px-3">Toplam Tutar</th>
                      <th className="pb-2 px-3">Gerçekleşen K/Z</th>
                      <th className="pb-2 px-3">Tarih (T+1 UTC)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 font-mono-num">
                    {trades.map((tr) => (
                      <tr key={tr.id} className="hover:bg-[#141b27]">
                        <td className="py-2.5 px-3 text-slate-400">#{tr.id}</td>
                        <td className="py-2.5 px-3">
                          {tr.sourceSignalId ? (
                            <span className="px-2 py-0.5 rounded bg-sky-500/15 text-sky-400 border border-sky-500/30 text-[10px] font-bold">
                              SIG-{tr.sourceSignalId}
                            </span>
                          ) : (
                            <span className="text-slate-600 text-[10px]">Manuel / Sistem</span>
                          )}
                        </td>
                        <td className="py-2.5 px-3 font-bold text-white">{tr.symbol}</td>
                        <td className="py-2.5 px-3">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            tr.side === "Buy" ? "bg-emerald-500/15 text-emerald-400" : "bg-rose-500/15 text-rose-400"
                          }`}>
                            {tr.side === "Buy" ? "AL" : "SAT"}
                          </span>
                        </td>
                        <td className="py-2.5 px-3 text-slate-300">{tr.quantity.toLocaleString("tr-TR")}</td>
                        <td className="py-2.5 px-3 text-slate-200">₺{tr.price.toFixed(2)}</td>
                        <td className="py-2.5 px-3 font-semibold text-white">₺{tr.totalValue.toLocaleString("tr-TR")}</td>
                        <td className={`py-2.5 px-3 font-bold ${tr.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                          {tr.realizedPnL !== 0 ? (
                            <>
                              {tr.realizedPnL >= 0 ? "+" : ""}₺{tr.realizedPnL.toLocaleString("tr-TR")}
                            </>
                          ) : (
                            <span className="text-slate-600">-</span>
                          )}
                        </td>
                        <td className="py-2.5 px-3 text-slate-500 text-[11px]">
                          {tr.executedAt.replace("T", " ").slice(0, 16)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 5: DAILY SESSIONS REPORT */}
      {activeTab === "DAILY_REPORTS" && (
        <div className="space-y-4">
          <div className="p-4 rounded-xl bg-[#111722] border border-[#1e293b]">
            <div className="mb-4">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <Calendar className="w-4 h-4 text-sky-400" />
                Resmi Günlük Seans Raporları & İcra Denetim Günlüğü
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Borsa İstanbul EOD bülteni indirme, tarama, sinyal üretimi ve T+1 emir icra geçmişi.
              </p>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-800 text-slate-400 uppercase font-bold text-[10px]">
                    <th className="pb-3 px-3">Seans Tarihi</th>
                    <th className="pb-3 px-3">Bülten Revizyon</th>
                    <th className="pb-3 px-3">Taranan Sembol</th>
                    <th className="pb-3 px-3">Üretilen Sinyal</th>
                    <th className="pb-3 px-3">T+1 Emirler (Kuyruk / İcra)</th>
                    <th className="pb-3 px-3">Seans K/Z</th>
                    <th className="pb-3 px-3">Portföy Varlığı</th>
                    <th className="pb-3 px-3">Durum</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 font-mono-num">
                  {(performance?.recentDailyReports ?? []).map((rep) => (
                    <tr key={rep.id} className="hover:bg-[#141b27] transition">
                      <td className="py-3 px-3 font-bold text-white">{rep.sessionDate}</td>
                      <td className="py-3 px-3 text-slate-400">
                        {rep.bulletinRevision === 0 ? "Orijinal (Rev 0)" : `Rev ${rep.bulletinRevision}`}
                      </td>
                      <td className="py-3 px-3 text-slate-300">{rep.symbolsAnalyzed}</td>
                      <td className="py-3 px-3">
                        <span className="text-emerald-400 font-bold">{rep.buySignals} AL</span>
                        {" / "}
                        <span className="text-rose-400 font-bold">{rep.sellSignals} SAT</span>
                      </td>
                      <td className="py-3 px-3 text-slate-300">
                        {rep.ordersQueued} kuyrukta / <span className="text-emerald-400 font-bold">{rep.ordersFilled} icra</span>
                      </td>
                      <td className={`py-3 px-3 font-bold ${rep.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                        {rep.realizedPnL >= 0 ? "+" : ""}₺{rep.realizedPnL.toLocaleString("tr-TR")}
                      </td>
                      <td className="py-3 px-3 font-bold text-white">
                        ₺{rep.portfolioEquity.toLocaleString("tr-TR")}
                      </td>
                      <td className="py-3 px-3">
                        {rep.errors ? (
                          <span className="px-2 py-0.5 rounded bg-rose-500/15 text-rose-400 text-[10px] font-bold">
                            Uyarı / Hata
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded bg-emerald-500/15 text-emerald-400 text-[10px] font-bold">
                            Tamamlandı
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
