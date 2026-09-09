"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Filter,
  BarChart3,
  Lightbulb,
  FlaskConical,
  Wallet,
  Bell,
  Bookmark,
  Code2,
  ChevronRight,
  TrendingUp,
  Database
} from "lucide-react";

interface NavItem {
  name: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
}

const navItems: NavItem[] = [
  { name: "Genel Bakış", href: "/dashboard", icon: LayoutDashboard },
  { name: "BIST Tarama", href: "/scanner", icon: Filter },
  { name: "İleri Test (Forward)", href: "/forward-testing", icon: TrendingUp, badge: "LIVE" },
  { name: "Sanal Portföy", href: "/paper-trading", icon: Wallet, badge: "₺" },
  { name: "Takip Listeleri", href: "/watchlists", icon: Bookmark },
  { name: "Hisse Analizi", href: "/stocks/THYAO", icon: BarChart3 },
  { name: "Strateji Lab", href: "/strategies", icon: Lightbulb },
  { name: "Backtest", href: "/backtests", icon: FlaskConical },
  { name: "Alarmlar", href: "/alerts", icon: Bell },
];

const adminItems: NavItem[] = [
  { name: "Veri İndirme & Gap", href: "/admin/backfill", icon: Database },
];

export const Sidebar: React.FC = () => {
  const pathname = usePathname();

  return (
    <aside className="w-56 shrink-0 bg-[#0b0f17] border-r border-[#1e293b] flex flex-col justify-between p-3 min-h-[calc(100vh-3.5rem)]">
      <div className="space-y-6">
        <div>
          <span className="px-3 text-[10px] font-bold uppercase tracking-wider text-slate-500">
            Analiz & İşlem
          </span>
          <nav className="mt-2 space-y-1">
            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = pathname === item.href || (item.href.startsWith("/stocks") && pathname.startsWith("/stocks"));

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-semibold transition ${
                    isActive
                      ? "bg-sky-500/15 text-sky-400 border border-sky-500/30 shadow-[0_0_12px_rgba(14,165,233,0.15)]"
                      : "text-slate-400 hover:text-white hover:bg-[#131923]"
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <Icon className={`w-4 h-4 ${isActive ? "text-sky-400" : "text-slate-400"}`} />
                    <span>{item.name}</span>
                  </div>

                  {item.badge && (
                    <span className={`text-[9px] px-1.5 py-0.5 rounded font-bold uppercase ${
                      item.badge === "LIVE"
                        ? "bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 animate-pulse"
                        : "bg-purple-500/20 text-purple-400 border border-purple-500/30"
                    }`}>
                      {item.badge}
                    </span>
                  )}
                </Link>
              );
            })}
          </nav>
        </div>

        <div>
          <span className="px-3 text-[10px] font-bold uppercase tracking-wider text-slate-500">
            Sistem & Veri
          </span>
          <nav className="mt-2 space-y-1">
            {adminItems.map((item) => {
              const Icon = item.icon;
              const isActive = pathname === item.href;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-semibold transition ${
                    isActive
                      ? "bg-sky-500/15 text-sky-400 border border-sky-500/30 shadow-[0_0_12px_rgba(14,165,233,0.15)]"
                      : "text-slate-400 hover:text-white hover:bg-[#131923]"
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <Icon className={`w-4 h-4 ${isActive ? "text-sky-400" : "text-slate-400"}`} />
                    <span>{item.name}</span>
                  </div>
                </Link>
              );
            })}
          </nav>
        </div>

        {/* Quick Stock Switcher in Sidebar */}
        <div className="pt-2 border-t border-[#1e293b]/60">
          <div className="flex items-center justify-between px-3 mb-1">
            <span className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
              Gözde Hisseler
            </span>
            <span className="text-[9px] text-emerald-400 font-mono-num font-bold">BIST 30</span>
          </div>
          <div className="grid grid-cols-2 gap-1 px-1">
            {["THYAO", "ASELS", "TUPRS", "FROTO", "GARAN", "KCHOL"].map((sym) => (
              <Link
                key={sym}
                href={`/stocks/${sym}`}
                className="px-2 py-1 text-center font-mono-num font-bold text-[11px] rounded bg-[#111722] hover:bg-[#18202d] text-slate-300 hover:text-white border border-[#1e293b] transition"
              >
                {sym}
              </Link>
            ))}
          </div>
        </div>
      </div>

      {/* Footer Info: Backend API Status & Swagger */}
      <div className="pt-3 border-t border-[#1e293b] space-y-2">
        <a
          href="/swagger"
          target="_blank"
          rel="noreferrer"
          className="flex items-center justify-between px-2.5 py-2 rounded-lg bg-[#111722] hover:bg-[#161e2c] border border-[#1e293b] text-slate-400 hover:text-sky-300 text-xs transition"
        >
          <div className="flex items-center gap-2">
            <Code2 className="w-3.5 h-3.5 text-sky-400" />
            <span className="text-[11px] font-medium">REST API / Swagger</span>
          </div>
          <ChevronRight className="w-3 h-3 text-slate-500" />
        </a>

        <div className="px-2 py-1.5 rounded bg-slate-900/60 border border-slate-800/80 text-[10px] text-slate-500 flex items-center justify-between">
          <span>Engine v1.0</span>
          <span className="flex items-center gap-1 text-emerald-400 font-semibold">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" /> .NET 10 API
          </span>
        </div>
      </div>
    </aside>
  );
};
