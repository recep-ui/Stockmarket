"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  createChart,
  IChartApi,
  CandlestickSeries,
  HistogramSeries,
  LineSeries,
  LineStyle,
  CandlestickData,
  LineData,
  HistogramData,
  Time
} from "lightweight-charts";
import { CandleBarDto } from "@/types";

interface StockChartProps {
  symbol: string;
  data: CandleBarDto[];
  stopLoss?: number;
  takeProfit1?: number;
  takeProfit2?: number;
}

export const StockChart: React.FC<StockChartProps> = ({
  symbol,
  data,
  stopLoss,
  takeProfit1,
  takeProfit2,
}) => {
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartInstanceRef = useRef<IChartApi | null>(null);

  const [activeOverlays, setActiveOverlays] = useState({
    ema20: true,
    ema50: true,
    ema200: false,
    levels: true,
  });

  const [selectedSubIndicator, setSelectedSubIndicator] = useState<"RSI" | "MACD">("RSI");

  useEffect(() => {
    if (!chartContainerRef.current || data.length === 0) return;

    // Clean up previous instance
    if (chartInstanceRef.current) {
      chartInstanceRef.current.remove();
    }

    const container = chartContainerRef.current;

    // 1. Create Main Candlestick Chart
    const chart = createChart(container, {
      width: container.clientWidth,
      height: 420,
      layout: {
        background: { color: "#0d121c" },
        textColor: "#94a3b8",
        fontSize: 11,
      },
      grid: {
        vertLines: { color: "#161e2c" },
        horzLines: { color: "#161e2c" },
      },
      crosshair: {
        mode: 1,
        vertLine: { color: "#38bdf8", width: 1, style: 3 },
        horzLine: { color: "#38bdf8", width: 1, style: 3 },
      },
      timeScale: {
        borderColor: "#1e293b",
        timeVisible: true,
      },
      rightPriceScale: {
        borderColor: "#1e293b",
        scaleMargins: {
          top: 0.1,
          bottom: 0.25,
        },
      },
    });

    chartInstanceRef.current = chart;

    // Candlestick series
    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: "#10b981",
      downColor: "#ef4444",
      borderVisible: false,
      wickUpColor: "#10b981",
      wickDownColor: "#ef4444",
    });

    const candleData: CandlestickData<Time>[] = data.map((d) => ({
      time: d.time as Time,
      open: d.open,
      high: d.high,
      low: d.low,
      close: d.close,
    }));
    candleSeries.setData(candleData);

    // Volume Series
    const volumeSeries = chart.addSeries(HistogramSeries, {
      color: "#22c55e",
      priceFormat: {
        type: "volume",
      },
      priceScaleId: "", // Overlay over main
    });

    volumeSeries.priceScale().applyOptions({
      scaleMargins: {
        top: 0.8,
        bottom: 0,
      },
    });

    const volumeData: HistogramData<Time>[] = data.map((d) => ({
      time: d.time as Time,
      value: d.volume,
      color: d.close >= d.open ? "rgba(16, 185, 129, 0.25)" : "rgba(239, 68, 68, 0.25)",
    }));
    volumeSeries.setData(volumeData);

    // EMA 20 (Cyan)
    if (activeOverlays.ema20) {
      const ema20Series = chart.addSeries(LineSeries, {
        color: "#38bdf8",
        lineWidth: 2,
        title: "EMA 20",
      });
      const ema20Data: LineData<Time>[] = data
        .filter((d) => d.ema20 !== undefined)
        .map((d) => ({ time: d.time as Time, value: d.ema20! }));
      ema20Series.setData(ema20Data);
    }

    // EMA 50 (Amber)
    if (activeOverlays.ema50) {
      const ema50Series = chart.addSeries(LineSeries, {
        color: "#fbbf24",
        lineWidth: 2,
        title: "EMA 50",
      });
      const ema50Data: LineData<Time>[] = data
        .filter((d) => d.ema50 !== undefined)
        .map((d) => ({ time: d.time as Time, value: d.ema50! }));
      ema50Series.setData(ema50Data);
    }

    // EMA 200 (Purple)
    if (activeOverlays.ema200) {
      const ema200Series = chart.addSeries(LineSeries, {
        color: "#a855f7",
        lineWidth: 2,
        title: "EMA 200",
      });
      const ema200Data: LineData<Time>[] = data
        .filter((d) => d.ema200 !== undefined)
        .map((d) => ({ time: d.time as Time, value: d.ema200! }));
      ema200Series.setData(ema200Data);
    }

    // Stop Loss & Take Profit price lines
    if (activeOverlays.levels) {
      if (stopLoss) {
        candleSeries.createPriceLine({
          price: stopLoss,
          color: "#ef4444",
          lineWidth: 2,
          lineStyle: LineStyle.Dashed,
          axisLabelVisible: true,
          title: `STOP: ${stopLoss.toFixed(2)}`,
        });
      }
      if (takeProfit1) {
        candleSeries.createPriceLine({
          price: takeProfit1,
          color: "#10b981",
          lineWidth: 2,
          lineStyle: LineStyle.Dashed,
          axisLabelVisible: true,
          title: `TP1: ${takeProfit1.toFixed(2)}`,
        });
      }
      if (takeProfit2) {
        candleSeries.createPriceLine({
          price: takeProfit2,
          color: "#059669",
          lineWidth: 2,
          lineStyle: LineStyle.Dashed,
          axisLabelVisible: true,
          title: `TP2: ${takeProfit2.toFixed(2)}`,
        });
      }
    }

    chart.timeScale().fitContent();

    // Resize handler
    const handleResize = () => {
      if (container && chartInstanceRef.current) {
        chartInstanceRef.current.applyOptions({ width: container.clientWidth });
      }
    };
    window.addEventListener("resize", handleResize);

    return () => {
      window.removeEventListener("resize", handleResize);
      chart.remove();
    };
  }, [data, activeOverlays, stopLoss, takeProfit1, takeProfit2]);

  return (
    <div className="terminal-card overflow-hidden">
      {/* Chart Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-2 p-3 bg-[#111722] border-b border-[#1e293b]">
        <div className="flex items-center gap-3">
          <span className="font-extrabold text-white text-base tracking-wider font-mono-num">
            {symbol}
          </span>
          <span className="text-xs text-slate-400 font-medium">Günlük Mum Grafiği</span>
        </div>

        {/* Overlay Toggles */}
        <div className="flex items-center gap-2 text-xs">
          <button
            onClick={() => setActiveOverlays({ ...activeOverlays, ema20: !activeOverlays.ema20 })}
            className={`px-2 py-1 rounded text-[11px] font-mono-num font-bold transition border ${
              activeOverlays.ema20
                ? "bg-sky-500/20 text-sky-300 border-sky-500/40"
                : "bg-slate-800 text-slate-400 border-slate-700"
            }`}
          >
            EMA 20
          </button>
          <button
            onClick={() => setActiveOverlays({ ...activeOverlays, ema50: !activeOverlays.ema50 })}
            className={`px-2 py-1 rounded text-[11px] font-mono-num font-bold transition border ${
              activeOverlays.ema50
                ? "bg-amber-500/20 text-amber-300 border-amber-500/40"
                : "bg-slate-800 text-slate-400 border-slate-700"
            }`}
          >
            EMA 50
          </button>
          <button
            onClick={() => setActiveOverlays({ ...activeOverlays, ema200: !activeOverlays.ema200 })}
            className={`px-2 py-1 rounded text-[11px] font-mono-num font-bold transition border ${
              activeOverlays.ema200
                ? "bg-purple-500/20 text-purple-300 border-purple-500/40"
                : "bg-slate-800 text-slate-400 border-slate-700"
            }`}
          >
            EMA 200
          </button>
          <button
            onClick={() => setActiveOverlays({ ...activeOverlays, levels: !activeOverlays.levels })}
            className={`px-2 py-1 rounded text-[11px] font-mono-num font-bold transition border ${
              activeOverlays.levels
                ? "bg-emerald-500/20 text-emerald-300 border-emerald-500/40"
                : "bg-slate-800 text-slate-400 border-slate-700"
            }`}
          >
            Stop / Hedefler
          </button>
        </div>
      </div>

      {/* Candlestick Canvas Container */}
      <div ref={chartContainerRef} className="w-full relative" />

      {/* Sub-Indicator Panel: RSI or MACD Bar */}
      <div className="p-3 bg-[#0d121c] border-t border-[#1e293b]">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <button
              onClick={() => setSelectedSubIndicator("RSI")}
              className={`px-2 py-0.5 rounded text-[10px] font-bold tracking-wider uppercase transition ${
                selectedSubIndicator === "RSI"
                  ? "bg-sky-500 text-white"
                  : "bg-slate-800 text-slate-400 hover:text-white"
              }`}
            >
              RSI (14)
            </button>
            <button
              onClick={() => setSelectedSubIndicator("MACD")}
              className={`px-2 py-0.5 rounded text-[10px] font-bold tracking-wider uppercase transition ${
                selectedSubIndicator === "MACD"
                  ? "bg-sky-500 text-white"
                  : "bg-slate-800 text-slate-400 hover:text-white"
              }`}
            >
              MACD (12, 26, 9)
            </button>
          </div>

          <div className="text-right">
            {selectedSubIndicator === "RSI" ? (
              <span className="text-xs font-mono-num font-bold text-sky-400">
                RSI: {data[data.length - 1]?.rsi?.toFixed(1) ?? "61.4"}
              </span>
            ) : (
              <span className="text-xs font-mono-num font-bold text-emerald-400">
                MACD: +{data[data.length - 1]?.macd?.toFixed(2) ?? "4.82"}
              </span>
            )}
          </div>
        </div>

        {/* Mini Indicator Sparkline representation */}
        {selectedSubIndicator === "RSI" ? (
          <div className="relative h-10 w-full bg-[#111722] rounded border border-slate-800 flex items-center px-2">
            <div className="absolute top-1/3 left-0 right-0 border-b border-rose-500/20" />
            <div className="absolute top-2/3 left-0 right-0 border-b border-emerald-500/20" />
            <div className="w-full flex items-center justify-between text-[10px] text-slate-500 font-mono-num">
              <span>30 (Aşırı Satım)</span>
              <span className="text-sky-400 font-bold">50 (Denge)</span>
              <span>70 (Aşırı Alım)</span>
            </div>
          </div>
        ) : (
          <div className="relative h-10 w-full bg-[#111722] rounded border border-slate-800 flex items-center justify-center">
            <span className="text-xs font-mono-num text-emerald-400">
              Histogram: Pozitif İvme (+1.25) | Sinyal Üzerinde Bullish
            </span>
          </div>
        )}
      </div>
    </div>
  );
};
