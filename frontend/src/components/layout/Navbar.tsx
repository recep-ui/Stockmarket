"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { 
  Bell, 
  TrendingUp, 
  Activity, 
  Wallet, 
  ChevronRight,
  LogOut,
  LogIn
} from "lucide-react";
import { AlertsApi, PaperTradingApi, AuthApi, getAuthToken, isMockEnabled, MOCK_NOTIFICATIONS } from "@/lib/api";
import { NotificationDto, PaperPortfolioDto, UserProfileDto } from "@/types";

export const Navbar: React.FC = () => {
  const [timeStr, setTimeStr] = useState<string>("");
  const [showNotifications, setShowNotifications] = useState<boolean>(false);
  const [showUserMenu, setShowUserMenu] = useState<boolean>(false);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [portfolio, setPortfolio] = useState<PaperPortfolioDto | null>(null);
  const [user, setUser] = useState<UserProfileDto | null>(null);
  const [hasToken, setHasToken] = useState<boolean>(false);

  useEffect(() => {
    const update = () => {
      const now = new Date();
      setTimeStr(now.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit", second: "2-digit" }));
    };
    update();
    const interval = setInterval(update, 1000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    let isMounted = true;
    const token = getAuthToken();

    async function loadData() {
      if (token) {
        if (isMounted) setHasToken(true);
        try {
          const profile = await AuthApi.getMe();
          if (isMounted && profile) {
            setUser(profile);
          }
        } catch {
          // Token expired or invalid
        }
      } else {
        if (isMounted) setHasToken(false);
      }

      try {
        const notifs = await AlertsApi.getNotifications();
        if (isMounted && Array.isArray(notifs)) {
          setNotifications(notifs);
        }
      } catch {
        if (isMounted && isMockEnabled()) {
          setNotifications(MOCK_NOTIFICATIONS);
        }
      }

      try {
        const port = await PaperTradingApi.getPortfolio();
        if (isMounted && port) {
          setPortfolio(port);
        }
      } catch {
        // Unauthenticated or backend unavailable
      }
    }

    loadData();
    return () => { isMounted = false; };
  }, []);

  const unreadCount = notifications.filter(n => !n.isRead).length;

  const markAllAsRead = () => {
    setNotifications(notifications.map(n => ({ ...n, isRead: true })));
  };

  const handleLogout = () => {
    AuthApi.logout();
  };

  const portfolioDisplayValue = portfolio ? (portfolio.portfolioValue ?? portfolio.totalValue ?? 0) : 100000;

  return (
    <header className="sticky top-0 z-40 flex items-center justify-between h-14 px-4 bg-[#0b0f17]/95 backdrop-blur border-b border-[#1e293b]">
      {/* Left: Brand & Quant Engine Status */}
      <div className="flex items-center gap-6">
        <Link href="/dashboard" className="flex items-center gap-2 text-white font-bold text-lg tracking-tight">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-sky-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-sky-500/20">
            <TrendingUp className="w-4 h-4 text-white" />
          </div>
          <span className="font-extrabold tracking-wider bg-gradient-to-r from-white via-slate-100 to-sky-300 bg-clip-text text-transparent">
            BIST QUANT
          </span>
          <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-sky-500/20 text-sky-400 rounded border border-sky-500/30">
            PRO
          </span>
        </Link>

        {/* Engine Status Bar */}
        <div className="hidden lg:flex items-center gap-3 px-3 py-1 rounded-full bg-[#111722] border border-[#1e293b] text-xs">
          <div className="flex items-center gap-1.5">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            <span className="text-slate-300 font-semibold">Motor:</span>
            <span className="text-slate-200">Çalışıyor</span>
          </div>
          <span className="text-slate-600">|</span>
          <div className="flex items-center gap-1 text-slate-400">
            <Activity className="w-3 h-3 text-sky-400" />
            <span>Terminal Saati</span>
            <span className="font-mono-num text-slate-300">({timeStr})</span>
          </div>
        </div>
      </div>

      {/* Right Actions: Paper Portfolio Balance, Notifications, Profile */}
      <div className="flex items-center gap-3">
        {/* Paper Balance Quick View */}
        <Link 
          href="/paper-trading"
          className="hidden sm:flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[#111722] hover:bg-[#161e2c] border border-[#1e293b] transition text-xs"
        >
          <Wallet className="w-3.5 h-3.5 text-emerald-400" />
          <div>
            <span className="text-slate-400 text-[10px] block leading-none">Sanal Portföy</span>
            <span className="font-mono-num font-bold text-white leading-tight">
              ₺{portfolioDisplayValue.toLocaleString("tr-TR", { minimumFractionDigits: 2 })}
            </span>
          </div>
          {portfolio && (
            <span className={`font-mono-num font-semibold text-[10px] px-1 py-0.5 rounded ${
              portfolio.totalPnL >= 0 ? "text-emerald-400 bg-emerald-500/15" : "text-rose-400 bg-rose-500/15"
            }`}>
              {portfolio.totalPnL >= 0 ? "+" : ""}{portfolio.totalPnLPercent.toFixed(2)}%
            </span>
          )}
        </Link>

        {/* Notification Bell */}
        <div className="relative">
          <button
            onClick={() => setShowNotifications(!showNotifications)}
            className="relative p-2 rounded-lg bg-[#111722] hover:bg-[#161e2c] border border-[#1e293b] text-slate-300 hover:text-white transition"
            aria-label="Bildirimler"
          >
            <Bell className="w-4 h-4" />
            {unreadCount > 0 && (
              <span className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-rose-500 text-[10px] font-bold text-white flex items-center justify-center animate-pulse">
                {unreadCount}
              </span>
            )}
          </button>

          {/* Notifications Dropdown */}
          {showNotifications && (
            <div className="absolute right-0 mt-2 w-80 sm:w-96 rounded-xl bg-[#111722] border border-[#283548] shadow-2xl p-4 z-50">
              <div className="flex items-center justify-between pb-3 border-b border-[#1e293b]">
                <div className="flex items-center gap-2">
                  <h4 className="text-sm font-bold text-white">Sinyal Bildirimleri</h4>
                  {unreadCount > 0 && (
                    <span className="px-1.5 py-0.5 text-[10px] font-semibold bg-rose-500/20 text-rose-400 rounded">
                      {unreadCount} yeni
                    </span>
                  )}
                </div>
                {unreadCount > 0 && (
                  <button 
                    onClick={markAllAsRead}
                    className="text-[11px] text-sky-400 hover:text-sky-300"
                  >
                    Okundu say
                  </button>
                )}
              </div>

              <div className="divide-y divide-[#1e293b] max-h-80 overflow-y-auto mt-2">
                {notifications.length === 0 ? (
                  <div className="py-6 text-center text-xs text-slate-400">
                    Henüz bildirim bulunmuyor.
                  </div>
                ) : (
                  notifications.map(item => (
                    <div 
                      key={item.id} 
                      className={`py-2.5 px-2 rounded-lg text-xs transition ${item.isRead ? "opacity-75" : "bg-slate-800/40"}`}
                    >
                      <div className="flex items-center justify-between font-bold text-white">
                        <span>{item.title}</span>
                        <span className="text-[10px] text-slate-400 font-normal">
                          {new Date(item.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </span>
                      </div>
                      <p className="text-slate-300 mt-1 leading-relaxed">{item.message}</p>
                    </div>
                  ))
                )}
              </div>

              <div className="pt-3 mt-2 border-t border-[#1e293b] text-center">
                <Link 
                  href="/alerts"
                  onClick={() => setShowNotifications(false)}
                  className="text-xs text-sky-400 hover:text-sky-300 inline-flex items-center gap-1 font-semibold"
                >
                  Tüm Alarmları Yönet <ChevronRight className="w-3 h-3" />
                </Link>
              </div>
            </div>
          )}
        </div>

        {/* User Account Menu */}
        <div className="relative">
          {hasToken || user ? (
            <button
              onClick={() => setShowUserMenu(!showUserMenu)}
              className="flex items-center gap-2 pl-2 border-l border-[#1e293b] hover:opacity-80 transition"
            >
              <div className="w-7 h-7 rounded-full bg-gradient-to-tr from-sky-600 to-indigo-600 flex items-center justify-center font-bold text-white text-xs border border-sky-400/30">
                {user?.firstName ? user.firstName[0].toUpperCase() : "Q"}
              </div>
              <div className="hidden md:block text-left">
                <span className="text-xs font-semibold text-slate-200 block leading-tight">
                  {user ? `${user.firstName} ${user.lastName}` : "Quant Trader"}
                </span>
                <span className="text-[10px] text-emerald-400 font-mono-num leading-none">
                  {user?.role || "Trader"}
                </span>
              </div>
            </button>
          ) : (
            <Link
              href="/login"
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-sky-500/15 hover:bg-sky-500/25 border border-sky-500/30 text-sky-300 text-xs font-semibold transition"
            >
              <LogIn className="w-3.5 h-3.5" />
              <span>Giriş Yap</span>
            </Link>
          )}

          {/* User Dropdown */}
          {showUserMenu && (
            <div className="absolute right-0 mt-2 w-56 rounded-xl bg-[#111722] border border-[#283548] shadow-2xl p-2 z-50">
              <div className="px-3 py-2 border-b border-[#1e293b]">
                <p className="text-xs font-semibold text-white truncate">
                  {user ? `${user.firstName} ${user.lastName}` : "BIST Quant Kullanıcısı"}
                </p>
                <p className="text-[10px] text-slate-400 truncate mt-0.5">
                  {user?.email || "Giriş yapıldı"}
                </p>
              </div>
              <button
                onClick={handleLogout}
                className="w-full mt-1.5 flex items-center gap-2 px-3 py-2 rounded-lg text-xs text-rose-400 hover:bg-rose-500/10 transition"
              >
                <LogOut className="w-3.5 h-3.5" />
                <span>Çıkış Yap</span>
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};
