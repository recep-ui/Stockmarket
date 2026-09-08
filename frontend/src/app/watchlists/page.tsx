"use client";

import React, { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import {
  Bookmark,
  Plus,
  Trash2,
  ExternalLink,
  ArrowUpRight,
  ArrowDownRight,
  CheckCircle2,
  AlertCircle
} from "lucide-react";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import { SignalBadge } from "@/components/ui/Badge";
import { ScoreGauge } from "@/components/ui/ScoreGauge";
import {
  WatchlistsApi,
  MarketDataApi,
  isMockEnabled
} from "@/lib/api";
import { WatchlistDto, SymbolDto } from "@/types";

export default function WatchlistsPage() {
  const [watchlists, setWatchlists] = useState<WatchlistDto[]>([]);
  const [activeListId, setActiveListId] = useState<number | null>(null);
  const [symbols, setSymbols] = useState<SymbolDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newListName, setNewListName] = useState("");
  const [newListDesc, setNewListDesc] = useState("");
  const [creatingList, setCreatingList] = useState(false);

  const [showAddModal, setShowAddModal] = useState(false);
  const [selectedTicker, setSelectedTicker] = useState<string>("THYAO");
  const [addingItem, setAddingItem] = useState(false);

  const [feedback, setFeedback] = useState<{ success: boolean; message: string } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [lists, syms] = await Promise.all([
        WatchlistsApi.getWatchlists(),
        MarketDataApi.getSymbols().catch(() => [])
      ]);

      setWatchlists(lists);
      setSymbols(syms);

      if (lists.length > 0 && !activeListId) {
        setActiveListId(lists[0].id);
      }
      if (syms.length > 0) {
        setSelectedTicker(syms[0].ticker);
      }
    } catch (err: any) {
      if (isMockEnabled()) {
        const mockLists: WatchlistDto[] = [
          {
            id: 1,
            userId: 1,
            name: "BIST Gözde Hisselerim",
            description: "Yakından takip edilen yüksek potansiyelli hisseler",
            isDefault: true,
            items: [
              { id: 1, symbolId: 1, ticker: "THYAO", symbol: "THYAO", name: "Türk Hava Yolları", currentPrice: 326.50, price: 326.50, changePercent: 2.84 },
              { id: 2, symbolId: 2, ticker: "ASELS", symbol: "ASELS", name: "Aselsan", currentPrice: 64.20, price: 64.20, changePercent: 3.21 }
            ]
          }
        ];
        setWatchlists(mockLists);
        setActiveListId(1);
      } else {
        setError(err.message || "Takip listeleri yüklenirken hata oluştu.");
      }
    } finally {
      setLoading(false);
    }
  }, [activeListId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const activeList = watchlists.find((w) => w.id === activeListId) || watchlists[0];

  const handleCreateList = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newListName.trim()) return;

    setCreatingList(true);
    try {
      const created = await WatchlistsApi.createWatchlist(newListName.trim(), newListDesc.trim());
      setWatchlists((prev) => [...prev, created]);
      setActiveListId(created.id);
      setShowCreateModal(false);
      setNewListName("");
      setNewListDesc("");
      setFeedback({ success: true, message: `'${created.name}' listesi oluşturuldu.` });
      setTimeout(() => setFeedback(null), 3000);
    } catch (err: any) {
      setFeedback({ success: false, message: err.message || "Liste oluşturulamadı." });
    } finally {
      setCreatingList(false);
    }
  };

  const handleAddItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeList || !selectedTicker) return;

    setAddingItem(true);
    try {
      const updated = await WatchlistsApi.addItem(activeList.id, selectedTicker);
      setWatchlists((prev) => prev.map((w) => (w.id === updated.id ? updated : w)));
      setShowAddModal(false);
      setFeedback({ success: true, message: `${selectedTicker} listeye eklendi.` });
      setTimeout(() => setFeedback(null), 3000);
    } catch (err: any) {
      setFeedback({ success: false, message: err.message || "Hisse eklenemedi." });
    } finally {
      setAddingItem(false);
    }
  };

  const handleRemoveItem = async (symbolId: number) => {
    if (!activeList) return;

    try {
      await WatchlistsApi.removeItem(activeList.id, symbolId);
      setWatchlists((prev) =>
        prev.map((w) =>
          w.id === activeList.id
            ? { ...w, items: w.items.filter((item) => item.symbolId !== symbolId) }
            : w
        )
      );
    } catch (err: any) {
      setFeedback({ success: false, message: err.message || "Hisse listeden silinemedi." });
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-black text-white tracking-tight flex items-center gap-2">
            <Bookmark className="w-5 h-5 text-sky-400" />
            Özel Takip Listeleri (Watchlists)
          </h1>
          <p className="text-xs text-slate-400 mt-0.5">
            İlgilendiğiniz BIST hisselerini listeler halinde gruplayın, anlık quant skorlarını ve sinyallerini takip edin.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-bold shadow-lg shadow-sky-600/30 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Yeni Liste Oluştur</span>
          </button>
        </div>
      </div>

      {feedback && (
        <div className={`p-3 rounded-xl border text-xs font-semibold flex items-center gap-2 ${
          feedback.success ? "bg-emerald-500/10 border-emerald-500/30 text-emerald-300" : "bg-rose-500/10 border-rose-500/30 text-rose-300"
        }`}>
          {feedback.success ? <CheckCircle2 className="w-4 h-4 shrink-0" /> : <AlertCircle className="w-4 h-4 shrink-0" />}
          <span>{feedback.message}</span>
        </div>
      )}

      {error && (
        <ErrorBanner
          title="Takip Listesi Hatası"
          message={error}
          onRetry={loadData}
        />
      )}

      {loading && watchlists.length === 0 && (
        <LoadingSkeleton rows={4} text="Takip listeleri yükleniyor..." />
      )}

      {!loading && watchlists.length === 0 && !error && (
        <EmptyState
          title="Henüz Takip Listeniz Yok"
          description="Hisselerinizi organize etmek ve takip etmek için ilk listenizi hemen oluşturun."
          actionText="Takip Listesi Oluştur"
          onAction={() => setShowCreateModal(true)}
        />
      )}

      {watchlists.length > 0 && (
        <div className="space-y-4">
          {/* Watchlists Tab Bar */}
          <div className="flex items-center justify-between border-b border-[#1e293b] pb-2">
            <div className="flex items-center gap-2 overflow-x-auto">
              {watchlists.map((w) => {
                const isActive = w.id === activeList?.id;
                return (
                  <button
                    key={w.id}
                    onClick={() => setActiveListId(w.id)}
                    className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition flex items-center gap-2 ${
                      isActive
                        ? "bg-sky-600 text-white shadow"
                        : "text-slate-400 hover:text-white bg-[#111722]"
                    }`}
                  >
                    <span>{w.name}</span>
                    <span className="text-[10px] px-1.5 py-0.2 rounded bg-black/20 font-mono-num font-normal">
                      {w.items.length}
                    </span>
                  </button>
                );
              })}
            </div>

            <button
              onClick={() => setShowAddModal(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold transition shrink-0"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Hisse Ekle</span>
            </button>
          </div>

          {/* Active Watchlist Content */}
          {activeList && (
            <div>
              {activeList.items.length === 0 ? (
                <EmptyState
                  title="Bu Listede Hisse Bulunmuyor"
                  description="Listeye hisse ekleyerek anlık skorlarını ve teknik sinyallerini izleyin."
                  actionText="Hisse Ekle"
                  onAction={() => setShowAddModal(true)}
                />
              ) : (
                <div className="terminal-card overflow-hidden">
                  <div className="overflow-x-auto">
                    <table className="w-full text-left text-xs">
                      <thead className="bg-[#0e1420] text-slate-400 uppercase font-semibold border-b border-[#1e293b]">
                        <tr>
                          <th className="py-3 px-4">Hisse</th>
                          <th className="py-3 px-3">Quant Skoru</th>
                          <th className="py-3 px-3">Sinyal Durumu</th>
                          <th className="py-3 px-3">Son Fiyat</th>
                          <th className="py-3 px-3">Günlük Değişim</th>
                          <th className="py-3 px-3">Notlar</th>
                          <th className="py-3 px-4 text-right">İşlemler</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[#1e293b] font-mono-num">
                        {activeList.items.map((item) => {
                          const ticker = item.ticker || item.symbol || "";
                          const price = item.currentPrice ?? item.price ?? 0;
                          const chg = item.changePercent ?? 0;

                          return (
                            <tr key={item.id} className="hover:bg-slate-800/30">
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
                                  <ScoreGauge score={item.score ?? 50} size="sm" />
                                  <span className="font-bold text-white">{item.score ?? "—"}</span>
                                </div>
                              </td>
                              <td className="py-3 px-3">
                                <SignalBadge type={(item.signalType || "Watch") as any} size="sm" />
                              </td>
                              <td className="py-3 px-3 font-bold text-slate-200">
                                ₺{price.toFixed(2)}
                              </td>
                              <td className="py-3 px-3 font-bold">
                                <span className={`inline-flex items-center gap-0.5 ${chg >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                                  {chg >= 0 ? <ArrowUpRight className="w-3 h-3" /> : <ArrowDownRight className="w-3 h-3" />}
                                  %{Math.abs(chg).toFixed(2)}
                                </span>
                              </td>
                              <td className="py-3 px-3 font-sans text-slate-400 max-w-xs truncate">
                                {item.notes || "—"}
                              </td>
                              <td className="py-3 px-4 text-right">
                                <div className="flex items-center justify-end gap-2">
                                  <Link
                                    href={`/stocks/${ticker}`}
                                    className="p-1.5 rounded hover:bg-slate-800 text-slate-400 hover:text-sky-300 transition"
                                    title="İncele"
                                  >
                                    <ExternalLink className="w-3.5 h-3.5" />
                                  </Link>
                                  <button
                                    onClick={() => handleRemoveItem(item.symbolId)}
                                    className="p-1.5 rounded hover:bg-rose-500/20 text-slate-400 hover:text-rose-400 transition"
                                    title="Listeden Çıkar"
                                  >
                                    <Trash2 className="w-3.5 h-3.5" />
                                  </button>
                                </div>
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
          )}
        </div>
      )}

      {/* Create List Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#111722] border border-[#283548] rounded-2xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <h2 className="text-base font-bold text-white flex items-center gap-2">
              <Plus className="w-4 h-4 text-sky-400" />
              Yeni Takip Listesi Oluştur
            </h2>

            <form onSubmit={handleCreateList} className="space-y-4 text-xs">
              <div>
                <label className="text-slate-400 font-semibold block mb-1">Liste Adı</label>
                <input
                  type="text"
                  placeholder="Örn: BIST 30 Favorilerim"
                  value={newListName}
                  onChange={(e) => setNewListName(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white outline-none"
                  required
                />
              </div>

              <div>
                <label className="text-slate-400 font-semibold block mb-1">Açıklama (İsteğe bağlı)</label>
                <textarea
                  rows={2}
                  placeholder="Liste amacı ve takip stratejisi..."
                  value={newListDesc}
                  onChange={(e) => setNewListDesc(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white outline-none resize-none"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 rounded-lg bg-[#1a2332] text-slate-300 hover:text-white"
                >
                  İptal
                </button>
                <button
                  type="submit"
                  disabled={creatingList}
                  className="px-4 py-2 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold transition disabled:opacity-50"
                >
                  {creatingList ? "Oluşturuluyor..." : "Listeyi Oluştur"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Add Item Modal */}
      {showAddModal && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#111722] border border-[#283548] rounded-2xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <h2 className="text-base font-bold text-white flex items-center gap-2">
              <Plus className="w-4 h-4 text-emerald-400" />
              Listeye Hisse Ekle
            </h2>

            <form onSubmit={handleAddItem} className="space-y-4 text-xs">
              <div>
                <label className="text-slate-400 font-semibold block mb-1">Hisse Senedi</label>
                <select
                  value={selectedTicker}
                  onChange={(e) => setSelectedTicker(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white font-mono-num font-bold outline-none"
                >
                  {symbols.map((s) => (
                    <option key={s.ticker} value={s.ticker}>
                      {s.ticker} - {s.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 rounded-lg bg-[#1a2332] text-slate-300 hover:text-white"
                >
                  İptal
                </button>
                <button
                  type="submit"
                  disabled={addingItem}
                  className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold transition disabled:opacity-50"
                >
                  {addingItem ? "Ekleniyor..." : "Listeye Ekle"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
