import React from "react";
import { AnalystUpdate } from "@/types";

const Row: React.FC<{ label: string; children: React.ReactNode }> = ({ label, children }) => (
  <div>
    <dt className="text-xs text-muted">{label}</dt>
    <dd className="text-sm mt-0.5">{children}</dd>
  </div>
);

/** What the AI call analyst thinks of the whole call so far (ml/brain, ADR-0005). Closed by default. */
export const AnalystPanel: React.FC<{ update: AnalystUpdate | null; pending: number }> = ({ update, pending }) => (
  <details className="border-t border-line pt-5">
    <summary className="flex cursor-pointer list-none items-baseline justify-between gap-4 text-[13px] font-semibold">
      <span>Analyst details</span>
      <span className="text-xs font-normal text-muted">
        {pending > 0 ? "reading the latest sentence… · " : ""}
        {update ? `turn ${update.turn} · ${update.analyzed ? `${(update.latencyMs / 1000).toFixed(1)} s` : "instant rules only"} · ${update.model || "no model"}` : "waiting for the first turn"}
      </span>
    </summary>

    <div className="mt-4">
      {!update ? (
        <p className="text-sm text-faint">The analyst judges the whole call after every turn, from either side.</p>
      ) : (
        <dl className="grid grid-cols-1 gap-x-8 gap-y-4 lg:grid-cols-3">
          <div className="space-y-4">
            <Row label="Risk">
              <span className="text-xl font-semibold tabular-nums">{update.risk}</span>
              <span className="ml-2 text-xs text-muted">
                AI {update.llmRisk ?? "–"} · rules floor {update.floor} · {update.trend.toLowerCase()}
              </span>
            </Row>
            <Row label="How far the call has gone">{update.stage}</Row>
            <Row label="Employee">{update.employeeState.replaceAll("_", " ").toLowerCase()}</Row>
          </div>
          <div className="space-y-4">
            <Row label="The caller is after">{update.callerGoal || "–"}</Row>
            {/* the compact live analyst only fills these in its full-report mode */}
            {update.strategy && <Row label="Strategy">{update.strategy}</Row>}
            {update.nextLikelyMove && <Row label="Likely next move">{update.nextLikelyMove}</Row>}
          </div>
          <div className="space-y-4">
            {update.evidence.length > 0 && (
              <Row label="Evidence">
                <ul className="space-y-1">
                  {update.evidence.map((e, i) => (
                    <li key={i} className="text-xs">
                      <span className="text-muted">[{e.turn}] {e.label}</span>{" "}
                      <span dir="rtl">«{e.quote}»</span>
                    </li>
                  ))}
                </ul>
              </Row>
            )}
            {update.hardSignals.length > 0 && <Row label="Instant rule flags">{update.hardSignals.join(" · ")}</Row>}
            {update.error && <p className="text-xs text-warn">{update.error}</p>}
          </div>
        </dl>
      )}
    </div>
  </details>
);
