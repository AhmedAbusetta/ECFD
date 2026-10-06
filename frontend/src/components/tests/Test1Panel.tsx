"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { Clip, labApi, Test1Data, Test1Row } from "./labApi";
import { Badge, Card, ErrorBox, Stat } from "./ui";

const SPEAKER_KEY = "ecfd-test1-speaker";

/** Test 1: record each line with the microphone; Speechmatics transcribes phone-quality audio. */
export const Test1Panel: React.FC = () => {
  const [speaker, setSpeaker] = useState("");
  const [view, setView] = useState<"me" | "all">("me");
  const [data, setData] = useState<Test1Data | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    try {
      setSpeaker(localStorage.getItem(SPEAKER_KEY) || "");
    } catch {
      /* storage unavailable */
    }
  }, []);

  const validName = /^[A-Za-z0-9]{1,30}$/.test(speaker);

  const load = useCallback(async () => {
    try {
      setData(await labApi.test1(view === "me" && validName ? [speaker] : undefined));
      setError("");
    } catch (e) {
      setError((e as Error).message);
    }
  }, [speaker, view, validName]);

  useEffect(() => {
    load();
  }, [load]);

  const saveSpeaker = (name: string) => {
    setSpeaker(name);
    try {
      localStorage.setItem(SPEAKER_KEY, name);
    } catch {
      /* ignore */
    }
  };

  if (error && !data) return <ErrorBox message={error} hint="Is the lab service running, and is SPEECHMATICS_API_KEY in .env?" />;
  if (!data) return <p className="text-slate-500 text-sm">Loading…</p>;

  const s = data.summary;
  const myRows = new Map(data.rows.filter((r) => r.speaker.toLowerCase() === speaker.toLowerCase()).map((r) => [r.clip_id, r]));

  return (
    <div className="space-y-6">
      <Card>
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="flex flex-wrap gap-6">
            <Stat
              label="Keyword recall"
              value={s.expected ? `${Math.round(s.recall * 100)}%` : "–"}
              sub={`${s.found}/${s.expected} words · pass line ${Math.round(s.pass_line * 100)}%`}
              good={s.expected ? s.passed : undefined}
            />
            <Stat label="Word error rate" value={s.median_wer != null ? `${Math.round(s.median_wer * 100)}%` : "–"} sub="median · info only" />
            <Stat label="Speakers" value={String(view === "me" ? (validName ? 1 : 0) : data.speakers.length)} sub={view === "me" ? "just you" : data.speakers.join(", ") || "none yet"} />
          </div>
          <div className="flex flex-col gap-2 items-end">
            <div className="flex items-center gap-2">
              <label className="text-xs text-slate-400">Your name</label>
              <input
                className="input w-36"
                placeholder="e.g. Ahmed"
                value={speaker}
                onChange={(e) => saveSpeaker(e.target.value.replace(/[^A-Za-z0-9]/g, ""))}
              />
            </div>
            <div className="flex gap-1">
              <button className={view === "me" ? "btn-primary" : "btn-secondary"} onClick={() => setView("me")}>My results</button>
              <button className={view === "all" ? "btn-primary" : "btn-secondary"} onClick={() => setView("all")}>All speakers</button>
            </div>
          </div>
        </div>
        {Object.keys(s.per_word).length > 0 && (
          <div className="mt-5 grid grid-cols-2 md:grid-cols-4 gap-2">
            {Object.entries(s.per_word)
              .sort((a, b) => a[1][0] / a[1][1] - b[1][0] / b[1][1])
              .map(([word, [found, total]]) => (
                <div key={word} className="bg-slate-950 border border-slate-800 rounded-lg px-3 py-2">
                  <div className="flex justify-between text-xs">
                    <span className="font-mono text-slate-300">{word}</span>
                    <span className={found / total >= s.pass_line ? "text-emerald-400" : "text-red-400"}>{found}/{total}</span>
                  </div>
                  <div className="h-1.5 bg-slate-800 rounded mt-1.5">
                    <div
                      className={`h-1.5 rounded ${found / total >= s.pass_line ? "bg-emerald-500" : "bg-red-500"}`}
                      style={{ width: `${(found / total) * 100}%` }}
                    />
                  </div>
                </div>
              ))}
          </div>
        )}
        <p className="text-[11px] text-slate-500 mt-4">
          {data.engine} · config {data.config} · audio is converted to 8 kHz phone quality before transcription.
          Tune only on DEV speakers; keep TEST speakers for the final number. Recordings stay on this machine (git-ignored).
        </p>
        {error && <p className="text-xs text-red-400 mt-2">{error}</p>}
      </Card>

      {view === "all" ? (
        <AllSpeakersTable data={data} />
      ) : !validName ? (
        <Card>
          <p className="text-sm text-slate-400">Type your name above (letters and digits only) to start recording.</p>
        </Card>
      ) : (
        <div className="space-y-3">
          {data.clips.map((clip) => (
            <ClipRecorder key={clip.id} clip={clip} speaker={speaker} row={myRows.get(clip.id)} onDone={load} />
          ))}
        </div>
      )}
    </div>
  );
};

