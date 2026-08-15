import React from "react";
import { RiskContributor } from "@/types";

interface RiskGaugeProps {
  score: number;
  severity: string;
  stage: string;
  contributors: RiskContributor[];
}

export const RiskGauge: React.FC<RiskGaugeProps> = ({ score, severity, stage, contributors }) => {
  const getBadgeColor = () => {
    switch (severity.toLowerCase()) {
      case "critical": return "bg-red-600 text-white";
      case "high": return "bg-orange-500 text-white";
      case "medium": return "bg-yellow-500 text-black";
      default: return "bg-green-600 text-white";
    }
  };

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl flex flex-col items-center">
      <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">Live Conversational Risk</h3>
      
      {/* Risk Circle */}
      <div className="relative w-40 h-40 flex items-center justify-center my-4">
        <div className="text-center">
          <span className="text-5xl font-extrabold text-white tracking-tight">{score}</span>
          <span className="text-slate-400 text-sm block">/ 100</span>
        </div>
      </div>

      <span className={`px-4 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider ${getBadgeColor()}`}>
        {severity} Risk
      </span>

      <div className="mt-4 text-center">
        <span className="text-xs text-slate-500">Current Attack Stage:</span>
        <p className="text-sm font-semibold text-sky-400">{stage}</p>
      </div>

      {/* Top Contributors */}
      <div className="w-full mt-6 pt-4 border-t border-slate-800">
        <h4 className="text-xs font-medium text-slate-400 mb-3">Top Contributors</h4>
        <div className="space-y-2">
          {contributors.map((c, i) => (
            <div key={i} className="flex justify-between text-xs">
              <span className="text-slate-300 font-mono">{c.type}</span>
              <span className="text-red-400 font-bold">+{c.contribution}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
