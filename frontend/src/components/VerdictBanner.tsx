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

const STYLE: Record<VerdictLevel, { box: string; dot: string; title: string }> = {
  idle: { box: "bg-slate-900 border-slate-800", dot: "bg-slate-600", title: "text-slate-300" },
  safe: { box: "bg-emerald-950/40 border-emerald-700", dot: "bg-emerald-400", title: "text-emerald-200" },
  watch: { box: "bg-amber-950/40 border-amber-600", dot: "bg-amber-400", title: "text-amber-200" },
  danger: { box: "bg-red-950/60 border-red-600", dot: "bg-red-500 animate-pulse", title: "text-red-100" },
};

const FALLBACK_ADVICE = "متديش أي بيانات، واقفل وكلّم الرقم الرسمي.";

/**
 * The one place that answers "is this call a scam, and why?" in a sentence. The number, the tactics and the
 * analyst's notes further down are the detail behind it.
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
  const level: VerdictLevel = !session ? "idle" : analyst?.level === "ALERT" || reasons.length > 0 && riskScore >= 60 ? "danger" : band.level;
  let style = STYLE[level];

  let title = "No active call";
  let detail = "Waiting for a call on the phone system.";
  if (session) {
    if (level === "danger") {
      title = reasons.length > 0 ? `Scam attempt: the caller ${reasons[0]}` : "Scam attempt in progress";
      detail = reasons.length > 1 ? `Also: the caller ${reasons.slice(1).join("; ")}.` : analyst?.callerGoal ? `What the caller is after: ${analyst.callerGoal}.` : "";
    } else if (level === "watch") {
      title = "Unusual call, but nothing forbidden has been asked";
      detail = analyst?.callerGoal ? `What the caller seems to want: ${analyst.callerGoal}.` : "ECFD keeps listening.";
    } else {
      title = "Nothing suspicious so far";
      detail = analystPending > 0 && !analyst ? "The AI analyst is reading the first sentences." : "Normal conversation.";
    }
  }

  // the analyst answered, but without the AI's judgement (service down, bad key): only the instant rules ran
  const analystDown = !!analyst && !analyst.analyzed && !!analyst.error && !analyst.error.startsWith("newer turn");
  if (session && level === "safe" && analystDown) {
    title = "No forbidden request heard, but the AI analyst is not running";
    detail = "Only the instant rules checked this call. They catch direct requests, not a scam told as a story.";
    style = STYLE.watch; // not a clean bill of health
  }
  if (session && ended) {
    title = level === "safe" && !analystDown ? "Call ended · nothing suspicious was found" : `Call ended · ${title.charAt(0).toLowerCase()}${title.slice(1)}`;
    if (level !== "danger" && !analystDown) detail = "";
  }

  const line = session?.callerLine;
  return (
    <div className={`border-2 rounded-xl p-5 mb-6 ${style.box}`}>
      <div className="flex items-start justify-between gap-6">
        <div className="flex items-start gap-4">
          <span className={`mt-2 w-3.5 h-3.5 rounded-full shrink-0 ${style.dot}`}></span>
          <div>
            <h2 className={`text-2xl font-extrabold tracking-tight ${style.title}`}>{title}</h2>
            {detail && <p className="text-sm text-slate-300 mt-1">{detail}</p>}
            {level === "danger" && (
              <p dir="rtl" className="text-lg text-white mt-3 font-semibold">
                {analyst?.alertAr || FALLBACK_ADVICE}
              </p>
            )}
          </div>
        </div>
        {session && (
          <div className="text-right shrink-0">
            <div className="text-4xl font-extrabold text-white leading-none">{riskScore}</div>
            <div className="text-[11px] text-slate-400 mt-1">risk out of 100</div>
            <div className="text-[11px] text-slate-300">{band.label}</div>
          </div>
        )}
      </div>

      {session && (
        <div className="flex flex-wrap gap-2 mt-4 text-xs">
          <Fact
            label="Caller's line"
            value={line === "EXTERNAL" ? "Outside the organisation" : line === "INTERNAL" ? "Internal extension" : "Unknown (simulated call)"}
          />
          <Fact
            label="Caller's voice"
            bad={voice?.suspicious}
            value={!voice ? "Not checked yet" : voice.suspicious ? "Sounds computer-generated" : voice.callScore === null ? "Checking…" : "Sounds human"}
          />
          <Fact
            label="Employee warned"
            bad={warnings.length > 0}
            value={warnings.length === 0 ? "No" : `Yes, ${warnings.length} spoken warning${warnings.length > 1 ? "s" : ""}`}
          />
          {analystDown ? (
            <Fact label="AI analyst" bad value="Not running (rules only)" />
          ) : (
            !ended && <Fact label="AI analyst" value={analystPending > 0 ? "Reading the latest sentence…" : analyst ? "Up to date" : "Waiting"} />
          )}
        </div>
      )}
    </div>
  );
};

const Fact: React.FC<{ label: string; value: string; bad?: boolean }> = ({ label, value, bad }) => (
  <span className={`px-3 py-1.5 rounded-lg border ${bad ? "border-red-700 bg-red-950/50 text-red-100" : "border-slate-700 bg-slate-950/60 text-slate-200"}`}>
    <span className="text-slate-400">{label}: </span>
    {value}
  </span>
);
