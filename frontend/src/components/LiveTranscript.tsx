import React, { useEffect, useRef } from "react";
import { TranscriptPartial, TranscriptSegment } from "@/types";
import { Panel } from "./Panel";

interface LiveTranscriptProps {
  segments: TranscriptSegment[];
  /** The sentence each side is speaking right now (at most one per speaker). */
  partials?: TranscriptPartial[];
}

const who = (speaker?: string) => (speaker === "EMPLOYEE" ? "Employee" : "Caller");

export const LiveTranscript: React.FC<LiveTranscriptProps> = ({ segments, partials = [] }) => {
  const listRef = useRef<HTMLDivElement>(null);
  const stick = useRef(true); // follow the newest words, unless the reader scrolled up
  const liveText = partials.map((p) => p.text).join("|");

  useEffect(() => {
    const el = listRef.current;
    if (el && stick.current) el.scrollTop = el.scrollHeight;
  }, [segments.length, liveText]);

  const onScroll = () => {
    const el = listRef.current;
    if (el) stick.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  };

  const empty = segments.length === 0 && partials.length === 0;

  return (
    <Panel title="Conversation">
      <div ref={listRef} onScroll={onScroll} className="h-[480px] overflow-y-auto pr-2">
        {empty && <p className="pt-2 text-sm text-faint">The conversation appears here as people speak.</p>}

        <div className="divide-y divide-line">
          {segments.map((s, i) => (
            <Line key={i} speaker={s.speaker}>
              {s.text}
            </Line>
          ))}
          {partials.map((p) => (
            <Line key={p.speaker} speaker={p.speaker} live>
              {/* confirmed words stay put; lighter words are still being worked out */}
              {p.parts && p.parts.length > 0
                ? p.parts.map((part, i) => (
                    <span key={i} className={part.stable ? "text-ink" : "text-faint"}>
                      {part.text}{" "}
                    </span>
                  ))
                : <span className="text-faint">{p.text}</span>}
            </Line>
          ))}
        </div>
      </div>
    </Panel>
  );
};

/** One turn: who spoke, then what they said. Arabic reads right to left, so the name sits on the right. */
const Line: React.FC<{ speaker?: string; live?: boolean; children: React.ReactNode }> = ({ speaker, live, children }) => (
  <div dir="rtl" className="grid grid-cols-[84px_1fr] gap-4 py-3 first:pt-1">
    <div dir="ltr" className="pt-1 text-right text-xs text-muted">
      {who(speaker)}
      {live && <span className="block text-faint">speaking</span>}
    </div>
    <p className="text-[17px] leading-relaxed">{children}</p>
  </div>
);
