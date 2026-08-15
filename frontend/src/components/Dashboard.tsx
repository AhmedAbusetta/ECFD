import React, { useEffect, useState } from "react";
import { signalRService } from "@/services/signalrService";
import { CallSession, TranscriptSegment, TacticEvidence, RiskContributor, AlertEvent } from "@/types";
import { RiskGauge } from "./RiskGauge";
import { LiveTranscript } from "./LiveTranscript";
import { AttackTimeline } from "./AttackTimeline";
import { TacticList } from "./TacticList";
import { AlertPanel } from "./AlertPanel";

export const Dashboard: React.FC = () => {
  const [session, setSession] = useState<CallSession | null>(null);
  const [transcripts, setTranscripts] = useState<TranscriptSegment[]>([]);
  const [tactics, setTactics] = useState<TacticEvidence[]>([]);
  const [riskScore, setRiskScore] = useState<number>(0);
  const [severity, setSeverity] = useState<string>("Low");
  const [stage, setStage] = useState<string>("Normal");
  const [contributors, setContributors] = useState<RiskContributor[]>([]);
  const [alerts, setAlerts] = useState<AlertEvent[]>([]);

  useEffect(() => {
    signalRService.startConnection(
      (data) => setSession(data),
      (data) => setTranscripts((prev) => [...prev, data]),
      (data) => setTactics((prev) => [...prev, data]),
      (data) => setStage(data.newStage),
      (data) => {
        setRiskScore(data.riskScore);
        setSeverity(data.severity);
        setContributors(data.topContributors || []);
      },
      (data) => setAlerts((prev) => [data, ...prev]),
      () => setSession(null)
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
        <div className="flex gap-3">
          <span className="px-3 py-1 bg-slate-900 border border-slate-800 rounded text-xs font-mono text-emerald-400">
            System: HEALTHY
          </span>
        </div>
      </header>

      {/* Main Grid */}
      <AlertPanel alerts={alerts} />

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6 mb-6">
        <div className="lg:col-span-1">
          <RiskGauge score={riskScore} severity={severity} stage={stage} contributors={contributors} />
        </div>
        <div className="lg:col-span-2">
          <LiveTranscript segments={transcripts} />
        </div>
        <div className="lg:col-span-1">
          <TacticList tactics={tactics} />
        </div>
      </div>

      <AttackTimeline currentStage={stage} />
    </div>
  );
};
