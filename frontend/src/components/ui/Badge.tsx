import React from "react";
import { SignalType } from "@/types";

interface BadgeProps {
  type: SignalType | string;
  className?: string;
  size?: "sm" | "md" | "lg";
}

export const SignalBadge: React.FC<BadgeProps> = ({ type, className = "", size = "md" }) => {
  const getStyle = () => {
    switch (type) {
      case "StrongBuy":
        return "bg-emerald-500/15 text-emerald-400 border-emerald-500/40 shadow-[0_0_10px_rgba(16,185,129,0.2)]";
      case "Buy":
        return "bg-green-500/15 text-green-400 border-green-500/30";
      case "BuyCandidate":
        return "bg-purple-500/15 text-purple-400 border-purple-500/30";
      case "Watch":
        return "bg-amber-500/15 text-amber-400 border-amber-500/30";
      case "Weak":
        return "bg-slate-500/15 text-slate-400 border-slate-500/30";
      case "Sell":
        return "bg-orange-500/15 text-orange-400 border-orange-500/30";
      case "StrongSell":
        return "bg-rose-500/15 text-rose-400 border-rose-500/40 shadow-[0_0_10px_rgba(239,68,68,0.2)]";
      default:
        return "bg-slate-700/20 text-slate-300 border-slate-700";
    }
  };

  const getLabel = () => {
    switch (type) {
      case "StrongBuy":
        return "GÜÇLÜ AL";
      case "Buy":
        return "AL";
      case "BuyCandidate":
        return "AL ADAYI";
      case "Watch":
        return "İZLE";
      case "Weak":
        return "ZAYIF";
      case "Sell":
        return "SAT";
      case "StrongSell":
        return "GÜÇLÜ SAT";
      default:
        return type;
    }
  };

  const sizeClasses = {
    sm: "px-1.5 py-0.5 text-[10px]",
    md: "px-2.5 py-1 text-xs",
    lg: "px-3.5 py-1.5 text-sm font-semibold",
  }[size];

  return (
    <span
      className={`inline-flex items-center justify-center font-bold tracking-wider rounded-md border uppercase ${sizeClasses} ${getStyle()} ${className}`}
    >
      {getLabel()}
    </span>
  );
};
