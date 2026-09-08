"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  FlaskConical,
  Play,
  TrendingUp,
  Percent,
  ShieldAlert,
  Award,
  ArrowUpRight,
  ArrowDownRight,
  RotateCcw
} from "lucide-react";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import {
  StrategiesApi,
  MarketDataApi,
  BacktestApi,
  isMockEnabled,
  MOCK_STRATEGIES,
  MOCK_SYMBOLS
} from "@/lib/api";
import {
  BacktestResultDto,
  BacktestTradeDto,
  StrategyDto,
  SymbolDto
} from "@/types";

export default function BacktestsPage() {
  const [strategies, setStrategies] = useState<StrategyDto[]>([]);
  const [symbols, setSymbols] = useState<SymbolDto[]>([]);
  const [loadingInitial, setLoadingInitial] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const [selectedStrategyId, setSelectedStrategyId] = useState<number | undefined>(undefined);
  const [selectedSymbol, setSelectedSymbol] = useState<string>("THYAO");
  const [initialCapital, setInitialCapital] = useState<number>(100000);
  const [commissionRate, setCommissionRate] = useState<number>(0.0015);
  const [slippageRate, setSlippageRate] = useState<number>(0.0010);
  const [isRunning, setIsRunning] = useState<boolean>(false);
  const [hasRun, setHasRun] = useState<boolean>(false);

  const [result, setResult] = useState<BacktestResultDto | null>(null);
  const [trades, setTrades] = useState<BacktestTradeDto[]>([]);

  const loadInitial = useCallback(async () => {
    setLoadingInitial(true);
    setError(null);
    try {
      const [strats, syms] = await Promise.all([
        StrategiesApi.getStrategies().catch(() => []),
        MarketDataApi.getSymbols().catch(() => [])
      ]);

      if (strats.length > 0) {
        setStrategies(strats);
        setSelectedStrategyId(strats[0].id);
      } else if (isMockEnabled()) {
        setStrategies(MOCK_STRATEGIES);
        setSelectedStrategyId(MOCK_STRATEGIES[0].id);
      }

      if (syms.length > 0) {
        setSymbols(syms);
        setSelectedSymbol(syms[0].ticker);
      } else if (isMockEnabled()) {
        setSymbols(MOCK_SYMBOLS);
        setSelectedSymbol(MOCK_SYMBOLS[0].ticker);
      }
    } catch (err: any) {
      if (isMockEnabled()) {
        setStrategies(MOCK_STRATEGIES);
        setSymbols(MOCK_SYMBOLS);
      } else {
        setError(err.message || "Backtest bileşenleri yüklenirken hata oluştu.");
      }
    } finally {
      setLoadingInitial(false);
    }
  }, []);

  useEffect(() => {
    loadInitial();
  }, [loadInitial]);

  const handleRunBacktest = async () => {
    setIsRunning(true);
    setError(null);

    try {
      const res = await BacktestApi.runBacktest({
        strategyId: selectedStrategyId || null,
        symbol: selectedSymbol,
        timeframe: 250, // Timeframe.Daily = 250
        initialCapital,
        commissionRate,
        slippageRate
      });

      if (res.result) {
        setResult(res.result);
      }
      if (res.trades && res.trades.length > 0) {
        setTrades(res.trades);
      } else if (res.id) {
        try {
          const runTrades = await BacktestApi.getTrades(res.id);
          setTrades(runTrades);
        } catch {
          // ignore trades load error
        }
      }
      setHasRun(true);
    } catch (err: any) {
      if (isMockEnabled()) {
        setResult({
          totalTrades: 12,
          winningTrades: 8,
          losingTrades: 4,
          winRate: 66.67,
          totalReturn: 21500.00,
          annualizedReturn: 24.3,
          averageWin: 3200,
          averageLoss: 1025,
          profitFactor: 2.34,
          maxDrawdown: 5.12,
          sharpeRatio: 1.85,
          sortinoRatio: 2.91,
          expectancy: 1791.66,
          equityCurve: [
            { date: "2026-03-01", equity: 100000, drawdownPercent: 0 },
            { date: "2026-06-01", equity: 110000, drawdownPercent: 1.5 },
            { date: "2026-09-01", equity: 121500, drawdownPercent: 0 },
          ]
        });
        setTrades([
          { symbol: selectedSymbol, entryDate: "2026-07-10", exitDate: "2026-07-25", entryPrice: 280, exitPrice: 305, quantity: 100, grossPnL: 2500, netPnL: 2412.25, returnPercent: 8.61, exitReason: "TP1" },
          { symbol: selectedSymbol, entryDate: "2026-08-01", exitDate: "2026-08-15", entryPrice: 300, exitPrice: 290, quantity: 100, grossPnL: -1000, netPnL: -1088.50, returnPercent: -3.63, exitReason: "StopLoss" }
        ]);
        setHasRun(true);
      } else {
        setError(err.message || "Backtest simülasyonu çalıştırılırken hata oluştu.");
      }
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-xl font-black text-white tracking-tight flex items-center gap-2">
          <FlaskConical className="w-5 h-5 text-emerald-400" />
          Kurumsal Seviye Algoritmik Backtest Motoru
        </h1>
        <p className="text-xs text-slate-400 mt-0.5">
          T barı kapanışındaki sinyaller, kesinlikle <strong>T+1 barı açılışında</strong> (sıfır look-ahead bias) komisyon (%0.15) ve kayma (%0.10) ile simüle edilir.
        </p>
      </div>

      {error && (
        <ErrorBanner
          title="Backtest Hatası"
          message={error}
          onRetry={loadInitial}
        />
      )}

      {loadingInitial && (
        <LoadingSkeleton rows={4} text="Stratejiler ve hisse listesi hazırlanıyor..." />
      )}

      {/* Setup Form */}
      <div className="terminal-card p-5 bg-[#111722]">
        <h2 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-4">
          Simülasyon Parametreleri
        </h2>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4 items-end">
          {/* Strategy */}
          <div className="lg:col-span-2">
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1">
              Strateji
            </label>
            <select
              value={selectedStrategyId ?? ""}
              onChange={(e) => setSelectedStrategyId(Number(e.target.value))}
              className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-xs text-white outline-none"
            >
              {strategies.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>

          {/* Symbol */}
          <div>
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1">
              Hisse Senedi
            </label>
            <select
              value={selectedSymbol}
              onChange={(e) => setSelectedSymbol(e.target.value)}
              className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-xs text-white font-mono-num font-bold outline-none"
            >
              {symbols.map((s) => (
                <option key={s.ticker} value={s.ticker}>
                  {s.ticker} ({s.name.slice(0, 14)})
                </option>
              ))}
            </select>
          </div>

          {/* Capital */}
          <div>
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1">
              Başlangıç Sermayesi
            </label>
            <input
              type="number"
              step={10000}
              value={initialCapital}
              onChange={(e) => setInitialCapital(Number(e.target.value))}
              className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-xs text-white font-mono-num font-bold outline-none"
            />
          </div>

          {/* Commission */}
          <div>
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1">
              Komisyon (BIST)
            </label>
            <input
              type="number"
              step={0.0005}
              value={commissionRate}
              onChange={(e) => setCommissionRate(Number(e.target.value))}
              className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-xs text-white font-mono-num font-bold outline-none"
            />
          </div>

          {/* Run Action */}
          <div>
            <button
              onClick={handleRunBacktest}
              disabled={isRunning || loadingInitial}
              className="w-full py-2 px-4 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs flex items-center justify-center gap-2 shadow-lg shadow-emerald-600/30 transition disabled:opacity-50"
            >
              {isRunning ? (
                <>
                  <RotateCcw className="w-4 h-4 animate-spin" />
                  <span>Hesaplanıyor...</span>
                </>
              ) : (
                <>
                  <Play className="w-4 h-4 fill-current" />
                  <span>Backtest Başlat</span>
                </>
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Results Dashboard */}
      {hasRun && result && (
        <div className="space-y-6">
          {/* Key Metrics Strip */}
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
            {/* Total Return */}
            <div className="terminal-card p-4 border-l-4 border-l-emerald-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Net Kâr / Zarar
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className={`text-xl font-black font-mono-num ${result.totalReturn >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                  ₺{result.totalReturn.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
                </span>
              </div>
              <span className="text-[11px] font-mono-num text-emerald-400 font-semibold block mt-0.5">
                {result.totalReturn >= 0 ? "+" : ""}{((result.totalReturn / (initialCapital || 100000)) * 100).toFixed(2)}%
              </span>
            </div>

            {/* Win Rate */}
            <div className="terminal-card p-4 border-l-4 border-l-sky-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Kazanma Oranı (Win Rate)
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-xl font-black font-mono-num text-white">
                  %{result.winRate.toFixed(1)}
                </span>
              </div>
              <span className="text-[11px] font-mono-num text-slate-400 block mt-0.5">
                {result.winningTrades} Başarılı / {result.totalTrades} İşlem
              </span>
            </div>

            {/* Profit Factor */}
            <div className="terminal-card p-4 border-l-4 border-l-purple-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Kâr Faktörü (Profit Factor)
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-xl font-black font-mono-num text-purple-400">
                  {result.profitFactor.toFixed(2)}
                </span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                Brüt Kâr / Brüt Zarar
              </span>
            </div>

            {/* Max Drawdown */}
            <div className="terminal-card p-4 border-l-4 border-l-rose-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Maksimum Düşüş (Max DD)
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-xl font-black font-mono-num text-rose-400">
                  -%{(result.maxDrawdown ?? result.maxDrawdownPercent ?? 0).toFixed(2)}%
                </span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                Tepe-Dip Portföy Kaybı
              </span>
            </div>

            {/* Sharpe Ratio */}
            <div className="terminal-card p-4 border-l-4 border-l-amber-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Sharpe Oranı
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-xl font-black font-mono-num text-amber-400">
                  {result.sharpeRatio !== null && result.sharpeRatio !== undefined ? result.sharpeRatio.toFixed(2) : "—"}
                </span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                {result.sharpeRatio === null ? "Yetersiz varyans/işlem" : "Yıllıklandırılmış Risk/Getiri"}
              </span>
            </div>

            {/* Sortino Ratio */}
            <div className="terminal-card p-4 border-l-4 border-l-indigo-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Sortino Oranı
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-xl font-black font-mono-num text-indigo-400">
                  {result.sortinoRatio !== null && result.sortinoRatio !== undefined ? result.sortinoRatio.toFixed(2) : "—"}
                </span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                {result.sortinoRatio === null ? "Yetersiz negatif getiri" : "Aşağı Yönlü Risk Düzeltilmiş"}
              </span>
            </div>
          </div>

          {/* Executed Trades Log */}
          <div className="terminal-card p-5">
            <h3 className="text-sm font-bold text-white mb-3">
              Gerçekleşen İşlem Geçmişi ({trades.length} İşlem)
            </h3>

            {trades.length === 0 ? (
              <p className="text-xs text-slate-400 py-4 text-center">Bu simülasyonda gerçekleşen işlem bulunmuyor.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead className="bg-[#0e1420] text-slate-400 uppercase font-semibold border-b border-[#1e293b]">
                    <tr>
                      <th className="py-2.5 px-3">Hisse</th>
                      <th className="py-2.5 px-3">Giriş Tarihi / Fiyat</th>
                      <th className="py-2.5 px-3">Çıkış Tarihi / Fiyat</th>
                      <th className="py-2.5 px-3">Adet</th>
                      <th className="py-2.5 px-3">Net K/Z (₺)</th>
                      <th className="py-2.5 px-3">K/Z (%)</th>
                      <th className="py-2.5 px-3">Çıkış Sebebi</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#1e293b] font-mono-num">
                    {trades.map((t, idx) => {
                      const netPnlVal = t.netPnL ?? t.pnl ?? 0;
                      const returnPctVal = t.returnPercent ?? t.pnlPercent ?? 0;
                      return (
                        <tr key={idx} className="hover:bg-slate-800/30">
                          <td className="py-2.5 px-3 font-bold text-white">{t.symbol}</td>
                          <td className="py-2.5 px-3 text-slate-300">
                            {t.entryDate.split("T")[0]} <span className="text-slate-500 font-sans">@</span> ₺{t.entryPrice.toFixed(2)}
                          </td>
                          <td className="py-2.5 px-3 text-slate-300">
                            {t.exitDate.split("T")[0]} <span className="text-slate-500 font-sans">@</span> ₺{t.exitPrice.toFixed(2)}
                          </td>
                          <td className="py-2.5 px-3">{t.quantity}</td>
                          <td className={`py-2.5 px-3 font-bold ${netPnlVal >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                            ₺{netPnlVal.toFixed(2)}
                          </td>
                          <td className={`py-2.5 px-3 font-bold ${returnPctVal >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                            {returnPctVal >= 0 ? "+" : ""}{returnPctVal.toFixed(2)}%
                          </td>
                          <td className="py-2.5 px-3 font-sans text-slate-300">{t.exitReason}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
