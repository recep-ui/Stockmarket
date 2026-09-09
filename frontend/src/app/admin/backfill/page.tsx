"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  Database,
  Play,
  Pause,
  RotateCcw,
  XCircle,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Calendar,
  Layers,
  Search,
  ExternalLink,
  ShieldAlert,
  Sliders,
  ChevronRight
} from "lucide-react";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import { BackfillApi } from "@/lib/api";
import {
  BackfillJobDto,
  UniverseCoverageSummaryDto,
  SymbolCoverageDto,
  SymbolGapsDto
} from "@/types";

export default function AdminBackfillPage() {
  const [jobs, setJobs] = useState<BackfillJobDto[]>([]);
  const [coverage, setCoverage] = useState<UniverseCoverageSummaryDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Job launcher form
  const [startDate, setStartDate] = useState("2026-01-01");
  const [endDate, setEndDate] = useState(new Date().toISOString().split("T")[0]);
  const [forceCheck, setForceCheck] = useState(false);
  const [startingJob, setStartingJob] = useState(false);

  // Search & Filter for symbol coverage
  const [symbolSearch, setSymbolSearch] = useState("");
  const [filterOnlyIncomplete, setFilterOnlyIncomplete] = useState(false);

  // Gap inspector modal
  const [selectedSymbol, setSelectedSymbol] = useState<SymbolCoverageDto | null>(null);
  const [symbolGaps, setSymbolGaps] = useState<SymbolGapsDto | null>(null);
  const [loadingGaps, setLoadingGaps] = useState(false);

  const loadData = useCallback(async () => {
    try {
      setError(null);
      const [jobsData, covData] = await Promise.all([
        BackfillApi.getBackfillJobs().catch(() => []),
        BackfillApi.getUniverseCoverage().catch(() => null)
      ]);
      setJobs(jobsData);
      setCoverage(covData);
    } catch (err: any) {
      setError(err.message || "Veriler yüklenirken hata oluştu.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
    const timer = setInterval(() => {
      // Auto-poll running jobs every 5 seconds
      BackfillApi.getBackfillJobs()
        .then((j) => setJobs(j))
        .catch(() => {});
    }, 5000);
    return () => clearInterval(timer);
  }, [loadData]);

  const handleStartBackfill = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!startDate || !endDate) return;

    setStartingJob(true);
    setError(null);
    try {
      await BackfillApi.startBackfill({
        startDate,
        endDate,
        forceRevisionCheck: forceCheck
      });
      await loadData();
    } catch (err: any) {
      setError(err.message || "Backfill başlatılamadı.");
    } finally {
      setStartingJob(false);
    }
  };

  const handlePause = async (id: number) => {
    try {
      await BackfillApi.pauseBackfill(id);
      const updated = await BackfillApi.getBackfillJobs();
      setJobs(updated);
    } catch (err: any) {
      setError(err.message || "İşlem duraklatılamadı.");
    }
  };

  const handleResume = async (id: number) => {
    try {
      await BackfillApi.resumeBackfill(id);
      const updated = await BackfillApi.getBackfillJobs();
      setJobs(updated);
    } catch (err: any) {
      setError(err.message || "İşlem sürdürülemedi.");
    }
  };

  const handleCancel = async (id: number) => {
    if (!confirm("Bu backfill görevini iptal etmek istediğinize emin misiniz?")) return;
    try {
      await BackfillApi.cancelBackfill(id);
      const updated = await BackfillApi.getBackfillJobs();
      setJobs(updated);
    } catch (err: any) {
      setError(err.message || "İşlem iptal edilemedi.");
    }
  };

  const inspectSymbolGaps = async (symbol: SymbolCoverageDto) => {
    setSelectedSymbol(symbol);
    setLoadingGaps(true);
    try {
      const gapsData = await BackfillApi.getSymbolGaps(symbol.ticker);
      setSymbolGaps(gapsData);
    } catch {
      setSymbolGaps({ ticker: symbol.ticker, totalGaps: 0, gaps: [] });
    } finally {
      setLoadingGaps(false);
    }
  };

  const filteredSymbols = (coverage?.symbolCoverages || []).filter((s) => {
    const match =
      s.ticker.toLowerCase().includes(symbolSearch.toLowerCase()) ||
      s.name.toLowerCase().includes(symbolSearch.toLowerCase());
    if (filterOnlyIncomplete) {
      return match && s.coveragePercent < 100;
    }
    return match;
  });

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-[#1e293b] pb-5">
        <div>
          <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-sky-400">
            <Database className="w-4 h-4" />
            <span>Veri Yönetimi & İnceleme</span>
          </div>
          <h1 className="text-2xl font-bold tracking-tight text-white mt-1">
            BIST Günlük Bülten Geçmiş Veri İndirme (Backfill) & Kapsam
          </h1>
          <p className="text-sm text-slate-400 mt-1">
            Resmi Borsa İstanbul Günlük Bülten arşivinden otomatik, sıralı, 2000ms nezaket gecikmeli veri aktarımı ve eksik gün analizi.
          </p>
        </div>

        <button
          onClick={loadData}
          className="flex items-center gap-2 px-3 py-2 bg-[#111722] hover:bg-[#18202d] border border-[#1e293b] text-slate-300 hover:text-white rounded-lg text-xs font-semibold transition"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? "animate-spin text-sky-400" : ""}`} />
          <span>Yenile</span>
        </button>
      </div>

      {error && <ErrorBanner message={error} onRetry={loadData} />}

      {/* Universe Coverage KPI Cards */}
      {coverage && (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          <div className="bg-[#0f172a]/60 border border-[#1e293b] rounded-xl p-3.5">
            <span className="text-[11px] font-semibold text-slate-400 uppercase">Aktif Hisseler</span>
            <div className="text-xl font-bold font-mono text-white mt-1">{coverage.activeSymbols}</div>
            <span className="text-[10px] text-slate-500">BIST Evreni</span>
          </div>

          <div className="bg-[#0f172a]/60 border border-[#1e293b] rounded-xl p-3.5">
            <span className="text-[11px] font-semibold text-slate-400 uppercase">Ort. Kapsam</span>
            <div className="text-xl font-bold font-mono text-emerald-400 mt-1">
              %{coverage.averageCoveragePercent.toFixed(1)}
            </div>
            <span className="text-[10px] text-slate-500">Tarihsel Oturumlar</span>
          </div>

          <div className="bg-[#0f172a]/60 border border-[#1e293b] rounded-xl p-3.5">
            <span className="text-[11px] font-semibold text-slate-400 uppercase">&ge; 300 Bar</span>
            <div className="text-xl font-bold font-mono text-sky-400 mt-1">{coverage.symbolsWith300PlusBars}</div>
            <span className="text-[10px] text-slate-500">EMA200 Stabil</span>
          </div>

          <div className="bg-[#0f172a]/60 border border-[#1e293b] rounded-xl p-3.5">
            <span className="text-[11px] font-semibold text-slate-400 uppercase">&ge; 500 Bar</span>
            <div className="text-xl font-bold font-mono text-indigo-400 mt-1">{coverage.symbolsWith500PlusBars}</div>
            <span className="text-[10px] text-slate-500">2+ Yıllık Geçmiş</span>
          </div>

          <div className="bg-[#0f172a]/60 border border-[#1e293b] rounded-xl p-3.5">
            <span className="text-[11px] font-semibold text-slate-400 uppercase">Eksik Oturum</span>
            <div className="text-xl font-bold font-mono text-amber-400 mt-1">{coverage.totalMissingSessions}</div>
            <span className="text-[10px] text-slate-500">Toplam Boşluk</span>
          </div>

          <div className="bg-[#0f172a]/60 border border-[#1e293b] rounded-xl p-3.5">
            <span className="text-[11px] font-semibold text-slate-400 uppercase">Sermaye Uyarısı</span>
            <div className="text-xl font-bold font-mono text-rose-400 mt-1">{coverage.symbolsWithCorporateActionWarnings}</div>
            <span className="text-[10px] text-slate-500">Bekleyen Aksiyon</span>
          </div>
        </div>
      )}

      {/* Backfill Launcher Form */}
      <div className="bg-[#0f172a]/80 border border-[#1e293b] rounded-2xl p-5 shadow-lg">
        <div className="flex items-center gap-2 mb-4">
          <Calendar className="w-4 h-4 text-sky-400" />
          <h2 className="text-base font-bold text-white">Yeni Geçmiş Veri İndirme Görevi Başlat</h2>
        </div>

        <form onSubmit={handleStartBackfill} className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 items-end">
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1.5">Başlangıç Tarihi</label>
            <input
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              max={endDate}
              required
              className="w-full bg-[#131923] border border-[#27354a] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500 font-mono"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1.5">Bitiş Tarihi</label>
            <input
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              min={startDate}
              max={new Date().toISOString().split("T")[0]}
              required
              className="w-full bg-[#131923] border border-[#27354a] rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500 font-mono"
            />
          </div>

          <div className="flex items-center gap-2.5 pb-2">
            <input
              type="checkbox"
              id="forceRevisionCheck"
              checked={forceCheck}
              onChange={(e) => setForceCheck(e.target.checked)}
              className="rounded bg-[#131923] border-slate-700 text-sky-500 focus:ring-0 w-4 h-4"
            />
            <label htmlFor="forceRevisionCheck" className="text-xs text-slate-300 cursor-pointer select-none">
              Revizyon Kontrolünü Zorla (Mevcut bültenleri tekrar kontrol et)
            </label>
          </div>

          <div>
            <button
              type="submit"
              disabled={startingJob}
              className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-gradient-to-r from-sky-500 to-blue-600 hover:from-sky-400 hover:to-blue-500 text-white font-semibold text-xs rounded-lg shadow-md transition disabled:opacity-50"
            >
              {startingJob ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  <span>Başlatılıyor...</span>
                </>
              ) : (
                <>
                  <Play className="w-3.5 h-3.5" />
                  <span>Backfill Başlat</span>
                </>
              )}
            </button>
          </div>
        </form>

        <div className="mt-3 text-[11px] text-slate-500 flex items-center gap-1.5">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
          <span>Sıralı istekler arasında minimum 2000ms nezaket gecikmesi uygulanır. Mevcut yerel arşiv dosyaları yeniden indirilmez.</span>
        </div>
      </div>

      {/* Backfill Jobs List */}
      <div className="bg-[#0f172a]/80 border border-[#1e293b] rounded-2xl p-5 shadow-lg">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <Layers className="w-4 h-4 text-sky-400" />
            <h2 className="text-base font-bold text-white">Backfill Görevleri</h2>
          </div>
          <span className="text-xs text-slate-400 font-mono">{jobs.length} Görev</span>
        </div>

        {jobs.length === 0 ? (
          <EmptyState
            title="Henüz Görev Yok"
            description="Yukarıdaki panelden tarih aralığı belirleyerek ilk geçmiş veri indirme görevini başlatabilirsiniz."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="border-b border-[#1e293b] text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                  <th className="pb-3 pr-2">ID</th>
                  <th className="pb-3 px-2">Aralık</th>
                  <th className="pb-3 px-2">Durum</th>
                  <th className="pb-3 px-2">İlerleme</th>
                  <th className="pb-3 px-2">Başarılı</th>
                  <th className="pb-3 px-2">Atlanan</th>
                  <th className="pb-3 px-2">Hatalı</th>
                  <th className="pb-3 px-2">Bar Sayısı</th>
                  <th className="pb-3 px-2">Başlangıç</th>
                  <th className="pb-3 pl-2 text-right">İşlemler</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#1e293b]/60 text-slate-300">
                {jobs.map((job) => {
                  const progressPct =
                    job.sessionsTotal > 0
                      ? Math.min(100, Math.round(((job.sessionsCompleted + job.sessionsSkipped) / job.sessionsTotal) * 100))
                      : 0;

                  return (
                    <tr key={job.id} className="hover:bg-[#131923]/60 transition">
                      <td className="py-3 pr-2 font-mono text-slate-400">#{job.id}</td>
                      <td className="py-3 px-2 font-mono">
                        {job.startDate} &rarr; {job.endDate}
                      </td>
                      <td className="py-3 px-2">
                        <span
                          className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider ${
                            job.status === "Running"
                              ? "bg-sky-500/15 text-sky-400 border border-sky-500/30 animate-pulse"
                              : job.status === "Completed"
                              ? "bg-emerald-500/15 text-emerald-400 border border-emerald-500/30"
                              : job.status === "Paused"
                              ? "bg-amber-500/15 text-amber-400 border border-amber-500/30"
                              : "bg-rose-500/15 text-rose-400 border border-rose-500/30"
                          }`}
                        >
                          {job.status}
                        </span>
                      </td>
                      <td className="py-3 px-2 min-w-[140px]">
                        <div className="flex items-center justify-between text-[10px] mb-1 font-mono">
                          <span>{job.sessionsCompleted + job.sessionsSkipped} / {job.sessionsTotal}</span>
                          <span>%{progressPct}</span>
                        </div>
                        <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                          <div
                            className={`h-full transition-all duration-300 ${
                              job.status === "Completed" ? "bg-emerald-400" : "bg-sky-400"
                            }`}
                            style={{ width: `${progressPct}%` }}
                          />
                        </div>
                      </td>
                      <td className="py-3 px-2 font-mono text-emerald-400">{job.sessionsCompleted}</td>
                      <td className="py-3 px-2 font-mono text-slate-400">{job.sessionsSkipped}</td>
                      <td className="py-3 px-2 font-mono text-rose-400">{job.sessionsFailed}</td>
                      <td className="py-3 px-2 font-mono text-sky-300">+{job.barsInserted.toLocaleString()}</td>
                      <td className="py-3 px-2 text-[11px] text-slate-500 font-mono">
                        {new Date(job.startedAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}
                      </td>
                      <td className="py-3 pl-2 text-right">
                        <div className="flex items-center justify-end gap-1">
                          {job.status === "Running" && (
                            <button
                              onClick={() => handlePause(job.id)}
                              title="Duraklat"
                              className="p-1.5 bg-[#131923] hover:bg-amber-500/20 text-amber-400 rounded border border-[#1e293b] transition"
                            >
                              <Pause className="w-3 h-3" />
                            </button>
                          )}
                          {job.status === "Paused" && (
                            <button
                              onClick={() => handleResume(job.id)}
                              title="Sürdür"
                              className="p-1.5 bg-[#131923] hover:bg-sky-500/20 text-sky-400 rounded border border-[#1e293b] transition"
                            >
                              <Play className="w-3 h-3" />
                            </button>
                          )}
                          {(job.status === "Running" || job.status === "Paused" || job.status === "Pending") && (
                            <button
                              onClick={() => handleCancel(job.id)}
                              title="İptal Et"
                              className="p-1.5 bg-[#131923] hover:bg-rose-500/20 text-rose-400 rounded border border-[#1e293b] transition"
                            >
                              <XCircle className="w-3 h-3" />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Symbol Coverage & Gap Inspector */}
      <div className="bg-[#0f172a]/80 border border-[#1e293b] rounded-2xl p-5 shadow-lg">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
          <div>
            <h2 className="text-base font-bold text-white">Hisse Bazlı Veri Kapsamı & Eksik Gün Denetimi</h2>
            <p className="text-xs text-slate-400">
              Her bir hissenin mevcut bar sayısı, EMA200 kararlılık durumu ve bülten uyumsuzlukları.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <div className="relative">
              <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                placeholder="Hisse ara (örn: THYAO)..."
                value={symbolSearch}
                onChange={(e) => setSymbolSearch(e.target.value)}
                className="bg-[#131923] border border-[#27354a] rounded-lg pl-8 pr-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-sky-500"
              />
            </div>

            <label className="flex items-center gap-1.5 text-xs text-slate-300 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={filterOnlyIncomplete}
                onChange={(e) => setFilterOnlyIncomplete(e.target.checked)}
                className="rounded bg-[#131923] border-slate-700 text-sky-500 focus:ring-0 w-3.5 h-3.5"
              />
              <span>Yalnızca Eksikleri Göster</span>
            </label>
          </div>
        </div>

        {loading ? (
          <LoadingSkeleton rows={5} />
        ) : (
          <div className="overflow-x-auto max-h-[420px] overflow-y-auto">
            <table className="w-full text-left text-xs">
              <thead className="sticky top-0 bg-[#0f172a] border-b border-[#1e293b]">
                <tr className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                  <th className="pb-2.5 pr-2">Kod</th>
                  <th className="pb-2.5 px-2">Şirket</th>
                  <th className="pb-2.5 px-2">Bar Sayısı</th>
                  <th className="pb-2.5 px-2">Beklenen</th>
                  <th className="pb-2.5 px-2">Eksik</th>
                  <th className="pb-2.5 px-2">Kapsam %</th>
                  <th className="pb-2.5 px-2">EMA200</th>
                  <th className="pb-2.5 px-2">Sermaye Aksiyonu</th>
                  <th className="pb-2.5 pl-2 text-right">İncele</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#1e293b]/60 text-slate-300">
                {filteredSymbols.slice(0, 100).map((s) => (
                  <tr key={s.ticker} className="hover:bg-[#131923]/60 transition">
                    <td className="py-2.5 pr-2 font-mono font-bold text-white">{s.ticker}</td>
                    <td className="py-2.5 px-2 text-slate-400 truncate max-w-[180px]">{s.name}</td>
                    <td className="py-2.5 px-2 font-mono text-sky-300">{s.dailyBarCount}</td>
                    <td className="py-2.5 px-2 font-mono text-slate-400">{s.expectedTradingSessions}</td>
                    <td className="py-2.5 px-2 font-mono">
                      {s.missingSessions > 0 ? (
                        <span className="text-rose-400 font-bold">-{s.missingSessions}</span>
                      ) : (
                        <span className="text-slate-500">0</span>
                      )}
                    </td>
                    <td className="py-2.5 px-2 font-mono">
                      <span
                        className={
                          s.coveragePercent >= 95
                            ? "text-emerald-400"
                            : s.coveragePercent >= 80
                            ? "text-amber-400"
                            : "text-rose-400"
                        }
                      >
                        %{s.coveragePercent.toFixed(1)}
                      </span>
                    </td>
                    <td className="py-2.5 px-2">
                      {s.ema200Ready ? (
                        <span className="inline-flex items-center gap-1 text-[10px] text-emerald-400 font-semibold">
                          <CheckCircle2 className="w-3 h-3" /> Hazır
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-[10px] text-amber-400 font-semibold">
                          <AlertTriangle className="w-3 h-3" /> &lt;220 Bar
                        </span>
                      )}
                    </td>
                    <td className="py-2.5 px-2">
                      {s.hasCorporateActionWarning ? (
                        <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-bold bg-rose-500/20 text-rose-400 border border-rose-500/30">
                          <ShieldAlert className="w-3 h-3" /> İnceleme Gerekli
                        </span>
                      ) : (
                        <span className="text-[10px] text-slate-500">Temiz</span>
                      )}
                    </td>
                    <td className="py-2.5 pl-2 text-right">
                      <button
                        onClick={() => inspectSymbolGaps(s)}
                        className="px-2 py-1 bg-[#131923] hover:bg-sky-500/20 text-sky-400 rounded border border-[#1e293b] text-[11px] font-semibold transition"
                      >
                        Boşlukları İncele
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Gap Inspector Drawer / Modal */}
      {selectedSymbol && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#0f172a] border border-[#1e293b] rounded-2xl w-full max-w-2xl max-h-[85vh] flex flex-col shadow-2xl overflow-hidden">
            <div className="p-4 border-b border-[#1e293b] flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-white flex items-center gap-2">
                  <span className="font-mono text-sky-400">{selectedSymbol.ticker}</span>
                  <span className="text-slate-400 text-xs font-normal">Tarihsel Boşluk İncelemesi</span>
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">{selectedSymbol.name}</p>
              </div>

              <button
                onClick={() => setSelectedSymbol(null)}
                className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
              >
                <XCircle className="w-5 h-5" />
              </button>
            </div>

            <div className="p-4 overflow-y-auto space-y-3 flex-1">
              {loadingGaps ? (
                <LoadingSkeleton rows={4} />
              ) : symbolGaps && symbolGaps.gaps.length > 0 ? (
                <div className="space-y-2">
                  <div className="text-xs text-slate-400 font-semibold mb-2">
                    Toplam {symbolGaps.totalGaps} eksik seans tespit edildi:
                  </div>

                  {symbolGaps.gaps.map((gap, idx) => (
                    <div
                      key={idx}
                      className="p-3 bg-[#131923] border border-[#1e293b] rounded-lg flex items-center justify-between text-xs"
                    >
                      <div className="flex items-center gap-3">
                        <span className="font-mono text-white font-bold">{gap.sessionDate}</span>
                        <span className="text-slate-400">{gap.description}</span>
                      </div>

                      <span
                        className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider ${
                          gap.reason === "BulletinMissing"
                            ? "bg-amber-500/15 text-amber-400 border border-amber-500/30"
                            : gap.reason === "Suspended"
                            ? "bg-purple-500/15 text-purple-400 border border-purple-500/30"
                            : gap.reason === "NoTrade"
                            ? "bg-slate-500/15 text-slate-400 border border-slate-500/30"
                            : "bg-rose-500/15 text-rose-400 border border-rose-500/30"
                        }`}
                      >
                        {gap.reason}
                      </span>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-8 text-slate-400 text-xs">
                  <CheckCircle2 className="w-8 h-8 text-emerald-400 mx-auto mb-2" />
                  Bu hisse senedi için beklenen tüm BIST işlem seanslarında eksiksiz OHLCV verisi mevcuttur.
                </div>
              )}
            </div>

            <div className="p-3 border-t border-[#1e293b] bg-[#0b0f17] flex justify-end">
              <button
                onClick={() => setSelectedSymbol(null)}
                className="px-4 py-1.5 bg-[#1e293b] hover:bg-[#27354a] text-white text-xs font-semibold rounded-lg transition"
              >
                Kapat
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
