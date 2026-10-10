"use client";

import Link from "next/link";
import React, { useEffect, useState } from "react";
import { signalRService } from "@/services/signalrService";
import { CallSession, TranscriptSegment, TranscriptPartial, TacticEvidence, RiskContributor, AlertEvent, AnalystUpdate, VoiceUpdate, EmployeeWarning } from "@/types";
import { RiskGauge } from "./RiskGauge";
import { LiveTranscript } from "./LiveTranscript";
import { AttackTimeline } from "./AttackTimeline";
import { TacticList } from "./TacticList";
import { AlertPanel } from "./AlertPanel";
import { CallControls } from "./CallControls";
import { AnalystPanel } from "./AnalystPanel";
import { VoicePanel } from "./VoicePanel";
import { EmployeeWarnings } from "./EmployeeWarnings";

export const Dashboard: React.FC = () => {
  const [session, setSession] = useState<CallSession | null>(null);
  const [transcripts, setTranscripts] = useState<TranscriptSegment[]>([]);
  const [partial, setPartial] = useState<TranscriptPartial | null>(null);
  const [tactics, setTactics] = useState<TacticEvidence[]>([]);
  const [riskScore, setRiskScore] = useState<number>(0);
  const [severity, setSeverity] = useState<string>("Low");
  const [stage, setStage] = useState<string>("Normal");
  const [contributors, setContributors] = useState<RiskContributor[]>([]);
  const [alerts, setAlerts] = useState<AlertEvent[]>([]);
  const [analyst, setAnalyst] = useState<AnalystUpdate | null>(null);
  // turns sent to the analyst that it has not answered yet (it answers in the background)
  const [analystPending, setAnalystPending] = useState<number>(0);
  const [voice, setVoice] = useState<VoiceUpdate | null>(null);
  const [warnings, setWarnings] = useState<EmployeeWarning[]>([]);

  useEffect(() => {
    signalRService.startConnection(
      (data) => {
        setSession(data);
        setTranscripts([]);
        setPartial(null);
        setTactics([]);
        setRiskScore(0);
        setSeverity("Low");
        setStage("Normal");
        setContributors([]);
        setAlerts([]);
        setAnalyst(null);
        setAnalystPending(0);
        setVoice(null);
        setWarnings([]);
      },
      (data) => {
        setPartial(null);
        setTranscripts((prev) => [...prev, data]);
        setAnalystPending((n) => n + 1);
      },
      (data) => setTactics((prev) => [...prev, data]),
      (data) => setStage(data.newStage),
      (data) => {
        setRiskScore(data.riskScore);
        setSeverity(data.severity);
        setContributors(data.topContributors || []);
      },
      (data) => setAlerts((prev) => [data, ...prev]),
      () => {
        setSession(null);
        setAnalystPending(0);
      },
      (data) => {
        setAnalyst(data);
        setAnalystPending((n) => Math.max(0, n - 1));
      },
      (data: TranscriptPartial) => setPartial(data.text ? data : null),
      (data: VoiceUpdate) => setVoice(data),
      (data: EmployeeWarning) => setWarnings((prev) => [...prev, data])
    );

    return () => signalRService.stopConnection();
  }, []);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 p-8 font-sans">
      {/* Top Bar */}
      <header className="flex justify-between items-center pb-6 mb-6 border-b border-slate-800">
        <div>
          <h1 className="text-xl font-extrabold tracking-tight text-white flex items-center gap-3">
            <span className="w-3 h-3 rounded-full bg-red-500 animate-pulse"></span>
            ECFD — Live Conversational Fraud Defense
          </h1>
          <p className="text-xs text-slate-400 mt-1 font-mono">
            Active Call Session: {session ? session.externalCallId : "No Active Call"} | Caller: {session?.caller || "Idle"}
          </p>
        </div>
        <div className="flex gap-3 items-center">
          <Link href="/tests" className="px-3 py-1 bg-sky-700 hover:bg-sky-600 rounded text-xs font-bold text-white">
            Test Lab →
          </Link>
          <span className="px-3 py-1 bg-slate-900 border border-slate-800 rounded text-xs font-mono text-emerald-400">
            System: HEALTHY
          </span>
        </div>
      </header>

      <CallControls callActive={session !== null} />

      {/* Main Grid */}
      <AlertPanel alerts={alerts} />

      <EmployeeWarnings warnings={warnings} />

      <AnalystPanel update={analyst} pending={analystPending} />

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6 mb-6">
        <div className="lg:col-span-1">
          <RiskGauge score={riskScore} severity={severity} stage={stage} contributors={contributors} />
        </div>
        <div className="lg:col-span-2">
          <LiveTranscript segments={transcripts} partial={partial} />
        </div>
        <div className="lg:col-span-1">
          <VoicePanel voice={voice} callActive={session !== null} />
          <TacticList tactics={tactics} />
        </div>
      </div>

      <AttackTimeline currentStage={stage} />
    </div>
  );
};
