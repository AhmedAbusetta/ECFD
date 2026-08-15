import React from "react";
import { TranscriptSegment } from "@/types";

interface LiveTranscriptProps {
  segments: TranscriptSegment[];
}

export const LiveTranscript: React.FC<LiveTranscriptProps> = ({ segments }) => {
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl flex-1 flex flex-col h-[400px]">
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider">Live Arabic Audio Transcript</h3>
        <span className="flex items-center text-xs text-emerald-400">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-ping mr-2"></span>
          Streaming
        </span>
      </div>

      <div className="flex-1 overflow-y-auto space-y-3 pr-2" dir="rtl">
        {segments.length === 0 ? (
          <div className="h-full flex items-center justify-center text-slate-600 text-sm">
            Waiting for audio stream from softphone...
          </div>
        ) : (
          segments.map((s, idx) => (
            <div key={idx} className="bg-slate-950 border border-slate-800/80 rounded-lg p-3">
              <p className="text-slate-100 text-base leading-relaxed font-sans">{s.text}</p>
              <div className="flex justify-between items-center mt-2 pt-2 border-t border-slate-900 text-[11px] text-slate-500 font-mono" dir="ltr">
                <span>Segment #{idx + 1}</span>
                <span>Confidence: {(s.confidence * 100).toFixed(0)}%</span>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
