import React from "react";
import { AnalystUpdate, CallSession, EmployeeWarning, TacticEvidence, VoiceUpdate } from "@/types";
import { describeHardSignal, describeTactic, riskBand, VerdictLevel } from "./labels";

interface VerdictBannerProps {
  session: CallSession | null;
  /** The call is over; the banner then shows how it was judged. */
  ended?: boolean;
  riskScore: number;
  analyst: AnalystUpdate | null;
  analystPending: number;
  tactics: TacticEvidence[];
  hardSignals: string[];
  voice: VoiceUpdate | null;
  warnings: EmployeeWarning[];
}

const STYLE: Record<VerdictLevel, { title: string }> = {
  idle: { title: "text-muted" },
  safe: { title: "text-ink" },
  watch: { title: "text-ink" },
  danger: { title: "text-danger" },
};

const FALLBACK_ADVICE = "متديش أي بيانات، واقفل وكلّم الرقم الرسمي.";

/**
 * The one place that answers "is this call a scam, and why?" in a sentence. Everything below it on the page
 * is the detail behind that sentence.
 */
export const VerdictBanner: React.FC<VerdictBannerProps> = ({
  session, ended, riskScore, analyst, analystPending, tactics, hardSignals, voice, warnings,
}) => {
  // what was actually asked for, newest first, without repeats
  const reasons: string[] = [];
  const add = (r: string) => !reasons.includes(r) && reasons.push(r);
  [...hardSignals].reverse().forEach((s) => add(describeHardSignal(s)));
  [...tactics].reverse().forEach((t) => add(describeTactic(t.tactic)));
  if (voice?.suspicious) add(describeTactic("VOICE_SPOOF"));

  const band = riskBand(riskScore);
  const level: VerdictLevel = !session
    ? "idle"
    : analyst?.level === "ALERT" || (reasons.length > 0 && riskScore >= 60) ? "danger" : band.level;

  // the analyst answered, but without the AI's judgement (service down, bad key): only the instant rules ran
  const analystDown = !!analyst && !analyst.analyzed && !!analyst.error && !analyst.error.startsWith("newer turn");

  let title = "No active call";
  let detail = "Waiting for a call on the phone system.";
  let barLevel: VerdictLevel = level;
  if (session) {
    if (level === "danger") {
      title = reasons.length > 0 ? `Scam attempt: the caller ${reasons[0]}` : "Scam attempt in progress";
      detail = reasons.length > 1 ? `Also: the caller ${reasons.slice(1).join("; ")}.` : analyst?.callerGoal ? `What the caller is after: ${analyst.callerGoal}.` : "";
    } else if (level === "watch") {
      title = "Unusual call, but nothing forbidden has been asked";
      detail = analyst?.callerGoal ? `What the caller seems to want: ${analyst.callerGoal}.` : "ECFD keeps listening.";
    } else if (analystDown) {
      title = "No forbidden request heard, but the AI analyst is not running";
      detail = "Only the instant rules checked this call. They catch direct requests, not a scam told as a story.";
      barLevel = "watch"; // not a clean bill of health
    } else {
      title = "Nothing suspicious so far";
      detail = analystPending > 0 && !analyst ? "The AI analyst is reading the first sentences." : "Normal conversation.";
    }
    if (ended) {
      title = level === "safe" && !analystDown ? "Call ended · nothing suspicious was found" : `Call ended · ${title.charAt(0).toLowerCase()}${title.slice(1)}`;
      if (level !== "danger" && !analystDown) detail = "";
    }
  }
  const style = STYLE[barLevel];
  const line = session?.callerLine;

  return (
    <section>
      <div className="flex items-start justify-between gap-10">
        <div className="min-w-0">
          <h2 className={`text-[28px] font-semibold leading-tight tracking-tight ${style.title}`}>{title}</h2>
          {detail && <p className="mt-2 text-[15px] text-muted">{detail}</p>}
        </div>
        {session && (
          <div className="shrink-0 text-right">
            <div className="text-5xl font-semibold leading-none tabular-nums">{riskScore}</div>
            <div className="mt-2 text-xs text-muted">risk, out of 100</div>
          </div>
        )}
      </div>

      {level === "danger" && (
        <p dir="rtl" className="mt-5 text-xl font-semibold leading-relaxed text-danger">
          {analyst?.alertAr || FALLBACK_ADVICE}
        </p>
      )}

      {session && (
        <dl className="mt-6 flex flex-wrap gap-x-9 gap-y-2 text-sm">
          <Fact
            label="Caller's line"
            value={line === "EXTERNAL" ? "outside the organisation" : line === "INTERNAL" ? "internal extension" : "unknown"}
          />
          <Fact
            label="Caller's voice"
            bad={voice?.suspicious}
            value={!voice ? "not checked yet" : voice.suspicious ? "sounds computer-generated" : voice.callScore === null ? "checking" : "sounds human"}
          />
          <Fact
            label="Employee warned"
            bad={warnings.length > 0}
            value={warnings.length === 0 ? "no" : `yes, ${warnings.length} time${warnings.length > 1 ? "s" : ""}`}
          />
          <Fact
            label="AI analyst"
            bad={analystDown}
            value={analystDown ? "not running" : ended ? "finished" : analystPending > 0 ? "reading the latest sentence" : analyst ? "up to date" : "waiting"}
          />
        </dl>
      )}
    </section>
  );
};

const Fact: React.FC<{ label: string; value: string; bad?: boolean }> = ({ label, value, bad }) => (
  <div className="flex gap-2">
    <dt className="text-muted">{label}</dt>
    <dd className={bad ? "font-medium text-danger" : "text-ink"}>{value}</dd>
  </div>
);
