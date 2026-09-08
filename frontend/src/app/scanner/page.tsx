"use client";

import React, { useState, useMemo, useEffect, useCallback } from "react";
import Link from "next/link";
import {
  Search,
  ArrowUpDown,
  ArrowUpRight,
  ArrowDownRight,
  Filter,
  RefreshCw,
  ExternalLink
} from "lucide-react";
import { SignalBadge } from "@/components/ui/Badge";
import { ScoreGauge } from "@/components/ui/ScoreGauge";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import { ScannerApi, isMockEnabled, MOCK_SCANNER_ITEMS } from "@/lib/api";
import { ScannerItemDto } from "@/types";

export default function ScannerPage() {
  const [items, setItems] = useState<ScannerItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedSignal, setSelectedSignal] = useState<string>("ALL");
  const [minScore, setMinScore] = useState<number>(0);
  const [selectedSector, setSelectedSector] = useState<string>("ALL");
  const [sortField, setSortField] = useState<keyof ScannerItemDto>("score");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [activePreset, setActivePreset] = useState<string>("ALL");
  const [isScanning, setIsScanning] = useState<boolean>(false);

  const sectors = ["ALL", "Ulaştırma", "Savunma", "Enerji", "Otomotiv", "Sanayi", "Metal", "Holding", "Perakende", "Bankacılık", "Kimya"];

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await ScannerApi.getScannerResults({
        timeframe: "Daily",
        page: 1,
        pageSize: 100
      });
      setItems(res.items || []);
    } catch (err: any) {
      if (isMockEnabled()) {
        setItems(MOCK_SCANNER_ITEMS);
      } else {
        setError(err.message || "Tarama sonuçları yüklenirken hata oluştu.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleTriggerScan = async () => {
    setIsScanning(true);
    try {
      await ScannerApi.runScan("Daily");
      await loadData();
    } catch (err: any) {
      if (!isMockEnabled()) {
        setError(err.message || "Tarama işlemi başlatılamadı.");
      }
    } finally {
      setIsScanning(false);
    }
  };

  const applyPreset = (preset: string) => {
    setActivePreset(preset);
    if (preset === "ALL") {
      setSelectedSignal("ALL");
      setMinScore(0);
    } else if (preset === "HIGH_SCORE") {
      setSelectedSignal("ALL");
      setMinScore(80);
    } else if (preset === "STRONG_BUY") {
      setSelectedSignal("StrongBuy");
      setMinScore(80);
    } else if (preset === "VOLUME_SURGE") {
      setSelectedSignal("ALL");
      setMinScore(70);
    } else if (preset === "OVERSOLD") {
      setSelectedSignal("ALL");
      setMinScore(50);
    }
  };

  const handleSort = (field: keyof ScannerItemDto) => {
    if (sortField === field) {
      setSortDirection(sortDirection === "asc" ? "desc" : "asc");
    } else {
      setSortField(field);
      setSortDirection("desc");
    }
  };

  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      const ticker = item.symbol || item.ticker || "";
      const name = item.name || "";
      if (
        searchTerm &&
        !ticker.toLowerCase().includes(searchTerm.toLowerCase()) &&
        !name.toLowerCase().includes(searchTerm.toLowerCase())
      ) {
        return false;
      }

      const sig = item.signal || item.signalType;
      if (selectedSignal !== "ALL" && sig !== selectedSignal) {
        return false;
      }

      if (item.score < minScore) {
        return false;
      }

      if (selectedSector !== "ALL" && item.sector !== selectedSector) {
        return false;
      }

      if (activePreset === "VOLUME_SURGE" && !item.isVolumeSurge && (item.volumeRatio ?? 0) < 1.3) {
        return false;
      }

      return true;
    }).sort((a, b) => {
      const valA = a[sortField];
      const valB = b[sortField];
      if (typeof valA === "number" && typeof valB === "number") {
        return sortDirection === "asc" ? valA - valB : valB - valA;
      }
      return 0;
    });
  }, [items, searchTerm, selectedSignal, minScore, selectedSector, sortField, sortDirection, activePreset]);

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-black text-white tracking-tight flex items-center gap-2">
            <Filter className="w-5 h-5 text-sky-400" />
            BIST Hisse Filtreleme & Algoritmik Tarama
          </h1>
          <p className="text-xs text-slate-400 mt-0.5">
            BIST 100 hisselerini kantitatif göstergeler, kırılımlar ve risk/getiri oranlarına göre anlık sıralayın.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleTriggerScan}
            disabled={isScanning || loading}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold shadow-lg shadow-sky-600/20 transition disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isScanning ? "animate-spin" : ""}`} />
            <span>{isScanning ? "Taranıyor..." : "Piyasayı Yeniden Tara"}</span>
          </button>
        </div>
      </div>

      {/* Quick Presets */}
      <div className="flex flex-wrap items-center gap-1.5 bg-[#111722] p-1.5 rounded-lg border border-[#1e293b]">
        <span className="text-[10px] text-slate-500 font-bold uppercase px-2">Ön Ayarlar:</span>
        <button
          onClick={() => applyPreset("ALL")}
          className={`px-2.5 py-1 text-xs font-semibold rounded transition ${
            activePreset === "ALL" ? "bg-sky-600 text-white" : "text-slate-400 hover:text-white"
          }`}
        >
          Tüm Hisseler
        </button>
        <button
          onClick={() => applyPreset("HIGH_SCORE")}
          className={`px-2.5 py-1 text-xs font-semibold rounded transition ${
            activePreset === "HIGH_SCORE" ? "bg-sky-600 text-white" : "text-slate-400 hover:text-white"
          }`}
        >
          🔥 Skor 80+
        </button>
        <button
          onClick={() => applyPreset("STRONG_BUY")}
          className={`px-2.5 py-1 text-xs font-semibold rounded transition ${
            activePreset === "STRONG_BUY" ? "bg-emerald-600 text-white" : "text-slate-400 hover:text-white"
          }`}
        >
          🚀 Güçlü Al
        </button>
        <button
          onClick={() => applyPreset("VOLUME_SURGE")}
          className={`px-2.5 py-1 text-xs font-semibold rounded transition ${
            activePreset === "VOLUME_SURGE" ? "bg-purple-600 text-white" : "text-slate-400 hover:text-white"
          }`}
        >
          ⚡ Hacimli Kırılımlar
        </button>
      </div>

      {/* Filter Control Bar */}
      <div className="terminal-card p-4 space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Search */}
          <div>
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1.5">
              Hisse Arama
            </label>
            <div className="relative">
              <Search className="w-4 h-4 text-slate-500 absolute left-3 top-2.5" />
              <input
                type="text"
                placeholder="Örn: THYAO, Aselsan..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg pl-9 pr-3 py-1.5 text-xs text-white placeholder-slate-500 outline-none transition"
              />
            </div>
          </div>

          {/* Signal Filter */}
          <div>
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1.5">
              Sinyal Sınıfı
            </label>
            <select
              value={selectedSignal}
              onChange={(e) => setSelectedSignal(e.target.value)}
              className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-1.5 text-xs text-white outline-none transition"
            >
              <option value="ALL">Tüm Sinyaller</option>
              <option value="StrongBuy">Güçlü Al</option>
              <option value="Buy">Al</option>
              <option value="BuyCandidate">Al Adayı</option>
              <option value="Watch">İzle</option>
              <option value="Weak">Zayıf</option>
              <option value="Sell">Sat</option>
            </select>
          </div>

          {/* Min Score Slider */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-[11px] text-slate-400 font-bold uppercase">
                Min. Quant Skoru
              </label>
              <span className="text-xs font-mono-num font-bold text-sky-400">{minScore} / 100</span>
            </div>
            <input
              type="range"
              min={0}
              max={100}
              step={5}
              value={minScore}
              onChange={(e) => setMinScore(Number(e.target.value))}
              className="w-full accent-sky-500 cursor-pointer"
            />
          </div>

          {/* Sector */}
          <div>
            <label className="text-[11px] text-slate-400 font-bold uppercase block mb-1.5">
              Sektör
            </label>
            <select
              value={selectedSector}
              onChange={(e) => setSelectedSector(e.target.value)}
              className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-1.5 text-xs text-white outline-none transition"
            >
              {sectors.map((s) => (
                <option key={s} value={s}>{s === "ALL" ? "Tüm Sektörler" : s}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Error state */}
      {error && (
        <ErrorBanner
          title="Tarama Sonuçları Alınamadı"
          message={error}
          onRetry={loadData}
        />
      )}

      {/* Loading state */}
      {loading && items.length === 0 && (
        <LoadingSkeleton rows={8} text="Hisse listesi ve göstergeler yükleniyor..." />
      )}

      {/* Results Table */}
      {!loading && filteredItems.length === 0 && !error && (
        <EmptyState
          title="Filtreye Uygun Hisse Bulunamadı"
          description="Arama kriterlerinizi genişleterek tekrar deneyin veya yeni bir tarama başlatın."
          actionText="Filtreleri Sıfırla"
          onAction={() => applyPreset("ALL")}
        />
      )}

      {filteredItems.length > 0 && (
        <div className="terminal-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-[#0e1420] text-slate-400 uppercase font-semibold border-b border-[#1e293b]">
                <tr>
                  <th className="py-3 px-4">Hisse</th>
                  <th className="py-3 px-3 cursor-pointer hover:text-white transition" onClick={() => handleSort("score")}>
                    <div className="flex items-center gap-1">
                      <span>Quant Skoru</span>
                      <ArrowUpDown className="w-3 h-3" />
                    </div>
                  </th>
                  <th className="py-3 px-3">Sinyal</th>
                  <th className="py-3 px-3 cursor-pointer hover:text-white transition" onClick={() => handleSort("price")}>
                    <div className="flex items-center gap-1">
                      <span>Fiyat</span>
                      <ArrowUpDown className="w-3 h-3" />
                    </div>
                  </th>
                  <th className="py-3 px-3 cursor-pointer hover:text-white transition" onClick={() => handleSort("dailyChangePercent")}>
                    <div className="flex items-center gap-1">
                      <span>Değişim</span>
                      <ArrowUpDown className="w-3 h-3" />
                    </div>
                  </th>
                  <th className="py-3 px-3">Trend / Mom / Hacim</th>
                  <th className="py-3 px-3">RSI (14)</th>
                  <th className="py-3 px-3">Hacim Oranı</th>
                  <th className="py-3 px-4 text-right">Detay</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#1e293b] font-mono-num">
                {filteredItems.map((item) => {
                  const ticker = item.symbol || item.ticker || "";
                  const chg = item.dailyChangePercent ?? item.changePercent ?? 0;
                  const sig = (item.signal || item.signalType) as any;

                  return (
                    <tr key={ticker} className="hover:bg-slate-800/30 transition">
                      <td className="py-3 px-4 font-bold text-white">
                        <Link href={`/stocks/${ticker}`} className="hover:text-sky-300 transition block">
                          <span className="text-sm">{ticker}</span>
                          <span className="text-[10px] text-slate-400 block font-normal font-sans truncate max-w-[150px]">
                            {item.name}
                          </span>
                        </Link>
                      </td>
                      <td className="py-3 px-3">
                        <div className="flex items-center gap-2">
                          <ScoreGauge score={item.score} size="sm" />
                          <span className="font-bold text-white text-sm">{item.score}</span>
                        </div>
                      </td>
                      <td className="py-3 px-3">
                        <SignalBadge type={sig} size="sm" />
                      </td>
                      <td className="py-3 px-3 font-bold text-slate-200">
                        ₺{item.price.toFixed(2)}
                      </td>
                      <td className="py-3 px-3 font-bold">
                        <span className={`inline-flex items-center gap-0.5 ${chg >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                          {chg >= 0 ? <ArrowUpRight className="w-3 h-3" /> : <ArrowDownRight className="w-3 h-3" />}
                          %{Math.abs(chg).toFixed(2)}
                        </span>
                      </td>
                      <td className="py-3 px-3 text-[11px] text-slate-400 font-sans">
                        <span className="text-slate-300 font-bold">{item.trendScore}</span>
                        <span className="text-slate-600"> / </span>
                        <span className="text-slate-300 font-bold">{item.momentumScore}</span>
                        <span className="text-slate-600"> / </span>
                        <span className="text-slate-300 font-bold">{item.volumeScore}</span>
                      </td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded text-[11px] font-bold ${
                          (item.rsi ?? 50) > 70 ? "text-rose-400 bg-rose-500/10" :
                          (item.rsi ?? 50) < 30 ? "text-emerald-400 bg-emerald-500/10" : "text-slate-300"
                        }`}>
                          {item.rsi !== null && item.rsi !== undefined ? item.rsi.toFixed(1) : "—"}
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        <span className={`font-bold ${
                          (item.volumeRatio ?? 1) >= 1.4 ? "text-emerald-400" : "text-slate-400"
                        }`}>
                          {item.volumeRatio ? `${item.volumeRatio.toFixed(2)}x` : "—"}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-right">
                        <Link
                          href={`/stocks/${ticker}`}
                          className="inline-flex items-center gap-1 px-2.5 py-1 rounded bg-slate-800 hover:bg-sky-600 text-slate-300 hover:text-white transition text-[11px] font-sans font-semibold"
                        >
                          İncele <ExternalLink className="w-3 h-3" />
                        </Link>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
