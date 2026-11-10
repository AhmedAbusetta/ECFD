import React from "react";
import { VoiceUpdate } from "@/types";
import { Panel } from "./Panel";

/** Is the caller's voice a real human? Anti-spoofing score for the whole call. */
export const VoicePanel: React.FC<{ voice: VoiceUpdate | null; callActive: boolean }> = ({ voice, callActive }) => {
  const score = voice?.callScore;
  const pct = score === null || score === undefined ? null : Math.round(score * 100);

  let text = callActive ? "Waiting for the caller to speak" : "No call";
  let tone = "text-muted";
  if (voice) {
    if (pct === null) {
      text = "Checking…";
    } else if (voice.suspicious) {
      text = "Sounds computer-generated";
      tone = "text-danger";
    } else {
      text = "Sounds human";
      tone = "text-ok";
    }
  }

  return (
    <Panel title="Caller's voice">
      <p className={`text-sm font-medium ${tone}`}>{text}</p>
      {voice && pct !== null && (
        <>
          <div className="mt-2 h-1 rounded-full bg-line">
            <div className={`h-full rounded-full ${voice.suspicious ? "bg-danger" : "bg-ok"}`} style={{ width: `${pct}%` }} />
          </div>
          <p className="text-xs text-faint mt-2">
            {pct}% chance the voice is synthetic · {voice.sentences} sentence{voice.sentences === 1 ? "" : "s"} checked
          </p>
          <p className="text-xs text-faint mt-0.5">A supporting signal: it raises the risk but never raises an alert alone.</p>
        </>
      )}
    </Panel>
  );
};
