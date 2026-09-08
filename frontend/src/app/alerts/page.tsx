"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  Bell,
  Plus,
  Send,
  Trash2,
  CheckCircle2,
  AlertCircle,
  MessageSquare,
  Sparkles,
  ShieldCheck,
  RefreshCw
} from "lucide-react";
import { LoadingSkeleton, ErrorBanner, EmptyState } from "@/components/ui/StateFeedback";
import {
  AlertsApi,
  MarketDataApi,
  isMockEnabled,
  MOCK_ALERTS,
  MOCK_NOTIFICATIONS,
  MOCK_SYMBOLS
} from "@/lib/api";
import { AlertSubscriptionDto, NotificationDto, SymbolDto, NotificationChannel } from "@/types";

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<AlertSubscriptionDto[]>([]);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [symbols, setSymbols] = useState<SymbolDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [selectedSymbol, setSelectedSymbol] = useState("ALL");
  const [alertType, setAlertType] = useState("ScoreThreshold");
  const [thresholdValue, setThresholdValue] = useState(85);
  const [channel, setChannel] = useState("Telegram");
  const [feedback, setFeedback] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [subs, notifs, syms] = await Promise.all([
        AlertsApi.getSubscriptions(),
        AlertsApi.getNotifications(),
        MarketDataApi.getSymbols().catch(() => [])
      ]);

      setAlerts(subs);
      setNotifications(notifs);
      setSymbols(syms);
    } catch (err: any) {
      if (isMockEnabled()) {
        setAlerts(MOCK_ALERTS);
        setNotifications(MOCK_NOTIFICATIONS);
        setSymbols(MOCK_SYMBOLS);
      } else {
        setError(err.message || "Alarm ve bildirim verileri yüklenemedi. Lütfen oturumunuzu kontrol edin.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleCreateAlert = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setFeedback(null);

    const symbolTicker = selectedSymbol === "ALL" ? null : selectedSymbol;

    try {
      const created = await AlertsApi.createSubscription({
        symbol: symbolTicker,
        alertType,
        condition: ">=",
        value: thresholdValue,
        channel
      });

      setAlerts((prev) => [created, ...prev]);
      setFeedback(`Alarm Başarıyla Kuruldu: ${selectedSymbol === "ALL" ? "Tüm Hisseler" : selectedSymbol} için ${alertType} (${channel})`);
      setTimeout(() => setFeedback(null), 4000);
    } catch (err: any) {
      if (isMockEnabled()) {
        const mockNew: AlertSubscriptionDto = {
          id: alerts.length + 1,
          userId: 1,
          symbol: selectedSymbol === "ALL" ? "TÜMÜ (Genel)" : selectedSymbol,
          alertType,
          condition: ">=",
          value: thresholdValue,
          channel: channel as NotificationChannel,
          isActive: true,
          lastTriggeredAt: null
        };
        setAlerts((prev) => [mockNew, ...prev]);
        setFeedback(`(Demo) Alarm Kuruldu: ${mockNew.symbol}`);
        setTimeout(() => setFeedback(null), 4000);
      } else {
        setError(err.message || "Alarm aboneliği oluşturulamadı.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await AlertsApi.deleteSubscription(id);
      setAlerts((prev) => prev.filter((a) => a.id !== id));
    } catch (err: any) {
      if (isMockEnabled()) {
        setAlerts((prev) => prev.filter((a) => a.id !== id));
      } else {
        setError(err.message || "Alarm silinemedi.");
      }
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-xl font-black text-white tracking-tight flex items-center gap-2">
          <Bell className="w-5 h-5 text-sky-400" />
          Multi-Channel Alarm & Telegram Bildirim Motoru
        </h1>
        <p className="text-xs text-slate-400 mt-0.5">
          Yüksek puanlı sinyalleri, direnç kırılımlarını ve stop seviyelerini anlık olarak Telegram ve uygulama içi bildirimle alın.
        </p>
      </div>

      {feedback && (
        <div className="p-3 bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 rounded-lg text-xs font-semibold flex items-center gap-2">
          <CheckCircle2 className="w-4 h-4" /> {feedback}
        </div>
      )}

      {error && (
        <ErrorBanner
          title="Alarm Servisi Hatası"
          message={error}
          onRetry={loadData}
        />
      )}

      {loading && alerts.length === 0 && (
        <LoadingSkeleton rows={4} text="Aktif alarmlar ve bildirimler yükleniyor..." />
      )}

      {/* Grid: Create Alert + Telegram Setup Card */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: 2 Cols - Create Alert Subscription Form */}
        <div className="lg:col-span-2 terminal-card p-5 bg-[#111722]">
          <div className="flex items-center gap-2 pb-3 border-b border-[#1e293b]">
            <Plus className="w-4 h-4 text-sky-400" />
            <h2 className="text-sm font-bold text-white">Yeni Alarm Tanımla</h2>
          </div>

          <form onSubmit={handleCreateAlert} className="mt-4 space-y-4 text-xs">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {/* Symbol */}
              <div>
                <label className="text-slate-300 font-semibold block mb-1">Hisse Senedi</label>
                <select
                  value={selectedSymbol}
                  onChange={(e) => setSelectedSymbol(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white outline-none"
                >
                  <option value="ALL">Tüm Hisseler (Genel Tarama)</option>
                  {symbols.map((s) => (
                    <option key={s.ticker} value={s.ticker}>
                      {s.ticker} - {s.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Alert Type */}
              <div>
                <label className="text-slate-300 font-semibold block mb-1">Tetikleyici Koşul</label>
                <select
                  value={alertType}
                  onChange={(e) => setAlertType(e.target.value)}
                  className="w-full bg-[#0d121c] border border-[#1e293b] focus:border-sky-500 rounded-lg px-3 py-2 text-white outline-none"
                >
                  <option value="ScoreThreshold">Quant Skoru Eşiği</option>
                  <option value="StrongBuy">Güçlü AL Sinyali</option>
                  <option value="PriceBreakout">20 Günlük Zirve Kırılımı</option>
                  <option value="RsiExtreme">RSI Aşırı Satım Dip Sinyali</option>
                </select>
              </div>

              {/* Value / Threshold */}
              {alertType === "ScoreThreshold" && (
                <div>
                  <div className="flex items-center justify-between mb-1">
                    <label className="text-slate-300 font-semibold">Hedef Skor Eşiği</label>
                    <span className="font-mono-num font-bold text-sky-400">{thresholdValue} / 100</span>
                  </div>
                  <input
                    type="range"
                    min={60}
                    max={95}
                    step={5}
                    value={thresholdValue}
                    onChange={(e) => setThresholdValue(Number(e.target.value))}
                    className="w-full accent-sky-500 cursor-pointer"
                  />
                </div>
              )}

              {/* Channel */}
              <div>
                <label className="text-slate-300 font-semibold block mb-1">İletim Kanalı</label>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setChannel("Telegram")}
                    className={`py-2 rounded-lg font-bold transition ${
                      channel === "Telegram" ? "bg-sky-600 text-white" : "bg-[#0d121c] text-slate-400"
                    }`}
                  >
                    Telegram Bot
                  </button>
                  <button
                    type="button"
                    onClick={() => setChannel("InApp")}
                    className={`py-2 rounded-lg font-bold transition ${
                      channel === "InApp" ? "bg-sky-600 text-white" : "bg-[#0d121c] text-slate-400"
                    }`}
                  >
                    Terminal İçi (In-App)
                  </button>
                </div>
              </div>
            </div>

            <div className="pt-2 flex justify-end">
              <button
                type="submit"
                disabled={submitting}
                className="px-5 py-2.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-bold text-xs shadow-lg shadow-sky-600/30 transition disabled:opacity-50"
              >
                {submitting ? "Kuruluyor..." : "Alarmı Aktifleştir"}
              </button>
            </div>
          </form>
        </div>

        {/* Right: Channel Status Info */}
        <div className="terminal-card p-5 bg-[#111722] space-y-4">
          <div className="flex items-center gap-2 pb-3 border-b border-[#1e293b]">
            <MessageSquare className="w-4 h-4 text-sky-400" />
            <h3 className="text-sm font-bold text-white">İletim Kanalları Durumu</h3>
          </div>

          <div className="p-3 rounded-lg bg-[#0d121c] border border-slate-800 space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="text-slate-300 font-semibold">Telegram Entegrasyonu</span>
              <span className="text-emerald-400 font-bold flex items-center gap-1">
                <ShieldCheck className="w-3.5 h-3.5" /> Aktif
              </span>
            </div>
            <p className="text-[11px] text-slate-400 leading-relaxed">
              Bot token ve varsayılan Chat ID sunucu ortam değişkenleri üzerinden yönetilir.
            </p>
          </div>

          <div className="p-3 rounded-lg bg-[#0d121c] border border-slate-800 space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="text-slate-300 font-semibold">Uygulama İçi Bildirimler</span>
              <span className="text-emerald-400 font-bold flex items-center gap-1">
                <ShieldCheck className="w-3.5 h-3.5" /> Aktif
              </span>
            </div>
            <p className="text-[11px] text-slate-400 leading-relaxed">
              Üretilen sinyaller anlık olarak bildirim çubuğunda ve bu sayfada listelenir.
            </p>
          </div>
        </div>
      </div>

      {/* Active Subscriptions List */}
      <div className="terminal-card p-5">
        <h2 className="text-sm font-bold text-white mb-3">
          Aktif Alarm Abonelikleri ({alerts.length})
        </h2>

        {alerts.length === 0 ? (
          <EmptyState
            title="Aktif Alarm Bulunmuyor"
            description="Henüz oluşturulmuş bir sinyal veya fiyat alarmınız yok."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-[#0e1420] text-slate-400 uppercase font-semibold border-b border-[#1e293b]">
                <tr>
                  <th className="py-2.5 px-3">Hisse</th>
                  <th className="py-2.5 px-3">Koşul</th>
                  <th className="py-2.5 px-3">Değer</th>
                  <th className="py-2.5 px-3">Kanal</th>
                  <th className="py-2.5 px-3">Durum</th>
                  <th className="py-2.5 px-3">Son Tetiklenme</th>
                  <th className="py-2.5 px-3 text-right">İşlem</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#1e293b] font-mono-num">
                {alerts.map((al) => (
                  <tr key={al.id} className="hover:bg-slate-800/30">
                    <td className="py-2.5 px-3 font-bold text-white font-sans">{al.symbol || "TÜMÜ (Genel)"}</td>
                    <td className="py-2.5 px-3 font-sans text-slate-300">{al.alertType}</td>
                    <td className="py-2.5 px-3 font-bold text-sky-400">{al.condition} {al.value}</td>
                    <td className="py-2.5 px-3 font-sans">
                      <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-semibold text-[11px]">
                        {al.channel}
                      </span>
                    </td>
                    <td className="py-2.5 px-3 font-sans">
                      <span className="text-emerald-400 font-bold flex items-center gap-1">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Aktif
                      </span>
                    </td>
                    <td className="py-2.5 px-3 text-slate-400">
                      {al.lastTriggeredAt ? new Date(al.lastTriggeredAt).toLocaleString("tr-TR") : "Henüz tetiklenmedi"}
                    </td>
                    <td className="py-2.5 px-3 text-right">
                      <button
                        onClick={() => handleDelete(al.id)}
                        className="p-1 rounded hover:bg-rose-500/20 text-slate-400 hover:text-rose-400 transition"
                        title="Alarmı Sil"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Triggered Notifications Log */}
      <div className="terminal-card p-5">
        <h2 className="text-sm font-bold text-white mb-3">
          Son Tetiklenen Sinyal Bildirimleri ({notifications.length})
        </h2>

        {notifications.length === 0 ? (
          <p className="text-xs text-slate-400 py-4 text-center">Henüz iletilen bildirim kaydı yok.</p>
        ) : (
          <div className="divide-y divide-[#1e293b]">
            {notifications.map((notif) => (
              <div key={notif.id} className="py-3 flex items-start justify-between gap-4 text-xs">
                <div>
                  <h4 className="font-bold text-white">{notif.title}</h4>
                  <p className="text-slate-300 mt-1 leading-relaxed">{notif.message}</p>
                </div>
                <span className="text-[11px] font-mono-num text-slate-400 shrink-0">
                  {new Date(notif.createdAt).toLocaleString("tr-TR")}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
