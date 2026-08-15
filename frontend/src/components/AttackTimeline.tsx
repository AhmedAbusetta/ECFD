import React from "react";

interface AttackTimelineProps {
  currentStage: string;
}

const STAGES = [
  { id: "Normal", label: "Normal Call" },
  { id: "IdentityClaim", label: "Identity Claim" },
  { id: "Pressure", label: "Pressure / Urgency" },
  { id: "SensitiveAction", label: "Sensitive Action" },
  { id: "CredentialExtraction", label: "Credential Extraction" }
];

export const AttackTimeline: React.FC<AttackTimelineProps> = ({ currentStage }) => {
  const getStageIndex = (stage: string) => STAGES.findIndex(s => s.id.toLowerCase() === stage.toLowerCase());
  const activeIdx = getStageIndex(currentStage);

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl w-full">
      <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-6">Attack Progression State Machine</h3>
      
      <div className="grid grid-cols-5 gap-2 relative">
        {STAGES.map((s, idx) => {
          const isPassed = idx < activeIdx;
          const isCurrent = idx === activeIdx;
          return (
            <div key={s.id} className="flex flex-col items-center text-center">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold mb-2 transition-all ${
                  isCurrent
                    ? "bg-red-500 text-white ring-4 ring-red-500/30 scale-110"
                    : isPassed
                    ? "bg-sky-600 text-white"
                    : "bg-slate-800 text-slate-500"
                }`}
              >
                {idx + 1}
              </div>
              <span className={`text-xs font-medium ${isCurrent ? "text-red-400 font-bold" : isPassed ? "text-slate-300" : "text-slate-600"}`}>
                {s.label}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
};
