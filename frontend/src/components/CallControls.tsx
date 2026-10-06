"use client";

import React, { useRef, useState } from "react";

const BACKEND_URL = process.env.NEXT_PUBLIC_BACKEND_URL || "http://localhost:5000";

type Status = "idle" | "in-call" | "recording" | "processing";
type Speaker = "CALLER" | "EMPLOYEE";

/**
 * Simulated-call controls for Stage 1 testing: start a call, then record one utterance at a
 * time from the microphone. Each recording is sent to /api/demo/audio (ASR -> NLP -> FSM -> risk)
 * and the results arrive on the dashboard through SignalR like any other event.
 */
export const CallControls: React.FC<{ callActive: boolean }> = ({ callActive }) => {
  const [status, setStatus] = useState<Status>("idle");
  const [message, setMessage] = useState<string>("");
  // In a real call each side arrives on its own phone leg; here you pick who is talking.
  const [speaker, setSpeaker] = useState<Speaker>("CALLER");
  const [typed, setTyped] = useState<string>("");
  const recorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  // Live transcript: the audio recorded so far is re-sent every second while speaking.
  const utteranceIdRef = useRef<string>("");
  const partialTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const partialInFlightRef = useRef<boolean>(false);

  const post = async (path: string, init?: RequestInit) => {
    const res = await fetch(`${BACKEND_URL}${path}`, { method: "POST", ...init });
    if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`);
    return res.json();
  };

  const startCall = async () => {
    try {
      await post("/api/demo/start-call", {
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ caller: "1002 (Mic)", callee: "1001 (Employee)" }),
      });
      setStatus("in-call");
      setMessage("Call started. Press Record and speak one sentence.");
    } catch (e) {
      setMessage(`Could not start call: ${(e as Error).message}`);
    }
  };

  const endCall = async () => {
    stopPartials();
    recorderRef.current?.stop();
    await post("/api/demo/end-call").catch(() => undefined);
    setStatus("idle");
    setMessage("");
  };

  const startRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const recorder = new MediaRecorder(stream);
      chunksRef.current = [];
      recorder.ondataavailable = (e) => e.data.size > 0 && chunksRef.current.push(e.data);
      recorder.onstop = async () => {
        stopPartials();
        stream.getTracks().forEach((t) => t.stop());
        await sendRecording(new Blob(chunksRef.current, { type: recorder.mimeType }));
      };
      utteranceIdRef.current = crypto.randomUUID();
      recorder.start(250); // small chunks so the live transcript has fresh audio every tick
      const utteranceId = utteranceIdRef.current;
      partialTimerRef.current = setInterval(() => sendPartial(utteranceId, recorder.mimeType), 1000);
      recorderRef.current = recorder;
      setStatus("recording");
      setMessage("Recording… press Stop when you finish the sentence.");
    } catch (e) {
      setMessage(`Microphone unavailable: ${(e as Error).message}`);
    }
  };

  const stopPartials = () => {
    if (partialTimerRef.current) clearInterval(partialTimerRef.current);
    partialTimerRef.current = null;
  };

  /** One live-transcript request at a time; a slow answer just means the next tick is skipped. */
  const sendPartial = async (utteranceId: string, mimeType: string) => {
    if (partialInFlightRef.current || chunksRef.current.length === 0) return;
    partialInFlightRef.current = true;
    try {
      const ext = mimeType.includes("ogg") ? "ogg" : mimeType.includes("mp4") ? "mp4" : "webm";
      const form = new FormData();
      form.append("audio", new Blob(chunksRef.current, { type: mimeType }), `partial.${ext}`);
      form.append("speaker", speaker);
      form.append("utteranceId", utteranceId);
      await post("/api/demo/audio-partial", { body: form });
    } catch {
      // live text is best-effort; the final transcription still runs when recording stops
    } finally {
      partialInFlightRef.current = false;
    }
  };

  const stopRecording = () => {
    recorderRef.current?.stop();
    recorderRef.current = null;
    setStatus("processing");
    setMessage("Transcribing…");
  };

  const sendRecording = async (blob: Blob) => {
    const ext = blob.type.includes("ogg") ? "ogg" : blob.type.includes("mp4") ? "mp4" : "webm";
    const form = new FormData();
    form.append("audio", blob, `utterance.${ext}`);
    form.append("speaker", speaker);
    form.append("utteranceId", utteranceIdRef.current);
    const started = performance.now();
    try {
      const result = await post("/api/demo/audio", { body: form });
      const seconds = ((performance.now() - started) / 1000).toFixed(1);
      setMessage(result.text ? `Heard (${seconds}s): “${result.text}”` : "No speech detected — try again.");
    } catch (e) {
      setMessage(`Audio processing failed: ${(e as Error).message}`);
    }
    setStatus("in-call");
  };

  /** Typed turn: skips ASR, so the analyst can be tested without a microphone. */
  const sendTyped = async () => {
    const text = typed.trim();
    if (!text) return;
    setStatus("processing");
    try {
      await post("/api/demo/utterance", {
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text, speaker }),
      });
      setTyped("");
      setMessage(`Sent as ${speaker.toLowerCase()}.`);
    } catch (e) {
      setMessage(`Sending failed: ${(e as Error).message}`);
    }
    setStatus("in-call");
  };

  const inCall = status !== "idle" || callActive;
  const btn = "px-4 py-2 rounded-lg text-xs font-bold uppercase tracking-wider transition disabled:opacity-40 disabled:cursor-not-allowed";

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 mb-6 flex flex-wrap items-center gap-3">
      {!inCall ? (
        <button className={`${btn} bg-sky-600 hover:bg-sky-500 text-white`} onClick={startCall}>
          Start simulated call
        </button>
      ) : (
        <>
          {status === "recording" ? (
            <button className={`${btn} bg-red-600 hover:bg-red-500 text-white animate-pulse`} onClick={stopRecording}>
              ■ Stop
            </button>
          ) : (
            <button
              className={`${btn} bg-emerald-600 hover:bg-emerald-500 text-white`}
              onClick={startRecording}
              disabled={status === "processing"}
            >
              ● Record utterance
            </button>
          )}
          <div className="flex rounded-lg overflow-hidden border border-slate-700">
            {(["CALLER", "EMPLOYEE"] as Speaker[]).map((s) => (
              <button
                key={s}
                className={`px-3 py-2 text-xs font-bold uppercase tracking-wider ${
                  speaker === s ? (s === "CALLER" ? "bg-rose-700 text-white" : "bg-sky-700 text-white") : "bg-slate-800 text-slate-400"
                }`}
                onClick={() => setSpeaker(s)}
                disabled={status === "recording"}
              >
                {s === "CALLER" ? "Caller speaks" : "Employee speaks"}
              </button>
            ))}
          </div>
          <input
            dir="auto"
            className="flex-1 min-w-[220px] bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100"
            placeholder="…or type a sentence and press Enter"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && status === "in-call" && sendTyped()}
            disabled={status === "recording"}
          />
          <button className={`${btn} bg-slate-700 hover:bg-slate-600 text-slate-100`} onClick={endCall}>
            End call
          </button>
        </>
      )}
      <span className="text-xs text-slate-400 font-mono" dir="auto">{message}</span>
    </div>
  );
};
