export interface CallSession {
  sessionId: string;
  externalCallId: string;
  caller: string;
  callee: string;
  startedAt: string;
}

export interface TranscriptSegment {
  sessionId: string;
  segmentId: string;
  text: string;
  confidence: number;
  isFinal: boolean;
  startMs: number;
  endMs: number;
}

export interface TacticEvidence {
  sessionId: string;
  evidenceId: string;
  tactic: string;
  confidence: number;
  timestamp: string;
}

export interface RiskContributor {
  type: string;
  contribution: number;
}

export interface RiskUpdate {
  sessionId: string;
  riskScore: number;
  severity: "Low" | "Medium" | "High" | "Critical";
  topContributors: RiskContributor[];
  stage: string;
}

export interface AlertEvent {
  sessionId: string;
  alertId: string;
  severity: string;
  title: string;
  description: string;
  createdAt: string;
}
