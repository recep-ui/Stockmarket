"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  Lightbulb,
  Plus,
  CheckCircle2,
  Sliders,
  Sparkles,
  Layers,
  FlaskConical,
  RefreshCw
} from "lucide-react";
import Link from "next/link";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import { StrategiesApi, isMockEnabled, MOCK_STRATEGIES } from "@/lib/api";
import { StrategyDto } from "@/types";

export default function StrategiesPage() {
  const [strategies, setStrategies] = useState<StrategyDto[]>([]);
  const [selectedStrategy, setSelectedStrategy] = useState<StrategyDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const [showModal, setShowModal] = useState(false);
  const [newStrategyName, setNewStrategyName] = useState("");
  const [newStrategyDesc, setNewStrategyDesc] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await StrategiesApi.getStrategies();
      setStrategies(data);
      if (data.length > 0) {
        setSelectedStrategy(data[0]);
      }
    } catch (err: any) {
      if (isMockEnabled()) {
        setStrategies(MOCK_STRATEGIES);
        setSelectedStrategy(MOCK_STRATEGIES[0]);
      } else {
        setError(err.message || "Stratejiler yüklenirken hata oluştu.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newStrategyName.trim()) return;

    setSubmitting(true);
    try {
      const created = await StrategiesApi.createStrategy({
        name: newStrategyName.trim(),
        description: newStrategyDesc.trim() || "Kullanıcı tanımlı özel kantitatif kural seti.",
        strategyType: "Technical",
        timeframe: 250,
        rules: []
      });
      setStrategies(prev => [...prev, created]);
      setSelectedStrategy(created);
      setShowModal(false);
      setNewStrategyName("");
      setNewStrategyDesc("");
    } catch (err: any) {
      if (isMockEnabled()) {
        const mockCreated: StrategyDto = {
          id: strategies.length + 1,
          name: newStrategyName.trim(),
          description: newStrategyDesc.trim() || "Özel kural seti",
          strategyType: "Technical",
          timeframe: "Daily",
          targetTimeframe: "Daily",
          isActive: true,
          rulesCount: 3,
          rules: []
        };
        setStrategies(prev => [...prev, mockCreated]);
        setSelectedStrategy(mockCreated);
        setShowModal(false);
        setNewStrategyName("");
        setNewStrategyDesc("");
      } else {
        setError(err.message || "Strateji kaydedilemedi.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-black text-white tracking-tight flex items-center gap-2">
            <Lightbulb className="w-5 h-5 text-amber-400" />
            Strateji Laboratuvarı & Kural Motoru
          </h1>
          <p className="text-xs text-slate-400 mt-0.5">
            Önceden tanımlanmış kurumsal quant stratejilerini inceleyin veya özel gösterge kuralları oluşturun.
          </p>
        </div>

        <button
          onClick={() => setShowModal(true)}
          className="flex items-center gap-2 px-4 py-2 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-bold shadow-lg shadow-sky-600/30 transition self-start md:self-auto"
        >
          <Plus className="w-4 h-4" />
          <span>Yeni Strateji Oluştur</span>
        </button>
      </div>

      {error && (
        <ErrorBanner
          title="Strateji Verileri Alınamadı"
          message={error}
          onRetry={loadData}
        />
      )}

      {loading && strategies.length === 0 && (
        <LoadingSkeleton rows={5} text="Stratejiler yükleniyor..." />
      )}

      {!loading && strategies.length === 0 && !error && (
        <EmptyState
          title="Strateji Bulunamadı"
          description="Sistemde henüz tanımlı quant stratejisi bulunmuyor."
          actionText="İlk Stratejiyi Oluştur"
          onAction={() => setShowModal(true)}
        />
      )}

      {strategies.length > 0 && selectedStrategy && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left: Strategy Cards List */}
          <div className="space-y-3">
            <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider block mb-2">
              Mevcut Stratejiler ({strategies.length})
            </span>

            {strategies.map((strat) => {
              const isSelected = selectedStrategy?.id === strat.id;

              return (
                <div
                  key={strat.id}
                  onClick={() => setSelectedStrategy(strat)}
                  className={`p-4 rounded-xl border cursor-pointer transition ${
                    isSelected
                      ? "bg-[#162030] border-sky-500/50 shadow-lg shadow-sky-500/10"
                      : "bg-[#111722] hover:bg-[#151c28] border-[#1e293b]"
                  }`}
                >
                  <div className="flex items-center justify-between mb-1.5">
                    <h3 className={`text-sm font-bold ${isSelected ? "text-sky-300" : "text-white"}`}>
                      {strat.name}
                    </h3>
                    <span className="text-[10px] px-2 py-0.5 rounded font-mono-num font-bold bg-slate-800 text-slate-300 border border-slate-700">
                      {strat.targetTimeframe || "Daily"}
                    </span>
                  </div>

                  <p className="text-xs text-slate-400 line-clamp-2 leading-relaxed">
                    {strat.description}
                  </p>

                  <div className="mt-3 pt-2 border-t border-[#1e293b] flex items-center justify-between text-[11px]">
                    <span className="text-slate-500">{strat.rulesCount ?? (strat.rules?.length ?? 0)} Kurumsal Kural</span>
                    <span className="flex items-center gap-1 text-emerald-400 font-semibold">
                      <CheckCircle2 className="w-3 h-3" /> {strat.isActive ? "Aktif" : "Pasif"}
                    </span>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Right: Selected Strategy Rule Matrix & Backtest Shortcut */}
          <div className="lg:col-span-2 space-y-6">
            <div className="terminal-card p-5">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-[#1e293b]">
                <div>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-sky-400 block mb-1">
                    Seçili Strateji Detayı
                  </span>
                  <h2 className="text-lg font-black text-white">{selectedStrategy.name}</h2>
                </div>

                <Link
                  href={`/backtests?strategy=${selectedStrategy.id}`}
                  className="flex items-center gap-2 px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-lg shadow-emerald-600/30 transition self-start"
                >
                  <FlaskConical className="w-3.5 h-3.5" />
                  <span>Bu Stratejiyi Backtest Et</span>
                </Link>
              </div>

              <p className="text-xs text-slate-300 mt-3 leading-relaxed">
                {selectedStrategy.description}
              </p>

              {/* Rules Matrix */}
              <div className="mt-6">
                <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3">
                  Değerlendirme Kuralları & Matematiksel Filtreler
                </h3>

                <div className="space-y-2.5">
                  <div className="p-3 rounded-lg bg-[#0d121c] border border-slate-800 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <span className="w-6 h-6 rounded-full bg-sky-500/20 text-sky-400 font-mono-num font-bold flex items-center justify-center text-xs">
                        1
                      </span>
                      <div>
                        <span className="font-bold text-white block">Trend Filtresi (EMA Stack & SuperTrend)</span>
                        <span className="text-slate-400 text-[11px]">EMA 20 {'>'} EMA 50 VE EMA 50 {'>'} EMA 200 VE Fiyat {'>'} SuperTrend</span>
                      </div>
                    </div>
                    <span className="font-mono-num font-bold text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded">
                      Ağırlık: %40
                    </span>
                  </div>

                  <div className="p-3 rounded-lg bg-[#0d121c] border border-slate-800 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <span className="w-6 h-6 rounded-full bg-purple-500/20 text-purple-400 font-mono-num font-bold flex items-center justify-center text-xs">
                        2
                      </span>
                      <div>
                        <span className="font-bold text-white block">Momentum & Wilder RSI Onayı</span>
                        <span className="text-slate-400 text-[11px]">RSI(14) 50 ile 65 arasında VE MACD Histogram {'>'} 0</span>
                      </div>
                    </div>
                    <span className="font-mono-num font-bold text-purple-400 bg-purple-500/10 px-2 py-0.5 rounded">
                      Ağırlık: %30
                    </span>
                  </div>

                  <div className="p-3 rounded-lg bg-[#0d121c] border border-slate-800 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <span className="w-6 h-6 rounded-full bg-amber-500/20 text-amber-400 font-mono-num font-bold flex items-center justify-center text-xs">
                        3
                      </span>
                      <div>
                        <span className="font-bold text-white block">Kurumsal Hacim Onayı (Volume Surge)</span>
                        <span className="text-slate-400 text-[11px]">Hacim {'>'} 1.3x 20 Günlük Ortalama VE OBV Yükseliyor</span>
                      </div>
                    </div>
                    <span className="font-mono-num font-bold text-amber-400 bg-amber-500/10 px-2 py-0.5 rounded">
                      Ağırlık: %15
                    </span>
                  </div>

                  <div className="p-3 rounded-lg bg-[#0d121c] border border-slate-800 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <span className="w-6 h-6 rounded-full bg-emerald-500/20 text-emerald-400 font-mono-num font-bold flex items-center justify-center text-xs">
                        4
                      </span>
                      <div>
                        <span className="font-bold text-white block">Direnç Kırılımı & Fiyat Yapısı</span>
                        <span className="text-slate-400 text-[11px]">Son 20 günün en yüksek seviyesinin üzerinde kapanış</span>
                      </div>
                    </div>
                    <span className="font-mono-num font-bold text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded">
                      Ağırlık: %15
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Create Modal */}
      {showModal && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#111722] border border-[#283548] rounded-2xl w-full max-w-lg p-6 shadow-2xl space-y-4">
            <h2 className="text-base font-bold text-white flex items-center gap-2">
              <Plus className="w-4 h-4 text-sky-400" />
              Yeni Algoritmik Strateji Tanımla
            </h2>

            <form onSubmit={handleCreate} className="space-y-4 text-xs">
              <div>
                <label className="text-slate-400 font-semibold block mb-1">
                  Strateji Adı
                </label>
                <input
                  type="text"
                  placeholder="Örn: MACD + RSI Dip Tepkisi"
                  value={newStrategyName}
                  onChange={(e) => setNewStrategyName(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white outline-none"
                  required
                />
              </div>

              <div>
                <label className="text-slate-400 font-semibold block mb-1">
                  Açıklama & Yatırım Tezi
                </label>
                <textarea
                  rows={3}
                  placeholder="Stratejinin giriş, çıkış koşulları ve mantıksal temeli..."
                  value={newStrategyDesc}
                  onChange={(e) => setNewStrategyDesc(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white outline-none resize-none"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="px-4 py-2 rounded-lg bg-[#1a2332] text-slate-300 hover:text-white"
                >
                  İptal
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold transition disabled:opacity-50"
                >
                  {submitting ? "Kaydediliyor..." : "Stratejiyi Kaydet"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
