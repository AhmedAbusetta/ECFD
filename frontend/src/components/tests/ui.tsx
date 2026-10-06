"use client";

import React from "react";
import { Tactic } from "./labApi";

export const Card: React.FC<{ title?: string; children: React.ReactNode }> = ({ title, children }) => (
  <section className="bg-slate-900 border border-slate-800 rounded-xl p-5">
    {title && <h3 className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-4">{title}</h3>}
    {children}
  </section>
);

export const Badge: React.FC<{ tone: "good" | "bad" | "neutral" | "warn"; children: React.ReactNode }> = ({ tone, children }) => {
  const colors = {
    good: "bg-emerald-600 text-white",
    bad: "bg-red-600 text-white",
    warn: "bg-amber-500 text-black",
    neutral: "bg-slate-700 text-slate-200",
  }[tone];
  return <span className={`inline-block px-2 py-0.5 rounded text-[11px] font-bold tracking-wide ${colors}`}>{children}</span>;
};

export const Stat: React.FC<{ label: string; value: string; sub?: string; good?: boolean }> = ({ label, value, sub, good }) => (
  <div>
    <div className="text-[11px] uppercase tracking-wider text-slate-500">{label}</div>
    <div className={`text-2xl font-extrabold ${good === undefined ? "text-white" : good ? "text-emerald-400" : "text-red-400"}`}>{value}</div>
    {sub && <div className="text-[11px] text-slate-500">{sub}</div>}
  </div>
);

export const TacticChips: React.FC<{ tactics: Tactic[]; dropped?: Tactic[] }> = ({ tactics, dropped = [] }) => (
  <div className="space-y-1">
    {tactics.map((t, i) => (
      <div key={`a${i}`} className="text-xs">
        <span className="font-mono text-amber-300">{t.label}</span>{" "}
        <span className="text-slate-500">{t.confidence}</span>
        <div dir="rtl" className="text-slate-300">«{t.quote}»</div>
      </div>
    ))}
    {dropped.map((t, i) => (
      <div key={`d${i}`} className="text-xs text-red-400/80">
        <span className="font-mono line-through">{t.label}</span> <span>dropped: {t.reason}</span>
        <div dir="rtl">«{t.quote}»</div>
      </div>
    ))}
  </div>
);

export const ErrorBox: React.FC<{ message: string; hint?: string }> = ({ message, hint }) => (
  <div className="bg-red-950/50 border border-red-800 rounded-xl p-4 text-sm text-red-200">
    {message}
    {hint && <div className="text-xs text-red-300/70 mt-1">{hint}</div>}
  </div>
);
