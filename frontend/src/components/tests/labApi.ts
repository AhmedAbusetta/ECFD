// Client for the ECFD Lab service (proxied at /lab by next.config.mjs).

export interface Tactic {
  label: string;
  confidence: "low" | "medium" | "high";
  quote: string;
  reason?: string;
}

export interface Test0Item {
  id: string;
  text: string;
  context?: string[];
  expected: string[];
  why: string;
}

export interface Test0Result {
  id: string;
  ok: boolean;
  accepted: Tactic[];
  dropped: Tactic[];
  missing: string[];
  extra: string[];
  needs_more_context: boolean;
  latency_ms: number | null;
}

export interface Test0Summary {
  answered: number;
  total: number;
  correct: number;
  invalid_quotes_kept: number;
  median_ms: number | null;
  max_ms: number | null;
  passed: boolean;
  pass_line: number;
}

export interface Test0Data {
  model: string;
  prompt_version: string;
  items: Test0Item[];
  results: Record<string, Test0Result>;
  summary: Test0Summary;
}

export interface ClassifyResult {
  model: string;
  latency_ms: number;
  needs_more_context: boolean;
  tactics: Tactic[];
  dropped: Tactic[];
}

export interface Clip {
  id: string;
  kind: "fraud" | "normal";
  noise?: boolean;
  text: string;
  keywords: string[];
}

export interface Test1Row {
  clip_id: string;
  speaker: string;
  file: string;
  transcribed: boolean;
  transcript?: string;
  hits?: string[];
  misses?: string[];
  wer?: number;
  engine_ms?: number;
}

export interface Test1Summary {
  found: number;
  expected: number;
  recall: number;
  median_wer: number | null;
  per_word: Record<string, [number, number]>;
  per_speaker: Record<string, [number, number]>;
  pass_line: number;
  passed: boolean;
}

export interface Test1Data {
  engine: string;
  config: string;
  clips: Clip[];
  keyword_groups: Record<string, string[]>;
  speakers: string[];
  rows: Test1Row[];
  summary: Test1Summary;
}

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/lab${path}`, init);
  if (!res.ok) {
    let detail = await res.text();
    try {
      detail = JSON.parse(detail).detail ?? detail;
    } catch {
      /* plain text */
    }
    throw new Error(`${res.status}: ${detail}`);
  }
  return res.json();
}

export const labApi = {
  test0: () => call<Test0Data>("/test0"),
  runTest0: (id: string, fresh: boolean) => call<Test0Result>(`/test0/run/${id}?fresh=${fresh}`, { method: "POST" }),
  classify: (text: string, context: string[]) =>
    call<ClassifyResult>("/brain/classify", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text, context }),
    }),
  test1: (speakers?: string[]) =>
    call<Test1Data>(`/test1${speakers && speakers.length ? `?speakers=${encodeURIComponent(speakers.join(","))}` : ""}`),
  record: (speaker: string, clipId: string, audio: Blob) => {
    const form = new FormData();
    const ext = audio.type.includes("ogg") ? "ogg" : "webm";
    form.append("speaker", speaker);
    form.append("clip_id", clipId);
    form.append("audio", audio, `T1_${clipId}_${speaker}.${ext}`);
    return call<Test1Row>("/test1/record", { method: "POST", body: form });
  },
  audioUrl: (file: string) => `/lab/test1/audio/${encodeURIComponent(file)}`,
};
