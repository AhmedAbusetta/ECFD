import React, { useEffect, useRef } from "react";
import { TranscriptPartial, TranscriptSegment } from "@/types";

interface LiveTranscriptProps {
  segments: TranscriptSegment[];
  /** The sentence each side is speaking right now (at most one per speaker). */
  partials?: TranscriptPartial[];
}

export const LiveTranscript: React.FC<LiveTranscriptProps> = ({ segments, partials = [] }) => {
  const listRef = useRef<HTMLDivElement>(null);
  const liveText = partials.map((p) => p.text).join("|");

  // keep the newest words in view
  useEffect(() => {
    if (listRef.current) listRef.current.scrollTop = listRef.current.scrollHeight;
  }, [segments.length, liveText]);

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl flex-1 flex flex-col h-[400px]">
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider">Live Arabic Audio Transcript</h3>
        <span className="flex items-center text-xs text-emerald-400">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-ping mr-2"></span>
          Streaming
        </span>
      </div>

      <div ref={listRef} className="flex-1 overflow-y-auto space-y-3 pr-2" dir="rtl">
        {segments.length === 0 && partials.length === 0 ? (
          <div className="h-full flex items-center justify-center text-slate-600 text-sm">
            Waiting for audio stream from softphone...
          </div>
        ) : (
          segments.map((s, idx) => (
            <div
              key={idx}
              className={`border rounded-lg p-3 ${
                s.speaker === "EMPLOYEE" ? "bg-sky-950/40 border-sky-900/60 mr-8" : "bg-slate-950 border-slate-800/80 ml-8"
              }`}
            >
              <p className="text-slate-100 text-base leading-relaxed font-sans">{s.text}</p>
              <div className="flex justify-between items-center mt-2 pt-2 border-t border-slate-900 text-[11px] text-slate-500 font-mono" dir="ltr">
                <span>
                  #{idx + 1} · {s.speaker === "EMPLOYEE" ? "Employee" : "Caller"}
                </span>
                <span>Confidence: {(s.confidence * 100).toFixed(0)}%</span>
              </div>
            </div>
          ))
        )}
        {partials.map((partial) => (
          <div
            key={partial.speaker}
            className={`border border-dashed rounded-lg p-3 ${
              partial.speaker === "EMPLOYEE" ? "bg-sky-950/20 border-sky-800/60 mr-8" : "bg-slate-950/60 border-slate-700 ml-8"
            }`}
          >
            <p className="text-base leading-relaxed font-sans">
              {/* confirmed words stay put; grey words are still being worked out */}
              {partial.parts && partial.parts.length > 0
                ? partial.parts.map((part, i) => (
                    <span key={i} className={part.stable ? "text-slate-100" : "text-slate-500"}>
                      {part.text}{" "}
                    </span>
                  ))
                : <span className="text-slate-500">{partial.text}</span>}
            </p>
            <div className="flex items-center gap-2 mt-2 pt-2 border-t border-slate-900 text-[11px] text-emerald-400 font-mono" dir="ltr">
              <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              live · {partial.speaker === "EMPLOYEE" ? "Employee" : "Caller"} speaking… (grey words may still change)
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
