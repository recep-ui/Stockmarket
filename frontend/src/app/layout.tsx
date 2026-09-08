import type { Metadata } from "next";
import { Navbar } from "@/components/layout/Navbar";
import { Sidebar } from "@/components/layout/Sidebar";
import "./globals.css";

export const metadata: Metadata = {
  title: "BIST Quant Scanner | Algoritmik Hisse Analiz ve Sinyal Platformu",
  description: "Borsa İstanbul için deterministik teknik analiz, kantitatif puanlama, backtest ve sinyal takip terminali.",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="tr" className="dark h-full bg-[#080b11] text-slate-100 antialiased">
      <body className="min-h-full flex flex-col bg-[#080b11] text-slate-100 selection:bg-sky-500/30 selection:text-sky-200">
        <Navbar />
        <div className="flex flex-1">
          <Sidebar />
          <main className="flex-1 p-4 md:p-6 overflow-y-auto max-w-[1700px] w-full mx-auto">
            {children}
          </main>
        </div>
      </body>
    </html>
  );
}
