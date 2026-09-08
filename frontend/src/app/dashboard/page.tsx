"use client";

import React, { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import {
  TrendingUp,
  Zap,
  Activity,
  Flame,
  ArrowUpRight,
  ArrowDownRight,
  ChevronRight,
  Target,
  Sparkles,
  BarChart2,
  RefreshCw
} from "lucide-react";
import { SignalBadge } from "@/components/ui/Badge";
import { ScoreGauge } from "@/components/ui/ScoreGauge";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import { ScannerApi, isMockEnabled, MOCK_OVERVIEW } from "@/lib/api";
import { ScannerOverviewDto } from "@/types";

export default function DashboardPage() {
  const [overview, setOverview] = useState<ScannerOverviewDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const loadData = useCallback(async () => {
    setError(null);
    try {
      const data = await ScannerApi.getOverview();
      setOverview(data);
    } catch (err: any) {
      if (isMockEnabled()) {
        setOverview(MOCK_OVERVIEW);
      } else {
        setError(err.message || "Piyasa verileri yüklenirken bir hata oluştu.");
      }
    } finally {
      setLoading(false);
      setIsRefreshing(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleRefresh = () => {
    setIsRefreshing(true);
    loadData();
  };

  return (
    <div className="space-y-6">
      {/* 1. Market Status Banner (Clean, no fake 'Piyasa Açık' claims) */}
      <div className="terminal-card p-5 relative overflow-hidden bg-gradient-to-r from-[#111722] via-[#141d2b] to-[#101826] border border-[#1e293b]">
        <div className="absolute top-0 right-0 w-96 h-full bg-gradient-to-l from-sky-500/10 to-transparent pointer-events-none" />
        
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 relative z-10">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <span className="flex h-2.5 w-2.5 relative">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-sky-400 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-sky-500"></span>
              </span>
              <span className="text-xs font-bold uppercase tracking-wider text-sky-400">
                Kantitatif Tarama Terminali • Borsa İstanbul Resmi Günlük Bülten (EOD)
              </span>
              <span className="text-slate-600">|</span>
              <span className="text-xs text-slate-400">BIST 30 & 100 Evreni</span>
            </div>
            <h1 className="text-2xl font-black text-white tracking-tight">
              Borsa İstanbul Kantitatif Tarama Terminali
            </h1>
            <p className="text-xs text-slate-400 mt-1 max-w-2xl leading-relaxed">
              Trend, momentum, kurumsal hacim ve fiyat yapısını 100 üzerinden puanlayarak yüksek olasılıklı 
              AL / SAT sinyalleri üretir. Tüm sinyaller deterministiktir ve sıfır look-ahead bias ile hesaplanır.
            </p>
          </div>

          <div className="flex items-center gap-3 shrink-0">
            <button
              onClick={handleRefresh}
              disabled={isRefreshing || loading}
              className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[#1a2332] hover:bg-[#202b3d] text-slate-300 hover:text-white border border-[#283548] text-xs font-semibold transition disabled:opacity-50"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isRefreshing ? "animate-spin text-sky-400" : ""}`} />
              <span>Yeniden Tara</span>
            </button>
            <Link
              href="/scanner"
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-bold shadow-lg shadow-sky-600/30 transition"
            >
              <span>Taramayı Başlat</span>
              <ChevronRight className="w-3.5 h-3.5" />
            </Link>
          </div>
        </div>
      </div>

      {/* Error state */}
      {error && (
        <ErrorBanner
          title="Veri Tabanı Bağlantı Hatası"
          message={error}
          onRetry={handleRefresh}
        />
      )}

      {/* Loading state */}
      {loading && !overview && (
        <LoadingSkeleton rows={5} text="Piyasa özeti ve sinyal puanlamaları yükleniyor..." />
      )}

      {overview && (
        <>
          {/* 2. Category Counters Banner */}
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
            <div className="terminal-card p-3.5 border-l-4 border-l-emerald-500">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400">GÜÇLÜ AL</span>
                <TrendingUp className="w-4 h-4 text-emerald-400" />
              </div>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black font-mono-num text-emerald-400">
                  {overview.strongBuyCount}
                </span>
                <span className="text-[10px] text-slate-400">hisse</span>
              </div>
            </div>

            <div className="terminal-card p-3.5 border-l-4 border-l-green-500">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400">AL</span>
                <Zap className="w-4 h-4 text-green-400" />
              </div>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black font-mono-num text-green-400">
                  {overview.buyCount}
                </span>
                <span className="text-[10px] text-slate-400">hisse</span>
              </div>
            </div>

            <div className="terminal-card p-3.5 border-l-4 border-l-purple-500">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400">AL ADAYI</span>
                <Sparkles className="w-4 h-4 text-purple-400" />
              </div>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black font-mono-num text-purple-400">
                  {overview.candidateCount ?? overview.buyCandidateCount ?? 0}
                </span>
                <span className="text-[10px] text-slate-400">hisse</span>
              </div>
            </div>

            <div className="terminal-card p-3.5 border-l-4 border-l-amber-500">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400">İZLE</span>
                <Activity className="w-4 h-4 text-amber-400" />
              </div>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black font-mono-num text-amber-400">
                  {overview.watchCount}
                </span>
                <span className="text-[10px] text-slate-400">hisse</span>
              </div>
            </div>

            <div className="terminal-card p-3.5 border-l-4 border-l-slate-500">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400">ORT. SKOR</span>
                <BarChart2 className="w-4 h-4 text-slate-400" />
              </div>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black font-mono-num text-slate-300">
                  {overview.averageMarketScore ? overview.averageMarketScore.toFixed(0) : "—"}
                </span>
                <span className="text-[10px] text-slate-400">/ 100</span>
              </div>
            </div>

            <div className="terminal-card p-3.5 border-l-4 border-l-rose-500">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400">SAT</span>
                <ArrowDownRight className="w-4 h-4 text-rose-400" />
              </div>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black font-mono-num text-rose-400">
                  {overview.sellCount}
                </span>
                <span className="text-[10px] text-slate-400">hisse</span>
              </div>
            </div>
          </div>

          {/* 3. Top Signals Featured Cards */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <Flame className="w-5 h-5 text-amber-400" />
                <h2 className="text-base font-bold text-white tracking-tight">
                  En Yüksek Skorlu Sinyaller (Algoritmik Öncüler)
                </h2>
              </div>
              <Link href="/scanner" className="text-xs text-sky-400 hover:text-sky-300 font-semibold flex items-center gap-1">
                Tümünü Gör <ChevronRight className="w-3.5 h-3.5" />
              </Link>
            </div>

            {overview.topSignals.length === 0 ? (
              <EmptyState
                title="Aktif Sinyal Bulunamadı"
                description="Kayıtlı hisseler için henüz taranmış sinyal bulunmuyor."
                actionText="Taramayı Başlat"
                onAction={handleRefresh}
              />
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                {overview.topSignals.map((item) => (
                  <Link
                    key={item.symbol || item.ticker}
                    href={`/stocks/${item.symbol || item.ticker}`}
                    className="terminal-card p-4 hover:border-sky-500/50 hover:shadow-lg transition group relative overflow-hidden"
                  >
                    <div className="flex items-start justify-between">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="text-lg font-black text-white font-mono-num group-hover:text-sky-300 transition">
                            {item.symbol || item.ticker}
                          </span>
                          <SignalBadge type={(item.signal || item.signalType) as any} size="sm" />
                        </div>
                        <span className="text-xs text-slate-400 truncate block mt-0.5 max-w-[140px]">
                          {item.name}
                        </span>
                      </div>
                      <ScoreGauge score={item.score} size="md" />
                    </div>

                    {/* Price & Change */}
                    <div className="mt-4 flex items-baseline justify-between border-t border-[#1e293b] pt-3">
                      <div>
                        <span className="text-[10px] text-slate-500 uppercase font-semibold block">Son Fiyat</span>
                        <span className="text-base font-bold font-mono-num text-white">
                          ₺{item.price.toFixed(2)}
                        </span>
                      </div>
                      <div className="text-right">
                        <span className="text-[10px] text-slate-500 uppercase font-semibold block">Günlük</span>
                        <span
                          className={`text-xs font-bold font-mono-num flex items-center gap-0.5 ${
                            (item.dailyChangePercent ?? item.changePercent ?? 0) >= 0 ? "text-emerald-400" : "text-rose-400"
                          }`}
                        >
                          {(item.dailyChangePercent ?? item.changePercent ?? 0) >= 0 ? (
                            <ArrowUpRight className="w-3 h-3" />
                          ) : (
                            <ArrowDownRight className="w-3 h-3" />
                          )}
                          %{Math.abs(item.dailyChangePercent ?? item.changePercent ?? 0).toFixed(2)}
                        </span>
                      </div>
                    </div>

                    {/* Targets & R:R */}
                    <div className="mt-3 grid grid-cols-3 gap-1 p-2 bg-[#0d121c] rounded-lg text-center border border-slate-800 text-[11px] font-mono-num">
                      <div>
                        <span className="text-[9px] text-rose-400 block font-semibold">TREND</span>
                        <span className="font-bold text-slate-300">{item.trendScore}/40</span>
                      </div>
                      <div>
                        <span className="text-[9px] text-emerald-400 block font-semibold">MOMENTUM</span>
                        <span className="font-bold text-slate-300">{item.momentumScore}/30</span>
                      </div>
                      <div>
                        <span className="text-[9px] text-sky-400 block font-semibold">HACİM</span>
                        <span className="font-bold text-sky-300">{item.volumeScore}/15</span>
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>

          {/* 4. Split Grid: Volume Surges & Breakouts */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Volume Surge Leaderboard */}
            <div className="terminal-card p-4">
              <div className="flex items-center justify-between pb-3 border-b border-[#1e293b]">
                <div className="flex items-center gap-2">
                  <Zap className="w-4 h-4 text-emerald-400" />
                  <h3 className="text-sm font-bold text-white">Hacim Patlaması Yaşayanlar (Vol {'>'} 1.3x)</h3>
                </div>
                <span className="text-[10px] text-slate-400 font-mono-num font-semibold">20 Günlük Ort. Kıyasla</span>
              </div>

              <div className="divide-y divide-[#1e293b] mt-1">
                {(overview.volumeSurges || overview.volumeLeaders || []).length === 0 ? (
                  <p className="py-6 text-center text-xs text-slate-400">Belirgin hacim artışı tespit edilmedi.</p>
                ) : (
                  (overview.volumeSurges || overview.volumeLeaders || []).map((stock) => (
                    <div key={stock.symbol || stock.ticker} className="py-2.5 flex items-center justify-between text-xs">
                      <div className="flex items-center gap-3">
                        <Link href={`/stocks/${stock.symbol || stock.ticker}`} className="font-bold font-mono-num text-white hover:text-sky-300 transition">
                          {stock.symbol || stock.ticker}
                        </Link>
                        <span className="text-slate-400 text-[11px]">{stock.sector}</span>
                      </div>
                      <div className="flex items-center gap-4">
                        <span className="font-mono-num font-bold text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                          {(stock.volumeRatio ?? 1.0).toFixed(2)}x Hacim
                        </span>
                        <span className="font-mono-num text-slate-200">₺{stock.price.toFixed(2)}</span>
                        <SignalBadge type={(stock.signal || stock.signalType) as any} size="sm" />
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Breakout Stocks */}
            <div className="terminal-card p-4">
              <div className="flex items-center justify-between pb-3 border-b border-[#1e293b]">
                <div className="flex items-center gap-2">
                  <Target className="w-4 h-4 text-sky-400" />
                  <h3 className="text-sm font-bold text-white">Yeni Direnç Kırılımı Yapanlar (Breakouts)</h3>
                </div>
                <span className="text-[10px] text-slate-400 font-mono-num font-semibold">20 Günlük Zirve</span>
              </div>

              <div className="divide-y divide-[#1e293b] mt-1">
                {(overview.breakouts || overview.breakoutStocks || []).length === 0 ? (
                  <p className="py-6 text-center text-xs text-slate-400">Direnç kırılımı gösteren hisse bulunmuyor.</p>
                ) : (
                  (overview.breakouts || overview.breakoutStocks || []).map((stock) => (
                    <div key={stock.symbol || stock.ticker} className="py-2.5 flex items-center justify-between text-xs">
                      <div className="flex items-center gap-3">
                        <Link href={`/stocks/${stock.symbol || stock.ticker}`} className="font-bold font-mono-num text-white hover:text-sky-300 transition">
                          {stock.symbol || stock.ticker}
                        </Link>
                        <span className="text-emerald-400 font-semibold text-[11px] flex items-center gap-1">
                          <ArrowUpRight className="w-3 h-3" /> Zirve Kırılımı
                        </span>
                      </div>
                      <div className="flex items-center gap-4">
                        <span className="font-mono-num text-slate-300 font-semibold">Skor: {stock.score}</span>
                        <span className="font-mono-num text-slate-200">₺{stock.price.toFixed(2)}</span>
                        <SignalBadge type={(stock.signal || stock.signalType) as any} size="sm" />
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
