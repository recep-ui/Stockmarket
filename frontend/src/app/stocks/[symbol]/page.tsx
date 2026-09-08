"use client";

import React, { useState, useEffect, use, useCallback } from "react";
import Link from "next/link";
import {
  ShieldAlert,
  Target,
  ArrowUpRight,
  ArrowDownRight,
  Clock,
  Layers,
  Sparkles,
  ChevronLeft,
  CheckCircle2,
  AlertCircle,
  Wallet
} from "lucide-react";
import { SignalBadge } from "@/components/ui/Badge";
import { ScoreGauge } from "@/components/ui/ScoreGauge";
import { StockChart } from "@/components/charts/StockChart";
import { LoadingSkeleton, ErrorBanner } from "@/components/ui/StateFeedback";
import {
  MarketDataApi,
  AnalysisApi,
  PaperTradingApi,
  isMockEnabled,
  MOCK_SYMBOLS,
  MOCK_SCANNER_ITEMS,
  generateCandleHistory
} from "@/lib/api";
import {
  CandleBarDto,
  SymbolDto,
  SignalDto,
  IndicatorSnapshotDto,
  PaperPortfolioDto
} from "@/types";

interface PageProps {
  params: Promise<{ symbol: string }>;
}

export default function StockDetailPage({ params }: PageProps) {
  const resolvedParams = use(params);
  const symbolTicker = resolvedParams.symbol.toUpperCase();

  const [timeframe, setTimeframe] = useState<"Daily" | "H1" | "M15">("Daily");
  const [stockMeta, setStockMeta] = useState<SymbolDto | null>(null);
  const [signalData, setSignalData] = useState<SignalDto | null>(null);
  const [snapshot, setSnapshot] = useState<IndicatorSnapshotDto | null>(null);
  const [candles, setCandles] = useState<CandleBarDto[]>([]);
  const [portfolio, setPortfolio] = useState<PaperPortfolioDto | null>(null);

  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const [orderShares, setOrderShares] = useState<number>(10);
  const [orderSubmitting, setOrderSubmitting] = useState<boolean>(false);
  const [orderFeedback, setOrderFeedback] = useState<{ success: boolean; message: string } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      // 1. Load Symbol
      const sym = await MarketDataApi.getSymbolDetail(symbolTicker);
      setStockMeta(sym);

      // 2. Load Candles
      const candleBars = await MarketDataApi.getCandles(symbolTicker, timeframe);
      setCandles(candleBars);

      // 3. Load Technical Snapshot
      try {
        const snap = await AnalysisApi.getTechnicalSnapshot(symbolTicker, timeframe);
        setSnapshot(snap);
      } catch {
        // snapshot may be unavailable if history is insufficient
      }

      // 4. Load Signal
      try {
        const sig = await AnalysisApi.getSignal(symbolTicker, timeframe);
        setSignalData(sig);
      } catch {
        // signal may be unavailable
      }

      // 5. Load Paper Portfolio
      try {
        const port = await PaperTradingApi.getPortfolio();
        setPortfolio(port);
      } catch {
        // unauthenticated
      }
    } catch (err: any) {
      if (isMockEnabled()) {
        const mockSym = MOCK_SYMBOLS.find((s) => s.ticker === symbolTicker) || MOCK_SYMBOLS[0];
        setStockMeta(mockSym);
        const mockItem = MOCK_SCANNER_ITEMS.find((s) => s.ticker === symbolTicker) || MOCK_SCANNER_ITEMS[0];
        setSignalData({
          symbol: mockItem.symbol || symbolTicker,
          price: mockItem.price,
          score: mockItem.score,
          signal: mockItem.signal || "Watch",
          scores: {
            trend: mockItem.trendScore,
            momentum: mockItem.momentumScore,
            volume: mockItem.volumeScore,
            structure: mockItem.structureScore
          },
          risk: {
            stopLoss: mockItem.stopLoss ?? mockItem.price * 0.95,
            takeProfit1: mockItem.takeProfit1 ?? mockItem.price * 1.05,
            takeProfit2: mockItem.takeProfit2 ?? mockItem.price * 1.10,
            riskReward: mockItem.riskRewardRatio ?? 2.0
          },
          reasons: [
            { code: "TREND", title: "Trend Gücü", description: "EMA stack pozitif eğilim gösteriyor.", score: 35, indicator: "EMA" },
            { code: "MOMENTUM", title: "Momentum Pozitif", description: "RSI ve MACD alım bölgesinde.", score: 25, indicator: "RSI" }
          ],
          createdAt: new Date().toISOString()
        });
        setCandles(generateCandleHistory(mockItem.price, 90));
      } else {
        setError(err.message || `'${symbolTicker}' için finansal veriler yüklenemedi.`);
      }
    } finally {
      setLoading(false);
    }
  }, [symbolTicker, timeframe]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const currentPrice = signalData?.price ?? stockMeta?.price ?? (candles.length > 0 ? candles[candles.length - 1].close : 100);
  const stopLoss = signalData?.risk?.stopLoss ?? currentPrice * 0.95;
  const takeProfit1 = signalData?.risk?.takeProfit1 ?? currentPrice * 1.05;
  const takeProfit2 = signalData?.risk?.takeProfit2 ?? currentPrice * 1.10;
  const riskRewardRatio = signalData?.risk?.riskReward ?? 2.0;

  const handleOrder = async (side: "Buy" | "Sell") => {
    if (!portfolio) {
      setOrderFeedback({ success: false, message: "Emir iletmek için giriş yapmalısınız veya portföy bulunamadı." });
      return;
    }

    setOrderSubmitting(true);
    setOrderFeedback(null);
    try {
      await PaperTradingApi.executeOrder({
        portfolioId: portfolio.id,
        symbol: symbolTicker,
        side,
        quantity: orderShares,
        orderType: "Market"
      });
      setOrderFeedback({
        success: true,
        message: `${orderShares} adet ${symbolTicker} ${side === "Buy" ? "Alış" : "Satış"} emri başarıyla gerçekleştirildi.`
      });
      // refresh portfolio
      const updatedPort = await PaperTradingApi.getPortfolio();
      setPortfolio(updatedPort);
    } catch (err: any) {
      setOrderFeedback({ success: false, message: err.message || "Emir gerçekleştirilemedi." });
    } finally {
      setOrderSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Back Link & Timeframe Selector */}
      <div className="flex items-center justify-between">
        <Link
          href="/scanner"
          className="inline-flex items-center gap-1.5 text-xs text-slate-400 hover:text-white transition font-semibold"
        >
          <ChevronLeft className="w-4 h-4" /> BIST Taramaya Geri Dön
        </Link>

        {/* Timeframe Selector */}
        <div className="flex items-center gap-1 bg-[#111722] p-1 rounded-lg border border-[#1e293b]">
          {(["Daily", "H1", "M15"] as const).map((tf) => (
            <button
              key={tf}
              onClick={() => setTimeframe(tf)}
              className={`px-3 py-1 text-xs font-semibold rounded transition ${
                timeframe === tf
                  ? "bg-sky-600 text-white shadow"
                  : "text-slate-400 hover:text-white"
              }`}
            >
              {tf === "Daily" ? "Günlük" : tf === "H1" ? "1 Saat" : "15 Dk"}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <ErrorBanner
          title="Veri Hatası"
          message={error}
          onRetry={loadData}
        />
      )}

      {loading && !stockMeta && (
        <LoadingSkeleton rows={6} text={`${symbolTicker} analiz verileri yükleniyor...`} />
      )}

      {stockMeta && (
        <>
          {/* Stock Header Card */}
          <div className="terminal-card p-5 bg-gradient-to-r from-[#111722] to-[#141c2b] border border-[#1e293b]">
            <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
              <div>
                <div className="flex items-center gap-3">
                  <h1 className="text-3xl font-black text-white font-mono-num tracking-tight">
                    {stockMeta.ticker}
                  </h1>
                  <SignalBadge type={(signalData?.signal || "Watch") as any} size="lg" />
                  <ScoreGauge score={signalData?.score ?? 50} size="md" />
                </div>

                <div className="flex items-center gap-3 mt-1 text-xs text-slate-400">
                  <span className="font-semibold text-slate-200">{stockMeta.name}</span>
                  <span>•</span>
                  <span className="text-sky-400 font-semibold">{stockMeta.sector || "Genel"}</span>
                  <span>•</span>
                  <span>{stockMeta.industry || "BIST"}</span>
                </div>
              </div>

              <div className="flex items-baseline gap-6">
                <div>
                  <span className="text-[10px] text-slate-500 uppercase font-bold block">
                    Son Fiyat
                  </span>
                  <span className="text-3xl font-black text-white font-mono-num">
                    ₺{currentPrice.toFixed(2)}
                  </span>
                </div>

                <div className="text-right">
                  <span className="text-[10px] text-slate-500 uppercase font-bold block">
                    Günlük Değişim
                  </span>
                  <span
                    className={`text-lg font-bold font-mono-num flex items-center justify-end gap-0.5 ${
                      (stockMeta.changePercent ?? 0) >= 0 ? "text-emerald-400" : "text-rose-400"
                    }`}
                  >
                    {(stockMeta.changePercent ?? 0) >= 0 ? <ArrowUpRight className="w-5 h-5" /> : <ArrowDownRight className="w-5 h-5" />}
                    %{Math.abs(stockMeta.changePercent ?? 0).toFixed(2)}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Main Chart + Right Side Panels */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left: 2 Columns - Interactive Candlestick Chart */}
            <div className="lg:col-span-2 space-y-6">
              <StockChart
                symbol={stockMeta.ticker}
                data={candles}
                stopLoss={stopLoss}
                takeProfit1={takeProfit1}
                takeProfit2={takeProfit2}
              />

              {/* Quick Paper Order Form */}
              <div className="terminal-card p-4 bg-[#111722]">
                <div className="flex items-center justify-between pb-3 border-b border-[#1e293b]">
                  <div className="flex items-center gap-2">
                    <Wallet className="w-4 h-4 text-emerald-400" />
                    <h3 className="text-sm font-bold text-white">Sanal Portföy Emri</h3>
                  </div>
                  <span className="text-xs text-slate-400 font-mono-num">
                    Nakit: ₺{portfolio ? portfolio.cashBalance.toLocaleString("tr-TR", { minimumFractionDigits: 2 }) : "—"}
                  </span>
                </div>

                {orderFeedback && (
                  <div className={`mt-3 p-2 border rounded-lg text-xs font-semibold flex items-center gap-2 ${
                    orderFeedback.success ? "bg-emerald-500/15 border-emerald-500/30 text-emerald-400" : "bg-rose-500/15 border-rose-500/30 text-rose-400"
                  }`}>
                    {orderFeedback.success ? <CheckCircle2 className="w-4 h-4 shrink-0" /> : <AlertCircle className="w-4 h-4 shrink-0" />}
                    <span>{orderFeedback.message}</span>
                  </div>
                )}

                <div className="mt-3 grid grid-cols-1 sm:grid-cols-4 gap-3 items-end">
                  <div className="sm:col-span-2">
                    <label className="text-[10px] text-slate-400 uppercase font-bold block mb-1">
                      İşlem Adedi (Lot)
                    </label>
                    <input
                      type="number"
                      min="1"
                      max="1000"
                      value={orderShares}
                      onChange={(e) => setOrderShares(Math.max(1, Number(e.target.value)))}
                      className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-1.5 text-xs font-mono-num text-white outline-none font-bold"
                    />
                  </div>

                  <div>
                    <span className="text-[10px] text-slate-400 uppercase font-bold block mb-1">
                      Toplam Tutar
                    </span>
                    <span className="text-xs font-bold font-mono-num text-white block py-2">
                      ₺{(orderShares * currentPrice).toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
                    </span>
                  </div>

                  <div className="flex gap-2">
                    <button
                      onClick={() => handleOrder("Buy")}
                      disabled={orderSubmitting}
                      className="flex-1 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs shadow-lg shadow-emerald-600/30 transition disabled:opacity-50"
                    >
                      AL
                    </button>
                    <button
                      onClick={() => handleOrder("Sell")}
                      disabled={orderSubmitting}
                      className="flex-1 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-white font-bold text-xs shadow-lg shadow-rose-600/30 transition disabled:opacity-50"
                    >
                      SAT
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {/* Right: 1 Column - Score Breakdown, Risk/Reward, Reasons */}
            <div className="space-y-6">
              {/* 1. Score Breakdown Card */}
              <div className="terminal-card p-4">
                <div className="flex items-center justify-between pb-3 border-b border-[#1e293b]">
                  <div className="flex items-center gap-2">
                    <Layers className="w-4 h-4 text-sky-400" />
                    <h3 className="text-sm font-bold text-white">Skor Ayrıştırma</h3>
                  </div>
                  <span className="font-mono-num font-black text-sky-400 text-sm">
                    {signalData?.score ?? 50} / 100
                  </span>
                </div>

                <div className="mt-4 space-y-3">
                  {/* Trend */}
                  <div>
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="text-slate-300 font-medium">Trend Gücü</span>
                      <span className="font-mono-num font-bold text-white">
                        {signalData?.scores?.trend ?? 0} <span className="text-slate-500 font-normal">/ 40</span>
                      </span>
                    </div>
                    <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                      <div
                        className="bg-sky-500 h-full rounded-full transition-all duration-700"
                        style={{ width: `${((signalData?.scores?.trend ?? 0) / 40) * 100}%` }}
                      />
                    </div>
                  </div>

                  {/* Momentum */}
                  <div>
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="text-slate-300 font-medium">Momentum İvmesi</span>
                      <span className="font-mono-num font-bold text-white">
                        {signalData?.scores?.momentum ?? 0} <span className="text-slate-500 font-normal">/ 30</span>
                      </span>
                    </div>
                    <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                      <div
                        className="bg-emerald-500 h-full rounded-full transition-all duration-700"
                        style={{ width: `${((signalData?.scores?.momentum ?? 0) / 30) * 100}%` }}
                      />
                    </div>
                  </div>

                  {/* Volume */}
                  <div>
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="text-slate-300 font-medium">Kurumsal Hacim</span>
                      <span className="font-mono-num font-bold text-white">
                        {signalData?.scores?.volume ?? 0} <span className="text-slate-500 font-normal">/ 15</span>
                      </span>
                    </div>
                    <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                      <div
                        className="bg-amber-500 h-full rounded-full transition-all duration-700"
                        style={{ width: `${((signalData?.scores?.volume ?? 0) / 15) * 100}%` }}
                      />
                    </div>
                  </div>

                  {/* Structure */}
                  <div>
                    <div className="flex items-center justify-between text-xs mb-1">
                      <span className="text-slate-300 font-medium">Fiyat Yapısı & Kırılım</span>
                      <span className="font-mono-num font-bold text-white">
                        {signalData?.scores?.structure ?? 0} <span className="text-slate-500 font-normal">/ 15</span>
                      </span>
                    </div>
                    <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                      <div
                        className="bg-purple-500 h-full rounded-full transition-all duration-700"
                        style={{ width: `${((signalData?.scores?.structure ?? 0) / 15) * 100}%` }}
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* 2. Risk / Reward Card */}
              <div className="terminal-card p-4">
                <div className="flex items-center justify-between pb-3 border-b border-[#1e293b]">
                  <div className="flex items-center gap-2">
                    <ShieldAlert className="w-4 h-4 text-rose-400" />
                    <h3 className="text-sm font-bold text-white">Risk & Getiri Hedefleri</h3>
                  </div>
                  <span className="text-xs font-mono-num font-bold text-emerald-400">
                    R:R {riskRewardRatio.toFixed(2)}
                  </span>
                </div>

                <div className="mt-3 space-y-2 text-xs font-mono-num">
                  <div className="flex items-center justify-between p-2 rounded bg-rose-500/10 border border-rose-500/20">
                    <span className="text-rose-400 font-sans font-semibold">Stop Loss Seviyesi</span>
                    <span className="font-bold text-rose-300">₺{stopLoss.toFixed(2)}</span>
                  </div>
                  <div className="flex items-center justify-between p-2 rounded bg-emerald-500/10 border border-emerald-500/20">
                    <span className="text-emerald-400 font-sans font-semibold">Kâr Al 1 (TP1)</span>
                    <span className="font-bold text-emerald-300">₺{takeProfit1.toFixed(2)}</span>
                  </div>
                  <div className="flex items-center justify-between p-2 rounded bg-sky-500/10 border border-sky-500/20">
                    <span className="text-sky-400 font-sans font-semibold">Kâr Al 2 (TP2)</span>
                    <span className="font-bold text-sky-300">₺{takeProfit2.toFixed(2)}</span>
                  </div>
                </div>
              </div>

              {/* 3. Signal Reasons Card */}
              <div className="terminal-card p-4">
                <div className="flex items-center gap-2 pb-3 border-b border-[#1e293b]">
                  <Sparkles className="w-4 h-4 text-amber-400" />
                  <h3 className="text-sm font-bold text-white">Sinyal Nedenleri & Gerekçeler</h3>
                </div>

                <div className="mt-3 space-y-2.5">
                  {(signalData?.reasons || []).length === 0 ? (
                    <p className="text-xs text-slate-400 py-3 text-center">Aktif kural sinyali tetiklenmedi.</p>
                  ) : (
                    (signalData?.reasons || []).map((reason, idx) => (
                      <div key={idx} className="p-2.5 rounded-lg bg-[#0d121c] border border-[#1e293b] text-xs">
                        <div className="flex items-center justify-between font-bold text-white">
                          <span>{reason.title}</span>
                          <span className="font-mono-num text-emerald-400">+{reason.score} Puan</span>
                        </div>
                        <p className="text-slate-400 text-[11px] mt-1 leading-relaxed">
                          {reason.description}
                        </p>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
