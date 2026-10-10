import React from "react";
import { VoiceUpdate } from "@/types";

/** Is the caller's voice a real human? Anti-spoofing score per sentence and for the whole call. */
export const VoicePanel: React.FC<{ voice: VoiceUpdate | null; callActive: boolean }> = ({ voice, callActive }) => {
  const score = voice?.callScore;
  let status: { text: string; style: string };
  if (!voice) {
    status = callActive
      ? { text: "Waiting for the caller to speak", style: "bg-slate-800 text-slate-300" }
      : { text: "No call", style: "bg-slate-800 text-slate-400" };
  } else if (score === null || score === undefined) {
    status = { text: "Checking…", style: "bg-sky-800 text-sky-100" };
  } else if (voice.suspicious) {
    status = { text: "Suspected synthetic voice", style: "bg-red-600 text-white" };
  } else {
    status = { text: "Human voice", style: "bg-emerald-700 text-white" };
  }
  const pct = score === null || score === undefined ? null : Math.round(score * 100);

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-xl mb-6">
      <div className="flex justify-between items-center mb-3">
        <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider">Caller voice authenticity</h3>
        <span className={`px-2 py-0.5 rounded text-[11px] font-bold ${status.style}`}>{status.text}</span>
      </div>
      {voice ? (
        <>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-extrabold text-white">{pct === null ? "–" : `${pct}%`}</span>
            <span className="text-xs text-slate-500">chance the voice is synthetic (whole call)</span>
          </div>
          <div className="h-2 bg-slate-800 rounded mt-2 overflow-hidden">
            <div
              className={`h-full ${voice.suspicious ? "bg-red-500" : "bg-emerald-500"}`}
              style={{ width: `${pct ?? 0}%` }}
            />
          </div>
          <p className="text-[11px] text-slate-500 font-mono mt-2">
            {voice.sentences} sentence{voice.sentences === 1 ? "" : "s"} checked · last {Math.round(voice.sentenceScore * 100)}% · {voice.model}
          </p>
          <p className="text-[10px] text-slate-600 mt-1">Supporting signal only: raises the risk, never alerts on its own.</p>
        </>
      ) : (
        <p className="text-sm text-slate-600">Checked on the caller&apos;s speech during real calls.</p>
      )}
    </div>
  );
};
