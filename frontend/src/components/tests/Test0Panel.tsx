"use client";

import React, { useCallback, useEffect, useState } from "react";
import { ClassifyResult, labApi, Tactic, Test0Data, Test0Result } from "./labApi";
import { Badge, Card, ErrorBox, Stat, TacticChips } from "./ui";

/** Test 0: the brain on typed Egyptian sentences, plus a playground for any sentence. */
export const Test0Panel: React.FC = () => {
  const [data, setData] = useState<Test0Data | null>(null);
  const [error, setError] = useState("");
  const [running, setRunning] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setData(await labApi.test0());
      setError("");
    } catch (e) {
      setError((e as Error).message);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const run = async (fresh: boolean) => {
    if (!data) return;
    const ids = data.items.map((i) => i.id).filter((id) => fresh || !data.results[id]);
    for (const id of ids) {
      setRunning(id);
      try {
        const result = await labApi.runTest0(id, fresh);
        setData((d) => (d ? { ...d, results: { ...d.results, [id]: result } } : d));
      } catch (e) {
        setError(`${id}: ${(e as Error).message}`);
        break;
      }
    }
    setRunning(null);
    load(); // refresh the summary from the server
  };

  if (error && !data) return <ErrorBox message={error} hint="Is the lab service running? (preview menu → lab)" />;
  if (!data) return <p className="text-slate-500 text-sm">Loading…</p>;

  const s = data.summary;
  const pending = data.items.filter((i) => !data.results[i.id]).length;

  return (
    <div className="space-y-6">
      <Card>
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap gap-6">
            <Stat label="Exactly correct" value={`${s.correct}/${s.total}`} sub={`pass line ${s.pass_line}`} good={s.correct >= s.pass_line} />
            <Stat label="Invented quotes kept" value={String(s.invalid_quotes_kept)} sub="pass line 0" good={s.invalid_quotes_kept === 0} />
            <Stat label="Median time" value={s.median_ms ? `${(s.median_ms / 1000).toFixed(1)} s` : "–"} sub="per sentence" />
            <div className="flex flex-col justify-center items-start">
              <Badge tone={s.passed ? "good" : s.answered < s.total ? "neutral" : "bad"}>
                {s.passed ? "TEST 0 PASS" : s.answered < s.total ? `${s.total - s.answered} NOT RUN` : "TEST 0 FAIL"}
              </Badge>
              <span className="text-[11px] text-slate-500 mt-1 font-mono">
                {data.model} · prompt {data.prompt_version}
              </span>
            </div>
          </div>
          <div className="flex gap-2">
            <button className="btn-primary" disabled={!!running || pending === 0} onClick={() => run(false)}>
              {running ? `Running ${running}…` : pending ? `Run ${pending} missing` : "All answered"}
            </button>
            <button
              className="btn-secondary"
              disabled={!!running}
              onClick={() => confirm("Re-run all 20 sentences? Uses 20 brain requests.") && run(true)}
            >
              Re-run all
            </button>
          </div>
        </div>
        {error && <p className="text-xs text-red-400 mt-3">{error}</p>}
      </Card>

      <Playground />

      <Card title="The 20 DEV sentences">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-slate-400 text-xs border-b border-slate-800">
                <th className="py-2 pr-3 w-12">#</th>
                <th className="py-2 pr-3">Caller sentence</th>
                <th className="py-2 pr-3 w-44">Expected</th>
                <th className="py-2 pr-3 w-72">Brain answer</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((item) => {
                const r: Test0Result | undefined = data.results[item.id];
                return (
                  <tr key={item.id} className="border-b border-slate-900 align-top">
                    <td className="py-3 pr-3 font-mono text-xs text-slate-400">{item.id}</td>
                    <td className="py-3 pr-3">
                      {item.context && item.context.length > 0 && (
                        <div className="text-[11px] text-slate-500 mb-1" dir="rtl">
                          سياق: {item.context.join(" ← ")}
                        </div>
                      )}
                      <div dir="rtl" className="text-base text-slate-100">{item.text}</div>
                      <div className="text-[11px] text-slate-500 mt-1">{item.why}</div>
                    </td>
                    <td className="py-3 pr-3">
                      {item.expected.length ? (
                        <div className="flex flex-wrap gap-1">
                          {item.expected.map((l) => (
                            <span key={l} className="chip">{l}</span>
                          ))}
                        </div>
                      ) : (
                        <span className="text-xs text-slate-500">no label</span>
                      )}
                    </td>
                    <td className="py-3 pr-3">
                      {running === item.id ? (
                        <span className="text-xs text-sky-400">asking the brain…</span>
                      ) : !r ? (
                        <span className="text-xs text-slate-500">not run</span>
                      ) : (
                        <div className="space-y-1">
                          <Badge tone={r.ok ? "good" : "bad"}>{r.ok ? "PASS" : "MISS"}</Badge>
                          {r.missing.length > 0 && <div className="text-[11px] text-red-400">missing: {r.missing.join(", ")}</div>}
                          {r.extra.length > 0 && <div className="text-[11px] text-amber-400">extra: {r.extra.join(", ")}</div>}
                          <TacticChips tactics={r.accepted} dropped={r.dropped} />
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
};

/** Type any caller sentence and see what the brain labels. Not stored. */
const Playground: React.FC = () => {
  const [text, setText] = useState("");
  const [context, setContext] = useState("");
  const [result, setResult] = useState<ClassifyResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const go = async () => {
    setBusy(true);
    setError("");
    try {
      const ctx = context.split("\n").map((l) => l.trim()).filter(Boolean);
      setResult(await labApi.classify(text, ctx));
    } catch (e) {
      setError((e as Error).message);
    }
    setBusy(false);
  };

  return (
    <Card title="Try any sentence (playground — not saved, not part of the test)">
      <div className="grid md:grid-cols-3 gap-3">
        <textarea
          dir="rtl"
          className="input md:col-span-2 h-20"
          placeholder="اكتب جملة المتصل هنا… مثال: ابعتلي الكود اللي جالك دلوقتي"
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
        <textarea
          dir="rtl"
          className="input h-20"
          placeholder="سياق اختياري: الجمل اللي قبلها، كل جملة في سطر"
          value={context}
          onChange={(e) => setContext(e.target.value)}
        />
      </div>
      <div className="flex items-center gap-3 mt-3">
        <button className="btn-primary" disabled={busy || !text.trim()} onClick={go}>
          {busy ? "Asking…" : "Ask the brain"}
        </button>
        {error && <span className="text-xs text-red-400">{error}</span>}
        {result && (
          <span className="text-[11px] text-slate-500 font-mono">
            {result.model} · {(result.latency_ms / 1000).toFixed(1)} s
          </span>
        )}
      </div>
      {result && (
        <div className="mt-3">
          {result.tactics.length === 0 && result.dropped.length === 0 && (
            <span className="text-sm text-emerald-400">No tactics — the brain sees nothing suspicious in this sentence.</span>
          )}
          <TacticChips tactics={result.tactics as Tactic[]} dropped={result.dropped as Tactic[]} />
          {result.needs_more_context && <div className="text-xs text-amber-400 mt-1">The brain says it needs more context.</div>}
        </div>
      )}
    </Card>
  );
};