const ClipRecorder: React.FC<{ clip: Clip; speaker: string; row?: Test1Row; onDone: () => void }> = ({ clip, speaker, row, onDone }) => {
  const [state, setState] = useState<"idle" | "recording" | "uploading">("idle");
  const [error, setError] = useState("");
  const recorder = useRef<MediaRecorder | null>(null);
  const chunks = useRef<Blob[]>([]);

  const start = async () => {
    setError("");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        // keep the raw voice: the test should hear what a phone line would carry, not browser clean-up
        audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
      });
      const r = new MediaRecorder(stream);
      chunks.current = [];
      r.ondataavailable = (e) => e.data.size > 0 && chunks.current.push(e.data);
      r.onstop = async () => {
        stream.getTracks().forEach((t) => t.stop());
        setState("uploading");
        try {
          await labApi.record(speaker, clip.id, new Blob(chunks.current, { type: r.mimeType }));
          onDone();
        } catch (e) {
          setError((e as Error).message);
        }
        setState("idle");
      };
      r.start();
      recorder.current = r;
      setState("recording");
    } catch (e) {
      setError(`Microphone unavailable: ${(e as Error).message}`);
    }
  };

  const stop = () => recorder.current?.stop();

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-2">
          <span className="font-extrabold text-white">{clip.id}</span>
          <Badge tone={clip.kind === "fraud" ? "warn" : "neutral"}>{clip.kind}</Badge>
          {clip.noise && <Badge tone="bad">record with background noise</Badge>}
        </div>
        <div className="flex items-center gap-2">
          {row && <audio controls preload="none" src={labApi.audioUrl(row.file)} className="h-8" />}
          {state === "recording" ? (
            <button className="btn-danger animate-pulse" onClick={stop}>■ Stop</button>
          ) : (
            <button className="btn-primary" disabled={state === "uploading"} onClick={start}>
              {state === "uploading" ? "Transcribing…" : row ? "● Record again" : "● Record"}
            </button>
          )}
        </div>
      </div>

      <p dir="rtl" className="text-xl leading-relaxed text-slate-100 mt-3">{clip.text}</p>

      {row?.transcribed && (
        <div className="mt-3 border-t border-slate-800 pt-3 space-y-2">
          <div className="text-[11px] uppercase tracking-wider text-slate-500">Speechmatics heard</div>
          <p dir="rtl" className="text-base text-sky-200">{row.transcript || "(nothing)"}</p>
          <div className="flex flex-wrap items-center gap-2">
            {row.hits?.map((k) => (
              <span key={k} className="px-2 py-0.5 rounded text-xs bg-emerald-600/20 text-emerald-300 border border-emerald-600/40">✓ {k}</span>
            ))}
            {row.misses?.map((k) => (
              <span key={k} className="px-2 py-0.5 rounded text-xs bg-red-600/20 text-red-300 border border-red-600/40">✗ {k}</span>
            ))}
            {row.wer != null && <span className="text-[11px] text-slate-500 ml-2">WER {Math.round(row.wer * 100)}%</span>}
          </div>
        </div>
      )}
      {error && <p className="text-xs text-red-400 mt-2">{error}</p>}
    </Card>
  );
};

const AllSpeakersTable: React.FC<{ data: Test1Data }> = ({ data }) => {
  const byClip = new Map<string, Test1Row[]>();
  data.rows.forEach((r) => byClip.set(r.clip_id, [...(byClip.get(r.clip_id) || []), r]));
  return (
    <Card title="Every recording">
      {data.rows.length === 0 ? (
        <p className="text-sm text-slate-500">No recordings yet.</p>
      ) : (
        <div className="space-y-4">
          {Object.entries(data.summary.per_speaker).map(([sp, [f, n]]) => (
            <span key={sp} className="inline-block mr-4 text-xs text-slate-400">
              <span className="font-bold text-slate-200">{sp}</span> {f}/{n} ({Math.round((f / n) * 100)}%)
            </span>
          ))}
          {data.clips.map((clip) =>
            (byClip.get(clip.id) || []).map((r) => (
              <div key={r.file} className="flex gap-3 border-b border-slate-900 pb-2 text-sm">
                <span className="font-mono text-xs text-slate-400 w-10">{clip.id}</span>
                <span className="text-xs text-slate-300 w-20">{r.speaker}</span>
                <span dir="rtl" className="flex-1 text-sky-200">{r.transcribed ? r.transcript : "not transcribed yet"}</span>
                <span className="w-40 text-xs">
                  {r.hits?.map((k) => <span key={k} className="text-emerald-400 mr-1">✓{k}</span>)}
                  {r.misses?.map((k) => <span key={k} className="text-red-400 mr-1">✗{k}</span>)}
                </span>
              </div>
            )),
          )}
        </div>
      )}
    </Card>
  );
};
