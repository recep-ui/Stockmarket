import React from "react";

interface ScoreGaugeProps {
  score: number;
  size?: "sm" | "md" | "lg";
  showLabel?: boolean;
}

export const ScoreGauge: React.FC<ScoreGaugeProps> = ({ score, size = "md", showLabel = false }) => {
  const getColor = (val: number) => {
    if (val >= 85) return { text: "text-emerald-400", bg: "bg-emerald-500", border: "border-emerald-500/30" };
    if (val >= 75) return { text: "text-green-400", bg: "bg-green-500", border: "border-green-500/30" };
    if (val >= 65) return { text: "text-purple-400", bg: "bg-purple-500", border: "border-purple-500/30" };
    if (val >= 50) return { text: "text-amber-400", bg: "bg-amber-500", border: "border-amber-500/30" };
    return { text: "text-rose-400", bg: "bg-rose-500", border: "border-rose-500/30" };
  };

  const colors = getColor(score);

  if (size === "sm") {
    return (
      <div className="flex items-center gap-1.5">
        <div className="w-10 bg-slate-800 rounded-full h-1.5 overflow-hidden">
          <div className={`h-full ${colors.bg} rounded-full`} style={{ width: `${score}%` }} />
        </div>
        <span className={`font-mono-num font-bold text-xs ${colors.text}`}>{score}</span>
      </div>
    );
  }

  if (size === "lg") {
    return (
      <div className="flex flex-col items-center justify-center p-4 bg-slate-900/60 rounded-xl border border-slate-800">
        <div className="relative flex items-center justify-center w-24 h-24">
          <svg className="w-full h-full transform -rotate-90">
            <circle
              cx="48"
              cy="48"
              r="40"
              stroke="#1e293b"
              strokeWidth="7"
              fill="transparent"
            />
            <circle
              cx="48"
              cy="48"
              r="40"
              stroke="currentColor"
              strokeWidth="7"
              strokeDasharray="251.2"
              strokeDashoffset={251.2 - (251.2 * score) / 100}
              strokeLinecap="round"
              fill="transparent"
              className={`${colors.text} transition-all duration-1000 ease-out`}
            />
          </svg>
          <div className="absolute flex flex-col items-center justify-center">
            <span className={`text-2xl font-bold font-mono-num ${colors.text}`}>{score}</span>
            <span className="text-[10px] text-slate-500 font-medium">/ 100</span>
          </div>
        </div>
        {showLabel && (
          <span className="mt-2 text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Kantitatif Skor
          </span>
        )}
      </div>
    );
  }

  // Default "md"
  return (
    <div className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded border bg-slate-900/80 ${colors.border}`}>
      <span className="text-[10px] text-slate-400 font-medium">SKOR</span>
      <span className={`font-mono-num font-bold text-xs ${colors.text}`}>{score}</span>
    </div>
  );
};
