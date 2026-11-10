import React from "react";
import { RiskContributor } from "@/types";
import { describeContributor, RISK_SCALE, riskBand } from "./labels";

interface RiskGaugeProps {
  score: number;
  severity: string;
  stage: string;
  contributors: RiskContributor[];
}

const STAGES: Record<string, string> = {
  Normal: "Ordinary conversation",
  IdentityClaim: "Caller said who they are",
  Pressure: "Caller is applying pressure",
  SensitiveAction: "Caller asked for a risky action",
  CredentialExtraction: "Caller asked for a secret",
};

/** The score with its meaning: where it sits on the scale and what it is made of. */
export const RiskGauge: React.FC<RiskGaugeProps> = ({ score, stage, contributors }) => {
  const band = riskBand(score);
  const color = score >= 60 ? "bg-red-500" : score >= 30 ? "bg-amber-400" : "bg-emerald-500";

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl">
      <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-3">Risk score</h3>

      <div className="flex items-baseline gap-2">
        <span className="text-5xl font-extrabold text-white tracking-tight">{score}</span>
        <span className="text-slate-400 text-sm">/ 100</span>
      </div>
      <p className="text-sm font-semibold text-slate-200 mt-1">{band.label}</p>

      <div className="w-full h-2 bg-slate-800 rounded-full mt-3 overflow-hidden">
        <div className={`h-full ${color} transition-all duration-500`} style={{ width: `${score}%` }}></div>
      </div>

      <ul className="mt-3 space-y-1">
        {RISK_SCALE.map((b, i) => {
          const to = RISK_SCALE[i + 1] ? RISK_SCALE[i + 1].from - 1 : 100;
          const active = band.from === b.from;
          return (
            <li key={b.from} className={`flex justify-between text-[11px] ${active ? "text-white font-semibold" : "text-slate-500"}`}>
              <span>{b.label}</span>
              <span className="font-mono">{b.from}–{to}</span>
            </li>
          );
        })}
      </ul>

      <div className="mt-4 pt-4 border-t border-slate-800">
        <span className="text-xs text-slate-500">How far the call has gone</span>
        <p className="text-sm font-semibold text-sky-400">{STAGES[stage] ?? stage}</p>
      </div>

      <div className="w-full mt-4 pt-4 border-t border-slate-800">
        <h4 className="text-xs font-medium text-slate-400 mb-2">Why this score</h4>
        {contributors.length === 0 ? (
          <p className="text-xs text-slate-600">Nothing has raised the score.</p>
        ) : (
          <div className="space-y-2">
            {contributors.map((c, i) => (
              <div key={i} className="flex justify-between gap-3 text-xs">
                <span className="text-slate-300">{describeContributor(c.type)}</span>
                <span className="text-red-400 font-bold font-mono">{c.type === "AI_CALL_ANALYST" || c.type === "FORBIDDEN_REQUEST" ? c.contribution : `+${c.contribution}`}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
