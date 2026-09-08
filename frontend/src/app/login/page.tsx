"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { TrendingUp, Lock, Mail, ArrowRight, ShieldCheck, AlertCircle } from "lucide-react";
import { AuthApi } from "@/lib/api";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isDemoEnabled = process.env.NEXT_PUBLIC_DEMO_MODE === "true";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      await AuthApi.login({ email, password });
      router.push("/dashboard");
    } catch (err: any) {
      setError(err.message || "Giriş başarısız. Lütfen bilgilerinizi kontrol edin.");
    } finally {
      setLoading(false);
    }
  };

  const handleDemoLogin = async () => {
    setEmail("demo@bistquant.com");
    setPassword("Demo1234!");
    setError(null);
    setLoading(true);

    try {
      await AuthApi.login({ email: "demo@bistquant.com", password: "Demo1234!" });
      router.push("/dashboard");
    } catch (err: any) {
      setError(err.message || "Demo girişi başarısız.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col justify-center items-center px-4 bg-[#080c14] text-slate-100">
      <div className="w-full max-w-md">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-gradient-to-br from-sky-500 to-indigo-600 shadow-xl shadow-sky-500/20 mb-4">
            <TrendingUp className="w-7 h-7 text-white" />
          </div>
          <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight bg-gradient-to-r from-white via-slate-100 to-sky-300 bg-clip-text text-transparent">
            BIST QUANT SCANNER
          </h1>
          <p className="text-sm text-slate-400 mt-1">
            Kurumsal Algoritmik Hisse Analiz ve İşlem Platformu
          </p>
        </div>

        {/* Login Card */}
        <div className="p-6 sm:p-8 rounded-2xl bg-[#0e1422] border border-[#1e293b] shadow-2xl backdrop-blur">
          <div className="mb-6">
            <h2 className="text-xl font-bold text-white">Giriş Yap</h2>
            <p className="text-xs text-slate-400 mt-0.5">Terminal erişimi için hesabınıza giriş yapın</p>
          </div>

          {error && (
            <div className="mb-5 p-3.5 rounded-xl bg-rose-500/10 border border-rose-500/30 flex items-start gap-3 text-rose-300 text-xs">
              <AlertCircle className="w-4 h-4 text-rose-400 shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-1.5">
                E-posta Adresi
              </label>
              <div className="relative">
                <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="adiniz@sirket.com"
                  className="w-full pl-10 pr-4 py-2.5 bg-[#141b2d] border border-[#223048] rounded-xl text-sm text-white placeholder-slate-500 focus:outline-none focus:border-sky-500 focus:ring-1 focus:ring-sky-500 transition"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-1.5">
                Şifre
              </label>
              <div className="relative">
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-10 pr-4 py-2.5 bg-[#141b2d] border border-[#223048] rounded-xl text-sm text-white placeholder-slate-500 focus:outline-none focus:border-sky-500 focus:ring-1 focus:ring-sky-500 transition"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full mt-2 py-3 px-4 rounded-xl bg-gradient-to-r from-sky-500 to-indigo-600 hover:from-sky-400 hover:to-indigo-500 text-white text-sm font-bold shadow-lg shadow-sky-500/25 flex items-center justify-center gap-2 transition disabled:opacity-50"
            >
              {loading ? (
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  <span>Giriş Yap</span>
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          {isDemoEnabled && (
            <div className="mt-5 pt-4 border-t border-[#1e293b]">
              <button
                type="button"
                onClick={handleDemoLogin}
                disabled={loading}
                className="w-full py-2.5 px-4 rounded-xl bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-amber-300 text-xs font-semibold flex items-center justify-center gap-2 transition"
              >
                <ShieldCheck className="w-4 h-4 text-amber-400" />
                <span>Geliştirici Demo Girişi (demo@bistquant.com)</span>
              </button>
            </div>
          )}

          <div className="mt-6 text-center">
            <span className="text-xs text-slate-400">Hesabınız yok mu? </span>
            <Link href="/register" className="text-xs font-semibold text-sky-400 hover:text-sky-300 transition">
              Kayıt Ol
            </Link>
          </div>
        </div>

        <div className="mt-6 text-center text-[11px] text-slate-500">
          BIST Quant Engine v2.0 • Güvenli JWT Kimlik Doğrulama
        </div>
      </div>
    </div>
  );
}
