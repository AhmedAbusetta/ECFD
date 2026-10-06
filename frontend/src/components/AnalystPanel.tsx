import React from "react";
import { AnalystUpdate } from "@/types";

const LEVEL_STYLE: Record<string, string> = {
  ALERT: "bg-red-600 text-white",
  WARNING: "bg-amber-500 text-slate-950",
  WATCH: "bg-sky-700 text-white",
  NONE: "bg-slate-800 text-slate-300",
};

const Row: React.FC<{ label: string; children: React.ReactNode }> = ({ label, children }) => (
  <div>
    <div className="text-[10px] uppercase tracking-wider text-slate-500">{label}</div>
    <div className="text-sm text-slate-200">{children}</div>
  </div>
);

/** What the AI call analyst thinks of the whole call so far (ml/brain, ADR-0005). */
export const AnalystPanel: React.FC<{ update: AnalystUpdate | null; pending: number }> = ({ update, pending }) => {
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl mb-6">
      <div className="flex flex-wrap justify-between items-center gap-3 mb-4">
        <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider">AI call analyst</h3>
        <div className="flex items-center gap-2 text-[11px] font-mono text-slate-500">
          {pending > 0 && <span className="text-sky-400 animate-pulse">thinking… ({pending})</span>}
          {update && (
            <span>
              turn {update.turn} · {update.model || "no model"} · {update.analyzed ? `${(update.latencyMs / 1000).toFixed(1)} s` : "floor only"}
            </span>
          )}
          {update && <span className={`px-2 py-0.5 rounded font-bold ${LEVEL_STYLE[update.level] || LEVEL_STYLE.NONE}`}>{update.level}</span>}
        </div>
      </div>

      {!update ? (
        <p className="text-sm text-slate-600">Waiting for the first turn. The analyst judges the whole call after every turn from either side.</p>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
          <div className="space-y-3">
            <Row label="Risk (final · LLM · floor)">
              <span className="text-2xl font-extrabold text-white">{update.risk}</span>
              <span className="text-slate-500 font-mono text-xs ml-2">
                LLM {update.llmRisk ?? "–"} · floor {update.floor} · {update.trend.toLowerCase()}
              </span>
            </Row>
            <Row label="Stage">{update.stage}</Row>
            <Row label="Employee">{update.employeeState.replaceAll("_", " ").toLowerCase()}</Row>
          </div>
          <div className="space-y-3">
            <Row label="Caller is after">{update.callerGoal || "–"}</Row>
            <Row label="Strategy">{update.strategy || "–"}</Row>
            <Row label="Likely next move">{update.nextLikelyMove || "–"}</Row>
          </div>
          <div className="space-y-3">
            {update.alertAr && (
              <div className="bg-red-950/60 border border-red-800 rounded-lg p-3 text-base text-red-100 leading-relaxed" dir="rtl">
                {update.alertAr}
              </div>
            )}
            {update.evidence.length > 0 && (
              <Row label="Evidence">
                <ul className="space-y-1">
                  {update.evidence.map((e, i) => (
                    <li key={i} className="text-xs">
                      <span className="font-mono text-slate-500">[{e.turn}] {e.label}</span>{" "}
                      <span dir="rtl" className="text-slate-200">«{e.quote}»</span>
                    </li>
                  ))}
                </ul>
              </Row>
            )}
            {update.hardSignals.length > 0 && <Row label="Hard signals (floor)">{update.hardSignals.join(" · ")}</Row>}
            {update.error && <p className="text-[11px] text-amber-400 font-mono">{update.error}</p>}
          </div>
        </div>
      )}
    </div>
  );
};
