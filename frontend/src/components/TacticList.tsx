import React from "react";
import { TacticEvidence } from "@/types";

interface TacticListProps {
  tactics: TacticEvidence[];
}

export const TacticList: React.FC<TacticListProps> = ({ tactics }) => {
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl h-[400px] flex flex-col">
      <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-4">Detected Social Engineering Tactics</h3>
      <div className="flex-1 overflow-y-auto space-y-2 pr-1">
        {tactics.length === 0 ? (
          <div className="h-full flex items-center justify-center text-slate-600 text-sm">
            No malicious tactics detected
          </div>
        ) : (
          tactics.map((t, idx) => (
            <div key={idx} className="flex justify-between items-center p-2.5 bg-slate-950 border border-slate-800 rounded-lg">
              <div>
                <span className="text-xs font-bold text-amber-400 font-mono">{t.tactic}</span>
                <span className="text-[10px] text-slate-500 block">{new Date(t.timestamp).toLocaleTimeString()}</span>
              </div>
              <span className="px-2 py-0.5 bg-amber-400/10 text-amber-400 border border-amber-400/20 rounded text-[11px] font-bold">
                {(t.confidence * 100).toFixed(0)}%
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
