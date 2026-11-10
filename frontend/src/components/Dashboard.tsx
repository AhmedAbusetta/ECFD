"use client";

import Link from "next/link";
import React, { useEffect, useRef, useState } from "react";
import { signalRService } from "@/services/signalrService";
import { CallSession, TranscriptSegment, TranscriptPartial, TacticEvidence, RiskContributor, AnalystUpdate, VoiceUpdate, EmployeeWarning } from "@/types";
import { RiskGauge } from "./RiskGauge";
import { LiveTranscript } from "./LiveTranscript";
import { VerdictBanner } from "./VerdictBanner";
import { AttackTimeline } from "./AttackTimeline";
import { TacticList } from "./TacticList";
import { CallControls } from "./CallControls";
import { AnalystPanel } from "./AnalystPanel";
import { VoicePanel } from "./VoicePanel";
import { EmployeeWarnings } from "./EmployeeWarnings";

// A turn the analyst has not answered for this long is given up on (it answers in the background and can fail).
const ANALYST_WAIT_MS = 20000;

export const Dashboard: React.FC = () => {
  const [session, setSession] = useState<CallSession | null>(null);
  const [transcripts, setTranscripts] = useState<TranscriptSegment[]>([]);
  // the sentence each side is speaking right now; one slot per speaker so they never overwrite each other
  const [partials, setPartials] = useState<Record<string, TranscriptPartial>>({});
  // every hard signal the analyst reported on this call (it only sends the newest turn's)
  const [hardSignals, setHardSignals] = useState<string[]>([]);
  // the call the panels still describe after it ended, so its verdict stays readable
  const [shownSession, setShownSession] = useState<CallSession | null>(null);
  const [tactics, setTactics] = useState<TacticEvidence[]>([]);
  const [riskScore, setRiskScore] = useState<number>(0);
  const [severity, setSeverity] = useState<string>("Low");
  const [stage, setStage] = useState<string>("Normal");
  const [contributors, setContributors] = useState<RiskContributor[]>([]);
  const [analyst, setAnalyst] = useState<AnalystUpdate | null>(null);
  // turns sent to the analyst that it has not answered yet
  const [analystPending, setAnalystPending] = useState<number>(0);
  const [voice, setVoice] = useState<VoiceUpdate | null>(null);
  const [warnings, setWarnings] = useState<EmployeeWarning[]>([]);
  const [testOpen, setTestOpen] = useState(false);

  // The call the page is showing. Updates from any other (older) call are ignored, so a late analyst answer
  // from the previous call can never change the current one.
  const activeCall = useRef<string | null>(null);
  const pendingTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const mine = (data: { sessionId?: string }) => {
      if (!data?.sessionId) return true;
      if (activeCall.current === null) {
        activeCall.current = data.sessionId; // the page was opened in the middle of a call
        return true;
      }
      return data.sessionId === activeCall.current;
    };
    const clearPending = () => {
      if (pendingTimer.current) clearTimeout(pendingTimer.current);
      pendingTimer.current = null;
      setAnalystPending(0);
    };

    signalRService.startConnection(
      (data) => {
        activeCall.current = data.sessionId;
        setSession(data);
        setShownSession(data);
        setTranscripts([]);
        setPartials({});
        setHardSignals([]);
        setTactics([]);
        setRiskScore(0);
        setSeverity("Low");
        setStage("Normal");
        setContributors([]);
        setAnalyst(null);
        clearPending();
        setVoice(null);
        setWarnings([]);
      },
      (data) => {
        if (!mine(data)) return;
        // the finished sentence replaces that speaker's live words only
        setPartials((prev) => {
          const next = { ...prev };
          delete next[data.speaker ?? "CALLER"];
          return next;
        });
        setTranscripts((prev) => [...prev, data]);
        setAnalystPending((n) => n + 1);
        if (pendingTimer.current) clearTimeout(pendingTimer.current);
        pendingTimer.current = setTimeout(clearPending, ANALYST_WAIT_MS);
      },
      (data) => mine(data) && setTactics((prev) => [...prev, data]),
      (data) => mine(data) && setStage(data.newStage),
      (data) => {
        if (!mine(data)) return;
        setRiskScore(data.riskScore);
        setSeverity(data.severity);
        setContributors(data.topContributors || []);
      },
      () => undefined, // alerts are shown through the verdict banner
      (data) => {
        if (!mine(data)) return;
        setSession(null);
        clearPending();
      },
      (data) => {
        if (!mine(data)) return;
        setAnalyst(data);
        if (data.hardSignals?.length) {
          setHardSignals((prev) => [...prev, ...data.hardSignals.filter((s: string) => !prev.includes(s))]);
        }
        setAnalystPending((n) => Math.max(0, n - 1));
      },
      (data: TranscriptPartial) => {
        if (!mine(data)) return;
        setPartials((prev) => {
          const next = { ...prev };
          if (data.text) next[data.speaker] = data;
          // an empty text clears the live words, but only of the sentence it belongs to
          else if (next[data.speaker]?.utteranceId === data.utteranceId) delete next[data.speaker];
          return next;
        });
      },
      (data: VoiceUpdate) => mine(data) && setVoice(data),
      (data: EmployeeWarning) => mine(data) && setWarnings((prev) => [...prev, data])
    );

    return () => {
      signalRService.stopConnection();
      if (pendingTimer.current) clearTimeout(pendingTimer.current);
    };
  }, []);

  // calls from the phone system start with "PBX-"; the test-call controls only work for simulated calls
  const realCall = session?.externalCallId?.startsWith("PBX-") ?? false;
  const calls = session ? "Call in progress" : "No active call";

  return (
    <div className="min-h-screen">
      <header className="border-b border-line">
        <div className="mx-auto flex max-w-[1100px] items-center justify-between px-8 py-4">
          <div className="flex items-baseline gap-5">
            <h1 className="text-[15px] font-semibold tracking-tight">ECFD</h1>
            <p className="text-sm text-muted">
              {calls}
              {session && <span className="text-faint"> · {session.caller} to {session.callee}</span>}
            </p>
          </div>
          <nav className="flex items-center gap-6 text-sm">
            {!realCall && (
              <button onClick={() => setTestOpen((v) => !v)} className="text-muted hover:text-ink">
                {testOpen ? "Hide test call" : "Test call"}
              </button>
            )}
            <Link href="/tests" className="text-muted hover:text-ink">Test lab</Link>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-[1100px] px-8 pb-16 pt-8">
        {!realCall && testOpen && (
          <div className="mb-8 border-b border-line pb-6">
            <CallControls callActive={session !== null} />
          </div>
        )}

        <VerdictBanner
          session={shownSession}
          ended={session === null}
          riskScore={riskScore}
          analyst={analyst}
          analystPending={analystPending}
          tactics={tactics}
          hardSignals={hardSignals}
          voice={voice}
          warnings={warnings}
        />

        <div className="mt-9 grid grid-cols-1 gap-x-10 border-t border-line pt-6 lg:grid-cols-[minmax(0,1fr)_320px]">
          <LiveTranscript segments={transcripts} partials={Object.values(partials)} />
          <div className="mt-8 divide-y divide-line lg:mt-0 lg:border-l lg:border-line lg:pl-10">
            <TacticList tactics={tactics} />
            <RiskGauge score={riskScore} severity={severity} stage={stage} contributors={contributors} />
            <VoicePanel voice={voice} callActive={session !== null} />
            <EmployeeWarnings warnings={warnings} />
          </div>
        </div>

        <div className="mt-9 space-y-6">
          <AttackTimeline currentStage={stage} />
          <AnalystPanel update={analyst} pending={analystPending} />
        </div>
      </main>
    </div>
  );
};
