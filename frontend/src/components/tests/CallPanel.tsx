"use client";

import React, { useEffect, useRef, useState } from "react";
import { Badge, Card, ErrorBox } from "./ui";

// Simulated call: the browser is the caller's phone. Mic audio is resampled to 8 kHz 16-bit PCM and
// streamed to the lab (ml/lab/call.py), which runs Speechmatics -> turns -> brain -> stage -> score
// and streams every event back here.

const STAGES = ["normal", "identity", "pressure", "sensitive", "extraction"] as const;
const LEVEL_STYLE: Record<string, { box: string; text: string; label: string }> = {
  NONE: { box: "bg-slate-900 border-slate-800", text: "text-slate-400", label: "No warning" },
  WATCH: { box: "bg-yellow-950/60 border-yellow-600", text: "text-yellow-300", label: "WATCH — something to keep an eye on" },
  WARNING: { box: "bg-orange-950/70 border-orange-500", text: "text-orange-300", label: "WARNING — this call shows several fraud tactics" },
  ALERT: { box: "bg-red-950/80 border-red-500 animate-pulse", text: "text-red-200", label: "ALERT — the caller is asking for something a real bank or IT never asks for" },
};

interface TurnView {
  n: number;
  text: string;
  reason: string;
  result?: {
    tactics?: { label: string; confidence: string; quote: string }[];
    dropped?: { label: string; quote: string; reason: string }[];
    needs_more_context?: boolean;
    stage?: string;
    score?: number;
    level?: string;
    level_changed?: boolean;
    latency_ms?: number;
    brain_ms?: number;
    contributors?: [string, number][];
    error?: string;
  };
}

interface Summary {
  saved: string;
  turns: number;
  audio_seconds: number;
  level: string;
  score: number;
  median_latency_ms: number | null;
  max_latency_ms: number | null;
}

const WORKLET = `
class EcfdCapture extends AudioWorkletProcessor {
  process(inputs) {
    const ch = inputs[0] && inputs[0][0];
    if (ch) this.port.postMessage(ch.slice(0));
    return true;
  }
}
registerProcessor("ecfd-capture", EcfdCapture);
`;

/** Box-filter downsampler from the AudioContext rate to 8 kHz 16-bit PCM. */
class Downsampler {
  private buf: number[] = [];
  private pos = 0;
  constructor(private ratio: number) {}
  push(input: Float32Array): Int16Array {
    for (let i = 0; i < input.length; i++) this.buf.push(input[i]);
    const out: number[] = [];
    while (this.pos + this.ratio <= this.buf.length) {
      const from = Math.floor(this.pos);
      const to = Math.floor(this.pos + this.ratio);
      let sum = 0;
      for (let i = from; i < to; i++) sum += this.buf[i];
      const v = Math.max(-1, Math.min(1, sum / Math.max(1, to - from)));
      out.push(Math.round(v * 32767));
      this.pos += this.ratio;
    }
    const drop = Math.floor(this.pos);
    this.buf = this.buf.slice(drop);
    this.pos -= drop;
    return Int16Array.from(out);
  }
}

