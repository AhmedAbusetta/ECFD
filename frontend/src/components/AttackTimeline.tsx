import React from "react";
import { Panel } from "./Panel";

interface AttackTimelineProps {
  currentStage: string;
}

const STAGES = [
  { id: "Normal", label: "Normal call" },
  { id: "IdentityClaim", label: "Says who they are" },
  { id: "Pressure", label: "Applies pressure" },
  { id: "SensitiveAction", label: "Asks for a risky action" },
  { id: "CredentialExtraction", label: "Asks for a secret" },
];

/** How far the call has moved toward a request: a thin line with five steps. */
export const AttackTimeline: React.FC<AttackTimelineProps> = ({ currentStage }) => {
  const active = Math.max(0, STAGES.findIndex((s) => s.id.toLowerCase() === currentStage.toLowerCase()));

  return (
    <Panel title="How far the call has gone" className="border-t border-line !pt-5">
      <ol className="grid grid-cols-5">
        {STAGES.map((s, i) => {
          const reached = i <= active;
          const risky = i === active && i >= 3;
          return (
            <li key={s.id} className="relative flex flex-col items-center text-center">
              {i > 0 && <span className={`absolute right-1/2 top-[5px] h-px w-full ${i <= active ? "bg-accent" : "bg-line"}`} />}
              <span
                className={`relative z-10 h-[11px] w-[11px] rounded-full border-2 ${
                  risky ? "border-danger bg-danger" : reached ? "border-accent bg-accent" : "border-line bg-surface"
                }`}
              />
              <span className={`mt-2 px-1 text-xs ${risky ? "font-semibold text-danger" : reached ? "text-ink" : "text-faint"}`}>{s.label}</span>
            </li>
          );
        })}
      </ol>
    </Panel>
  );
};
