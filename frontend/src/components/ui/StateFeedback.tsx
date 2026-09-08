"use client";

import React from "react";
import { AlertCircle, RefreshCw, Inbox, Loader2 } from "lucide-react";

export interface ErrorBannerProps {
  title?: string;
  message: string;
  onRetry?: () => void;
}

export const ErrorBanner: React.FC<ErrorBannerProps> = ({
  title = "Veri Yükleme Hatası",
  message,
  onRetry
}) => {
  return (
    <div className="terminal-card p-4 border border-rose-500/30 bg-rose-500/10 rounded-xl">
      <div className="flex items-start gap-3">
        <AlertCircle className="w-5 h-5 text-rose-400 shrink-0 mt-0.5" />
        <div className="flex-1">
          <h4 className="text-sm font-bold text-rose-300">{title}</h4>
          <p className="text-xs text-rose-200/80 mt-1 leading-relaxed">{message}</p>
          {onRetry && (
            <button
              onClick={onRetry}
              className="mt-3 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold shadow-lg shadow-rose-600/20 transition"
            >
              <RefreshCw className="w-3.5 h-3.5" />
              <span>Tekrar Dene</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export interface LoadingSkeletonProps {
  rows?: number;
  text?: string;
}

export const LoadingSkeleton: React.FC<LoadingSkeletonProps> = ({
  rows = 3,
  text = "Veriler yükleniyor..."
}) => {
  return (
    <div className="terminal-card p-6 space-y-4">
      <div className="flex items-center gap-2.5 text-slate-400 text-xs font-semibold">
        <Loader2 className="w-4 h-4 animate-spin text-sky-400" />
        <span>{text}</span>
      </div>
      <div className="space-y-2.5">
        {Array.from({ length: rows }).map((_, i) => (
          <div
            key={i}
            className="h-10 rounded-lg bg-slate-800/40 animate-pulse border border-slate-800"
          />
        ))}
      </div>
    </div>
  );
};

export interface EmptyStateProps {
  title?: string;
  description?: string;
  actionText?: string;
  onAction?: () => void;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  title = "Kayıt Bulunamadı",
  description = "Arama kriterlerinize uygun veri bulunmuyor.",
  actionText,
  onAction
}) => {
  return (
    <div className="terminal-card p-10 text-center border border-slate-800">
      <div className="w-12 h-12 rounded-full bg-slate-800/60 border border-slate-700 mx-auto flex items-center justify-center text-slate-400 mb-3">
        <Inbox className="w-6 h-6" />
      </div>
      <h3 className="text-sm font-bold text-slate-200">{title}</h3>
      <p className="text-xs text-slate-400 mt-1 max-w-sm mx-auto">{description}</p>
      {actionText && onAction && (
        <button
          onClick={onAction}
          className="mt-4 px-3 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold transition"
        >
          {actionText}
        </button>
      )}
    </div>
  );
};
