import React from "react";
import { RiskContributor } from "@/types";
import { describeContributor, riskBand } from "./labels";
import { Panel } from "./Panel";

interface RiskGaugeProps {
  score: number;
  severity: string;
  stage: string;
  contributors: RiskContributor[];
}

/** How the score is made: a bar for where it sits and the list of what added to it. */
export const RiskGauge: React.FC<RiskGaugeProps> = ({ score, contributors }) => {
  const band = riskBand(score);
  const color = score >= 60 ? "bg-danger" : score >= 30 ? "bg-warn" : "bg-ok";

  return (
    <Panel title="Risk">
      <div className="flex items-baseline justify-between text-sm">
        <span>{band.label}</span>
        <span className="tabular-nums text-muted">{score} / 100</span>
      </div>
      <div className="mt-2 h-1 rounded-full bg-line">
        <div className={`h-full rounded-full ${color} transition-all duration-500`} style={{ width: `${score}%` }} />
      </div>
      <p className="mt-2 text-xs text-faint">0–29 normal · 30–59 unusual · 60–79 suspicious · 80+ scam attempt</p>

      <h4 className="mb-1.5 mt-4 text-xs text-muted">What raised the score</h4>
      {contributors.length === 0 ? (
        <p className="text-sm text-faint">Nothing so far.</p>
      ) : (
        <ul className="space-y-1">
          {contributors.map((c, i) => (
            <li key={i} className="flex justify-between gap-4 text-sm">
              <span>{describeContributor(c.type)}</span>
              <span className="tabular-nums text-muted">
                {c.type === "AI_CALL_ANALYST" || c.type === "FORBIDDEN_REQUEST" ? c.contribution : `+${c.contribution}`}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
};
