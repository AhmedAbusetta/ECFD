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

        <div className="flex flex-col gap-3">
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

/** One turn as a chat bubble: the caller on the left in grey, the employee on the right in blue. */
const Line: React.FC<{ speaker?: string; live?: boolean; children: React.ReactNode }> = ({ speaker, live, children }) => {
  const employee = speaker === "EMPLOYEE";
  return (
    <div className={`flex max-w-[88%] flex-col ${employee ? "items-end self-end" : "items-start self-start"}`}>
      <span className="mb-1 px-1 text-xs text-muted">
        {who(speaker)}
        {live ? " · speaking" : ""}
      </span>
      <div
        dir="rtl"
        className={`rounded-2xl px-4 py-2.5 ${employee ? "rounded-br-md" : "rounded-bl-md"} ${
          live ? "border border-dashed border-line" : employee ? "bg-[#1b2b45]" : "bg-[#222226]"
        }`}
      >
        <p className="text-[17px] leading-relaxed">{children}</p>
      </div>
    </div>
  );
};