export const CallPanel: React.FC = () => {
  const [state, setState] = useState<"idle" | "connecting" | "live" | "ending" | "ended">("idle");
  const [error, setError] = useState("");
  const [live, setLive] = useState({ final: "", partial: "" });
  const [turns, setTurns] = useState<TurnView[]>([]);
  const [summary, setSummary] = useState<Summary | null>(null);
  const [seconds, setSeconds] = useState(0);

  const ws = useRef<WebSocket | null>(null);
  const audio = useRef<{ ctx: AudioContext; stream: MediaStream } | null>(null);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  const latest = [...turns].reverse().find((t) => t.result && !t.result.error)?.result;
  const level = latest?.level ?? "NONE";
  const stage = latest?.stage ?? "normal";
  const style = LEVEL_STYLE[level] ?? LEVEL_STYLE.NONE;

  const stopAudio = () => {
    audio.current?.stream.getTracks().forEach((t) => t.stop());
    audio.current?.ctx.close().catch(() => undefined);
    audio.current = null;
    if (timer.current) clearInterval(timer.current);
  };

  useEffect(() => () => {
    stopAudio();
    ws.current?.close();
  }, []);

  const start = async () => {
    setError("");
    setTurns([]);
    setSummary(null);
    setLive({ final: "", partial: "" });
    setSeconds(0);
    setState("connecting");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      });
      const ctx = new AudioContext();
      await ctx.audioWorklet.addModule(URL.createObjectURL(new Blob([WORKLET], { type: "application/javascript" })));
      const source = ctx.createMediaStreamSource(stream);
      const node = new AudioWorkletNode(ctx, "ecfd-capture");
      source.connect(node);
      audio.current = { ctx, stream };

      const socket = new WebSocket(`${location.protocol === "https:" ? "wss" : "ws"}://${location.hostname}:8010/call/ws`);
      socket.binaryType = "arraybuffer";
      ws.current = socket;

      const down = new Downsampler(ctx.sampleRate / 8000);
      let pending: number[] = [];
      node.port.onmessage = (e: MessageEvent<Float32Array>) => {
        const pcm = down.push(e.data);
        for (let i = 0; i < pcm.length; i++) pending.push(pcm[i]);
        if (pending.length >= 800 && socket.readyState === WebSocket.OPEN) {
          socket.send(Int16Array.from(pending).buffer); // 100 ms of 8 kHz audio
          pending = [];
        }
      };

      socket.onmessage = (msg) => {
        const e = JSON.parse(msg.data);
        if (e.type === "status" && e.state === "live") {
          setState("live");
          timer.current = setInterval(() => setSeconds((s) => s + 1), 1000);
        } else if (e.type === "live") {
          setLive({ final: e.final, partial: e.partial });
        } else if (e.type === "turn") {
          setLive({ final: "", partial: "" });
          setTurns((t) => [...t, { n: e.n, text: e.text, reason: e.reason }]);
        } else if (e.type === "result") {
          setTurns((t) => t.map((x) => (x.n === e.n ? { ...x, result: e } : x)));
        } else if (e.type === "ended") {
          setSummary(e);
          setState("ended");
        } else if (e.type === "error") {
          setError(e.msg);
        }
      };
      socket.onclose = () => {
        stopAudio();
        setState((s) => (s === "ended" ? s : "idle"));
      };
      socket.onerror = () => setError("Cannot reach the lab service on port 8010. Start it from the preview menu (lab).");
    } catch (e) {
      stopAudio();
      setState("idle");
      setError(`Microphone unavailable: ${(e as Error).message}`);
    }
  };

  const hangUp = () => {
    setState("ending");
    stopAudio();
    ws.current?.send(JSON.stringify({ type: "end" }));
  };

  const inCall = state === "live" || state === "connecting" || state === "ending";

  return (
    <div className="space-y-6">
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <div className="text-sm text-slate-300">
              You are the <b>caller</b>. Talk naturally in Egyptian Arabic and pause between sentences like a real call.
            </div>
            <div className="text-[11px] text-slate-500 mt-1">
              Mic → 8 kHz phone quality → Speechmatics live → turns (0.6 s silence) → brain → stage → score. One call at a time (free plan).
            </div>
          </div>
          <div className="flex items-center gap-3">
            {inCall && <span className="font-mono text-sm text-emerald-400">● {Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, "0")}</span>}
            {!inCall ? (
              <button className="btn-primary !px-5 !py-2.5 !text-sm" onClick={start}>📞 Start call</button>
            ) : (
              <button className="btn-danger !px-5 !py-2.5 !text-sm" disabled={state !== "live"} onClick={hangUp}>
                {state === "connecting" ? "Connecting…" : state === "ending" ? "Finishing…" : "Hang up"}
              </button>
            )}
          </div>
        </div>
      </Card>

      {error && <ErrorBox message={error} />}

      <div className={`rounded-xl border-2 p-5 transition-colors ${style.box}`}>
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <div className={`text-2xl font-extrabold ${style.text}`}>{level === "NONE" ? "No warning" : level}</div>
            <div className={`text-sm ${style.text} opacity-90`}>{style.label}</div>
          </div>
          <div className="text-right">
            <div className="text-[11px] uppercase tracking-wider text-slate-400">Risk score</div>
            <div className="text-4xl font-extrabold text-white">{latest?.score ?? 0}<span className="text-base text-slate-400">/100</span></div>
          </div>
        </div>
        <div className="grid grid-cols-5 gap-2 mt-4">
          {STAGES.map((s, i) => {
            const reached = STAGES.indexOf(stage as (typeof STAGES)[number]) >= i;
            return (
              <div key={s} className={`text-center text-[11px] rounded py-1.5 ${reached ? (i === 4 ? "bg-red-600 text-white" : "bg-sky-700 text-white") : "bg-slate-800 text-slate-500"}`}>
                {s}
              </div>
            );
          })}
        </div>
        {latest?.contributors && latest.contributors.length > 0 && (
          <div className="flex flex-wrap gap-2 mt-3">
            {latest.contributors.map(([label, pts]) => (
              <span key={label} className="text-[11px] font-mono bg-black/30 rounded px-2 py-0.5 text-slate-200">
                {label} +{pts}
              </span>
            ))}
          </div>
        )}
      </div>

      <Card title="Live transcript (what Speechmatics hears now)">
        <p dir="rtl" className="text-lg min-h-[2rem] leading-relaxed">
          <span className="text-slate-100">{live.final}</span> <span className="text-slate-500 italic">{live.partial}</span>
          {!live.final && !live.partial && <span className="text-slate-600 text-sm">{state === "live" ? "Listening…" : "—"}</span>}
        </p>
      </Card>

      <Card title={`Turns (${turns.length})`}>
        {turns.length === 0 ? (
          <p className="text-sm text-slate-500">Each sentence becomes a turn after 0.6 s of silence.</p>
        ) : (
          <div className="space-y-3">
            {turns.map((t) => (
              <div key={t.n} className="border border-slate-800 rounded-lg p-3">
                <div className="flex justify-between text-[11px] text-slate-500">
                  <span>Turn {t.n} · closed by {t.reason.replace(/_/g, " ")}</span>
                  {t.result?.latency_ms != null && (
                    <span className={t.result.latency_ms <= 5000 ? "text-emerald-400" : "text-red-400"}>
                      silence → result {(t.result.latency_ms / 1000).toFixed(1)} s (brain {((t.result.brain_ms ?? 0) / 1000).toFixed(1)} s)
                    </span>
                  )}
                </div>
                <p dir="rtl" className="text-base text-slate-100 my-1.5">{t.text}</p>
                {!t.result ? (
                  <span className="text-xs text-sky-400">asking the brain…</span>
                ) : t.result.error ? (
                  <span className="text-xs text-red-400">{t.result.error}</span>
                ) : (
                  <div className="flex flex-wrap items-center gap-2">
                    {t.result.tactics?.length ? (
                      t.result.tactics.map((x) => (
                        <span key={x.label} className="text-xs bg-amber-500/15 border border-amber-500/40 text-amber-200 rounded px-2 py-0.5">
                          {x.label} <span className="text-amber-400/70">{x.confidence}</span> · <span dir="rtl">«{x.quote}»</span>
                        </span>
                      ))
                    ) : (
                      <span className="text-xs text-slate-500">no tactics</span>
                    )}
                    {t.result.level_changed && <Badge tone={t.result.level === "ALERT" ? "bad" : "warn"}>→ {t.result.level}</Badge>}
                    {t.result.needs_more_context && <span className="text-[11px] text-slate-400">needs more context</span>}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </Card>

      {summary && (
        <Card title="Call summary">
          <div className="text-sm text-slate-300 space-y-1">
            <div>
              {summary.turns} turns · {summary.audio_seconds} s of audio · final level <b>{summary.level}</b> · score {summary.score}
            </div>
            {summary.median_latency_ms != null && (
              <div>
                Delay from caller silence to result: median <b>{(summary.median_latency_ms / 1000).toFixed(1)} s</b>, worst{" "}
                {((summary.max_latency_ms ?? 0) / 1000).toFixed(1)} s <span className="text-slate-500">(Test 2 line: median under 5 s)</span>
              </div>
            )}
            <div className="text-[11px] text-slate-500">Saved as ml/lab/calls/{summary.saved} (git-ignored)</div>
          </div>
        </Card>
      )}
    </div>
  );
};
