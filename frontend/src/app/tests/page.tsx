"use client";

import Link from "next/link";
import React, { useState } from "react";
import { Test0Panel } from "@/components/tests/Test0Panel";
import { Test1Panel } from "@/components/tests/Test1Panel";
import { CallPanel } from "@/components/tests/CallPanel";

const TABS = [
  { id: "test0", title: "Test 0 — Brain", sub: "20 typed sentences · pass ≥ 15/20" },
  { id: "test1", title: "Test 1 — Speech", sub: "phone-quality clips · keyword recall ≥ 70%" },
  { id: "call", title: "📞 Simulated call", sub: "talk live · full pipeline · alert delay" },
] as const;

export default function TestsPage() {
  const [tab, setTab] = useState<(typeof TABS)[number]["id"]>("test0");

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 p-8 font-sans">
      <header className="flex justify-between items-center pb-6 mb-6 border-b border-slate-800">
        <div>
          <h1 className="text-xl font-extrabold tracking-tight text-white">ECFD — Test Lab</h1>
          <p className="text-xs text-slate-400 mt-1">Measure each AI part before the live call. DEV data only — never tune on TEST speakers.</p>
        </div>
        <Link href="/" className="btn-secondary">← Live console</Link>
      </header>

      <nav className="flex gap-3 mb-6">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`text-left px-4 py-3 rounded-xl border transition ${
              tab === t.id ? "bg-slate-800 border-sky-600" : "bg-slate-900 border-slate-800 hover:border-slate-600"
            }`}
          >
            <div className="text-sm font-bold">{t.title}</div>
            <div className="text-[11px] text-slate-400">{t.sub}</div>
          </button>
        ))}
      </nav>

      {tab === "test0" ? <Test0Panel /> : tab === "test1" ? <Test1Panel /> : <CallPanel />}
    </div>
  );
}
