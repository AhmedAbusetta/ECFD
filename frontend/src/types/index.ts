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
  speaker?: "CALLER" | "EMPLOYEE";
  confidence: number;
  isFinal: boolean;
  startMs: number;
  endMs: number;
}

/** Live (not yet final) text of the utterance being spoken; replaced by the final segment. */
export interface TranscriptPartial {
  sessionId: string;
  utteranceId: string;
  speaker: "CALLER" | "EMPLOYEE";
  text: string;
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

export interface AnalystEvidence {
  turn: number;
  speaker: string;
  label: string;
  quote: string;
}

/** The AI call analyst's view of the whole call after one turn (ADR-0005). */
export interface AnalystUpdate {
  sessionId: string;
  turn: number;
  analyzed: boolean;
  risk: number;
  level: "NONE" | "WATCH" | "WARNING" | "ALERT";
  llmRisk: number | null;
  floor: number;
  stage: string;
  trend: string;
  employeeState: string;
  callerGoal: string;
  strategy: string;
  nextLikelyMove: string;
  alertAr: string;
  policyViolations: string[];
  evidence: AnalystEvidence[];
  hardSignals: string[];
  reasons: string[];
  model: string;
  latencyMs: number;
  error: string | null;
}

/** Voice anti-spoofing for the caller: per sentence and combined over the call (null until enough speech). */
export interface VoiceUpdate {
  sessionId: string;
  sentenceScore: number;
  callScore: number | null;
  sentences: number;
  suspicious: boolean;
  model: string;
}

/** A spoken warning played into the employee's ear during the call. */
export interface EmployeeWarning {
  sessionId: string;
  kind: string;
  at: string;
}
