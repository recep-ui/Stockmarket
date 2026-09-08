"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  Wallet,
  TrendingUp,
  ArrowUpRight,
  ArrowDownRight,
  CheckCircle2,
  DollarSign,
  Plus,
  History,
  AlertCircle,
  Briefcase,
  RefreshCw
} from "lucide-react";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import {
  PaperTradingApi,
  MarketDataApi,
  isMockEnabled,
  MOCK_PORTFOLIO,
  MOCK_POSITIONS,
  MOCK_TRADES,
  MOCK_SYMBOLS
} from "@/lib/api";
import {
  PaperPortfolioDto,
  PaperPositionDto,
  PaperTradeDto,
  SymbolDto
} from "@/types";

export default function PaperTradingPage() {
  const [portfolio, setPortfolio] = useState<PaperPortfolioDto | null>(null);
  const [positions, setPositions] = useState<PaperPositionDto[]>([]);
  const [trades, setTrades] = useState<PaperTradeDto[]>([]);
  const [symbols, setSymbols] = useState<SymbolDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<"POSITIONS" | "TRADES">("POSITIONS");
  const [showOrderModal, setShowOrderModal] = useState(false);
  const [orderSymbol, setOrderSymbol] = useState("THYAO");
  const [orderSide, setOrderSide] = useState<"Buy" | "Sell">("Buy");
  const [orderQty, setOrderQty] = useState(10);
  const [submittingOrder, setSubmittingOrder] = useState(false);
  const [feedbackMsg, setFeedbackMsg] = useState<{ success: boolean; text: string } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const port = await PaperTradingApi.getPortfolio();
      setPortfolio(port);

      const [pos, tr, syms] = await Promise.all([
        PaperTradingApi.getPositions(port.id).catch(() => []),
        PaperTradingApi.getTrades(port.id).catch(() => []),
        MarketDataApi.getSymbols().catch(() => [])
      ]);

      setPositions(pos);
      setTrades(tr);
      if (syms.length > 0) {
        setSymbols(syms);
        setOrderSymbol(syms[0].ticker);
      }
    } catch (err: any) {
      if (isMockEnabled()) {
        setPortfolio(MOCK_PORTFOLIO);
        setPositions(MOCK_POSITIONS);
        setTrades(MOCK_TRADES);
        setSymbols(MOCK_SYMBOLS);
      } else {
        setError(err.message || "Sanal portföy verileri yüklenemedi. Oturum süreniz dolmuş olabilir.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleExecuteOrder = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!portfolio) return;

    setSubmittingOrder(true);
    setFeedbackMsg(null);

    try {
      await PaperTradingApi.executeOrder({
        portfolioId: portfolio.id,
        symbol: orderSymbol,
        side: orderSide,
        quantity: orderQty,
        type: "Market"
      });

      setFeedbackMsg({
        success: true,
        text: `${orderQty} adet ${orderSymbol} ${orderSide === "Buy" ? "Alış" : "Satış"} emri başarıyla gerçekleştirildi.`
      });

      setShowOrderModal(false);
      await loadData();
    } catch (err: any) {
      if (isMockEnabled()) {
        setFeedbackMsg({
          success: true,
          text: `(Demo) ${orderQty} adet ${orderSymbol} emri gerçekleştirildi.`
        });
        setShowOrderModal(false);
      } else {
        setFeedbackMsg({
          success: false,
          text: err.message || "Emir iletilemedi."
        });
      }
    } finally {
      setSubmittingOrder(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-black text-white tracking-tight flex items-center gap-2">
            <Wallet className="w-5 h-5 text-emerald-400" />
            Sanal Portföy & Paper Trading Simülatörü
          </h1>
          <p className="text-xs text-slate-400 mt-0.5">
            Gerçek piyasa fiyatlarıyla risksiz strateji testi yapın, pozisyonlarınızı ve gerçekleşen kâr/zararı takip edin.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowOrderModal(true)}
            disabled={!portfolio}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-lg shadow-emerald-600/30 transition disabled:opacity-50"
          >
            <Plus className="w-4 h-4" />
            <span>Yeni Emir Gir</span>
          </button>
        </div>
      </div>

      {feedbackMsg && (
        <div className={`p-3 rounded-xl border text-xs font-semibold flex items-center gap-2 ${
          feedbackMsg.success
            ? "bg-emerald-500/10 border-emerald-500/30 text-emerald-300"
            : "bg-rose-500/10 border-rose-500/30 text-rose-300"
        }`}>
          {feedbackMsg.success ? <CheckCircle2 className="w-4 h-4 shrink-0" /> : <AlertCircle className="w-4 h-4 shrink-0" />}
          <span>{feedbackMsg.text}</span>
        </div>
      )}

      {error && (
        <ErrorBanner
          title="Portföy Verisi Alınamadı"
          message={error}
          onRetry={loadData}
        />
      )}

      {loading && !portfolio && (
        <LoadingSkeleton rows={5} text="Portföy ve açık pozisyonlar yükleniyor..." />
      )}

      {portfolio && (
        <>
          {/* Key Metric Cards */}
          <div className="grid grid-cols-2 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Total Value */}
            <div className="terminal-card p-4 border-l-4 border-l-sky-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Toplam Portföy Büyüklüğü
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-2xl font-black font-mono-num text-white">
                  ₺{(portfolio.portfolioValue ?? portfolio.totalValue ?? 0).toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
                </span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                Başlangıç: ₺{portfolio.initialBalance.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
              </span>
            </div>

            {/* Cash Balance */}
            <div className="terminal-card p-4 border-l-4 border-l-emerald-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Kullanılabilir Nakit Bakiye
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-2xl font-black font-mono-num text-emerald-400">
                  ₺{portfolio.cashBalance.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
                </span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                Nakit Oranı: %{(((portfolio.cashBalance) / ((portfolio.portfolioValue ?? portfolio.totalValue) || 1)) * 100).toFixed(1)}
              </span>
            </div>

            {/* Total PnL */}
            <div className={`terminal-card p-4 border-l-4 ${portfolio.totalPnL >= 0 ? "border-l-emerald-500" : "border-l-rose-500"}`}>
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Toplam Net Kâr / Zarar
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className={`text-2xl font-black font-mono-num ${portfolio.totalPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                  ₺{portfolio.totalPnL.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
                </span>
              </div>
              <span className={`text-[11px] font-mono-num font-semibold block mt-0.5 ${portfolio.totalPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                {portfolio.totalPnL >= 0 ? "+" : ""}{portfolio.totalPnLPercent.toFixed(2)}%
              </span>
            </div>

            {/* Position Count */}
            <div className="terminal-card p-4 border-l-4 border-l-purple-500">
              <span className="text-[10px] text-slate-400 font-bold uppercase block">
                Açık Pozisyon Sayısı
              </span>
              <div className="mt-1 flex items-baseline gap-1">
                <span className="text-2xl font-black font-mono-num text-white">
                  {positions.length}
                </span>
                <span className="text-xs text-slate-400">hisse</span>
              </div>
              <span className="text-[11px] text-slate-400 block mt-0.5">
                Gerçekleşen İşlem: {trades.length}
              </span>
            </div>
          </div>

          {/* Tab Selection */}
          <div className="flex items-center gap-2 border-b border-[#1e293b] pb-2">
            <button
              onClick={() => setActiveTab("POSITIONS")}
              className={`flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg transition ${
                activeTab === "POSITIONS"
                  ? "bg-sky-600 text-white shadow-lg shadow-sky-600/30"
                  : "text-slate-400 hover:text-white"
              }`}
            >
              <Briefcase className="w-3.5 h-3.5" />
              <span>Açık Pozisyonlar ({positions.length})</span>
            </button>
            <button
              onClick={() => setActiveTab("TRADES")}
              className={`flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg transition ${
                activeTab === "TRADES"
                  ? "bg-sky-600 text-white shadow-lg shadow-sky-600/30"
                  : "text-slate-400 hover:text-white"
              }`}
            >
              <History className="w-3.5 h-3.5" />
              <span>İşlem Geçmişi ({trades.length})</span>
            </button>
          </div>

          {/* Table View */}
          {activeTab === "POSITIONS" ? (
            positions.length === 0 ? (
              <EmptyState
                title="Açık Pozisyon Yok"
                description="Portföyünüzde henüz hisse senedi pozisyonu bulunmuyor."
                actionText="Hemen Alış Emri Gir"
                onAction={() => setShowOrderModal(true)}
              />
            ) : (
              <div className="terminal-card overflow-hidden">
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-[#0e1420] text-slate-400 uppercase font-semibold border-b border-[#1e293b]">
                      <tr>
                        <th className="py-3 px-4">Hisse</th>
                        <th className="py-3 px-3">Adet (Lot)</th>
                        <th className="py-3 px-3">Ortalama Maliyet</th>
                        <th className="py-3 px-3">Güncel Fiyat</th>
                        <th className="py-3 px-3">Toplam Maliyet</th>
                        <th className="py-3 px-3">Piyasa Değeri</th>
                        <th className="py-3 px-3">Kâr / Zarar (₺)</th>
                        <th className="py-3 px-4 text-right">Getiri (%)</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#1e293b] font-mono-num">
                      {positions.map((pos) => (
                        <tr key={pos.id} className="hover:bg-slate-800/30">
                          <td className="py-3 px-4 font-bold text-white text-sm">{pos.symbol}</td>
                          <td className="py-3 px-3 text-slate-300 font-semibold">{pos.quantity}</td>
                          <td className="py-3 px-3 text-slate-300">₺{pos.averagePrice.toFixed(2)}</td>
                          <td className="py-3 px-3 text-white font-bold">₺{pos.currentPrice.toFixed(2)}</td>
                          <td className="py-3 px-3 text-slate-300">₺{pos.totalCost.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}</td>
                          <td className="py-3 px-3 font-bold text-white">₺{pos.currentValue.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}</td>
                          <td className={`py-3 px-3 font-bold ${pos.unrealizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                            ₺{pos.unrealizedPnL.toFixed(2)}
                          </td>
                          <td className={`py-3 px-4 text-right font-bold ${pos.unrealizedPnLPercent >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                            {pos.unrealizedPnLPercent >= 0 ? "+" : ""}{pos.unrealizedPnLPercent.toFixed(2)}%
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )
          ) : (
            trades.length === 0 ? (
              <EmptyState
                title="İşlem Geçmişi Boş"
                description="Henüz gerçekleştirilmiş bir alım veya satım emri bulunmuyor."
              />
            ) : (
              <div className="terminal-card overflow-hidden">
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-[#0e1420] text-slate-400 uppercase font-semibold border-b border-[#1e293b]">
                      <tr>
                        <th className="py-3 px-4">Tarih</th>
                        <th className="py-3 px-3">Hisse</th>
                        <th className="py-3 px-3">Yön</th>
                        <th className="py-3 px-3">Adet</th>
                        <th className="py-3 px-3">İşlem Fiyatı</th>
                        <th className="py-3 px-3">Toplam Tutar</th>
                        <th className="py-3 px-3">Komisyon</th>
                        <th className="py-3 px-4 text-right">Realize K/Z</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#1e293b] font-mono-num">
                      {trades.map((tr) => (
                        <tr key={tr.id} className="hover:bg-slate-800/30">
                          <td className="py-3 px-4 text-slate-400">{new Date(tr.executedAt).toLocaleString("tr-TR")}</td>
                          <td className="py-3 px-3 font-bold text-white">{tr.symbol}</td>
                          <td className="py-3 px-3">
                            <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                              tr.side === "Buy" ? "bg-emerald-500/20 text-emerald-400 border border-emerald-500/30" : "bg-rose-500/20 text-rose-400 border border-rose-500/30"
                            }`}>
                              {tr.side === "Buy" ? "ALIŞ" : "SATIŞ"}
                            </span>
                          </td>
                          <td className="py-3 px-3 text-slate-300">{tr.quantity}</td>
                          <td className="py-3 px-3 text-slate-300">₺{tr.price.toFixed(2)}</td>
                          <td className="py-3 px-3 font-bold text-white">₺{tr.totalValue.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}</td>
                          <td className="py-3 px-3 text-slate-400">₺{tr.commission.toFixed(2)}</td>
                          <td className={`py-3 px-4 text-right font-bold ${tr.realizedPnL >= 0 ? "text-emerald-400" : "text-rose-400"}`}>
                            ₺{tr.realizedPnL.toFixed(2)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )
          )}
        </>
      )}

      {/* Order Modal */}
      {showOrderModal && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#111722] border border-[#283548] rounded-2xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <h2 className="text-base font-bold text-white flex items-center gap-2">
              <Wallet className="w-4 h-4 text-emerald-400" />
              Sanal BIST Emri Ver
            </h2>

            <form onSubmit={handleExecuteOrder} className="space-y-4 text-xs">
              <div>
                <label className="text-slate-400 font-semibold block mb-1">
                  Hisse Seçimi
                </label>
                <select
                  value={orderSymbol}
                  onChange={(e) => setOrderSymbol(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white font-mono-num font-bold outline-none"
                >
                  {symbols.map((s) => (
                    <option key={s.ticker} value={s.ticker}>
                      {s.ticker} - {s.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="text-slate-400 font-semibold block mb-1">
                  İşlem Yönü
                </label>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setOrderSide("Buy")}
                    className={`py-2 rounded-lg font-bold transition ${
                      orderSide === "Buy" ? "bg-emerald-600 text-white" : "bg-[#0d121c] text-slate-400"
                    }`}
                  >
                    ALIŞ (BUY)
                  </button>
                  <button
                    type="button"
                    onClick={() => setOrderSide("Sell")}
                    className={`py-2 rounded-lg font-bold transition ${
                      orderSide === "Sell" ? "bg-rose-600 text-white" : "bg-[#0d121c] text-slate-400"
                    }`}
                  >
                    SATIŞ (SELL)
                  </button>
                </div>
              </div>

              <div>
                <label className="text-slate-400 font-semibold block mb-1">
                  Adet (Lot)
                </label>
                <input
                  type="number"
                  min={1}
                  max={10000}
                  value={orderQty}
                  onChange={(e) => setOrderQty(Math.max(1, Number(e.target.value)))}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white font-mono-num font-bold outline-none"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setShowOrderModal(false)}
                  className="px-4 py-2 rounded-lg bg-[#1a2332] text-slate-300 hover:text-white"
                >
                  Vazgeç
                </button>
                <button
                  type="submit"
                  disabled={submittingOrder}
                  className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold transition disabled:opacity-50"
                >
                  {submittingOrder ? "İşleniyor..." : "Emri Onayla"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
