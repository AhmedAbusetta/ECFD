"""Build the Test 0 PDF report from the saved results (no API calls).

Usage (repo root):  python ml/brain/make_test0_report.py
Output:             docs/reports/ECFD_Test0_Report.pdf
Renders through headless Microsoft Edge/Chrome so Arabic shapes and flows right-to-left correctly.
"""

import datetime as dt
import html
import json
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))

from ecfd_brain.brain import validate  # noqa: E402

MODELS = [  # (results file, display name, provider note)
    ("results_openai_gpt-oss-120b.json", "GPT-OSS-120B via Groq", "Default brain · free · ~1,000 requests/day"),
    ("results_gemini-3.5-flash.json", "Gemini 3.5 Flash", "Free tier · 20 requests/day"),
]
BROWSERS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]


def e(text) -> str:
    return html.escape(str(text))


def score_model(items, path):
    store = json.loads(path.read_text(encoding="utf-8"))
    rows, latencies = {}, []
    for item in items:
        ans = store["answers"].get(item["id"])
        if ans is None:
            rows[item["id"]] = None
            continue
        accepted, dropped = validate(ans["raw"], item["text"])
        got = {t.label for t in accepted}
        rows[item["id"]] = {
            "ok": got == set(item["expected"]),
            "accepted": accepted,
            "dropped": dropped,
            "missing": sorted(set(item["expected"]) - got),
            "extra": sorted(got - set(item["expected"])),
        }
        latencies.append(ans["latency_ms"])
    answered = [r for r in rows.values() if r is not None]
    latencies.sort()
    return {
        "prompt": store.get("prompt_version", "?"),
        "rows": rows,
        "answered": len(answered),
        "correct": sum(r["ok"] for r in answered),
        "invalid_kept": 0,  # validate() never accepts a quote that is not in the sentence
        "dropped": sum(len(r["dropped"]) for r in answered),
        "median": latencies[len(latencies) // 2] if latencies else None,
        "max": latencies[-1] if latencies else None,
    }


def labels_html(result) -> str:
    if result is None:
        return '<span class="muted">not run (daily quota)</span>'
    parts = [f'<div class="lab">{e(t.label)} <span class="conf">{e(t.confidence)}</span>'
             f'<div class="ar quote">«{e(t.quote)}»</div></div>' for t in result["accepted"]]
    parts += [f'<div class="lab dropped">✕ {e(t.label)} <span class="conf">dropped: {e(why)}</span>'
              f'<div class="ar quote">«{e(t.quote)}»</div></div>' for t, why in result["dropped"]]
    return "".join(parts) or ' <span class="muted">no label</span>'


def verdict(result) -> str:
    if result is None:
        return '<span class="badge na">—</span>'
    return '<span class="badge ok">PASS</span>' if result["ok"] else '<span class="badge miss">MISS</span>'


def build_html(items, scored, dev_note) -> str:
    today = dt.date.today().strftime("%d %B %Y")
    main_name = MODELS[0][1]
    main = scored[main_name]
    passed = main["answered"] == len(items) and main["correct"] >= 15 and main["invalid_kept"] == 0

    comparison = "".join(
        f"<tr><td><b>{e(name)}</b><div class='muted'>{e(note)}</div></td>"
        f"<td>{s['correct']}/{s['answered']}"
        + (f" <span class='muted'>({len(items) - s['answered']} not run)</span>" if s['answered'] < len(items) else "")
        + f"</td><td>{s['invalid_kept']}</td><td>{s['dropped']}</td>"
        f"<td>{(s['median'] or 0) / 1000:.1f} s</td><td>{(s['max'] or 0) / 1000:.1f} s</td>"
        f"<td>{'<span class=\"badge ok\">PASS</span>' if s['answered'] == len(items) and s['correct'] >= 15 else '<span class=\"badge na\">INCOMPLETE</span>' if s['answered'] < len(items) else '<span class=\"badge miss\">FAIL</span>'}</td></tr>"
        for name, note, s in ((n, note, scored[n]) for _, n, note in MODELS if n in scored)
    )

    rows = []
    for item in items:
        ctx = ""
        if item.get("context"):
            ctx = "<div class='ctx'>context (previous turns):" + "".join(f"<div class='ar'>{e(c)}</div>" for c in item["context"]) + "</div>"
        expected = ", ".join(item["expected"]) or "<span class='muted'>no label</span>"
        cells = "".join(f"<td>{verdict(scored[n]['rows'][item['id']])}{labels_html(scored[n]['rows'][item['id']])}</td>"
                        for _, n, _ in MODELS if n in scored)
        rows.append(f"<tr><td class='id'>{e(item['id'])}</td><td><div class='ar sentence'>{e(item['text'])}</div>{ctx}"
                    f"<div class='why'>{e(item['why'])}</div></td><td class='exp'>{expected}</td>{cells}</tr>")
    model_heads = "".join(f"<th>{e(n)}</th>" for _, n, _ in MODELS if n in scored)

    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<title>ECFD Test 0 Report</title>
<style>
@page {{ size: A4; margin: 16mm 14mm; }}
body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #1c2430; font-size: 10.5pt; line-height: 1.45; }}
h1 {{ font-size: 20pt; margin: 0 0 2pt; }}
h2 {{ font-size: 13pt; margin: 18pt 0 6pt; border-bottom: 1.5pt solid #1c2430; padding-bottom: 2pt; }}
.sub {{ color: #5b6675; margin-bottom: 12pt; }}
.verdict {{ border: 1.5pt solid {'#1f7a3d' if passed else '#a33'}; background: {'#eef8f1' if passed else '#fbeeee'}; padding: 9pt 12pt; border-radius: 6pt; }}
.verdict b.big {{ font-size: 14pt; color: {'#1f7a3d' if passed else '#a33'}; }}
table {{ border-collapse: collapse; width: 100%; margin: 4pt 0; }}
th, td {{ border: 0.6pt solid #c9d0d8; padding: 4pt 5pt; vertical-align: top; text-align: left; }}
th {{ background: #eef1f5; font-size: 9.5pt; }}
tr {{ page-break-inside: avoid; }}
.ar {{ font-family: 'Segoe UI', 'Arial', 'Tahoma', sans-serif; direction: rtl; unicode-bidi: plaintext; text-align: right; }}
.sentence {{ font-size: 11.5pt; }}
.quote {{ font-size: 9.5pt; color: #3d4754; }}
.why, .ctx {{ font-size: 8.5pt; color: #6a7482; margin-top: 2pt; }}
.id {{ font-weight: 700; white-space: nowrap; }}
.exp {{ font-family: Consolas, monospace; font-size: 8.5pt; }}
.lab {{ font-family: Consolas, monospace; font-size: 8.5pt; margin-top: 3pt; }}
.lab.dropped {{ color: #a33; }}
.conf {{ color: #6a7482; }}
.badge {{ display: inline-block; font-size: 8pt; font-weight: 700; padding: 1pt 5pt; border-radius: 3pt; }}
.badge.ok {{ background: #1f7a3d; color: #fff; }} .badge.miss {{ background: #b3261e; color: #fff; }} .badge.na {{ background: #c9d0d8; color: #1c2430; }}
.muted {{ color: #7a8491; font-size: 8.5pt; }}
ul {{ margin: 3pt 0; padding-left: 16pt; }} li {{ margin: 2pt 0; }}
.small {{ font-size: 9pt; color: #5b6675; }}
</style></head><body>

<h1>ECFD — Test 0 Report: The Brain on Typed Sentences</h1>
<div class="sub">Egyptian Conversational Fraud Defense · 6th of October University · Generated {today} · Prompt version {e(main['prompt'])}</div>

<div class="verdict"><b class="big">Test 0: {'PASS' if passed else 'NOT PASSED'}</b> — the default brain ({e(main_name)}) labelled
<b>{main['correct']} of {len(items)}</b> sentences exactly right (pass line: 15) and <b>{main['invalid_kept']}</b> invented quotes survived (pass line: 0).
Median response time <b>{(main['median'] or 0) / 1000:.1f} s</b>.</div>

<h2>1. What was tested</h2>
<ul>
<li><b>Goal:</b> check that the NLP "brain" (pipeline stage 6) labels social-engineering tactics in Egyptian Arabic correctly <i>before</i> adding speech recognition and live calls.</li>
<li><b>Input:</b> {len(items)} typed caller sentences (DEV set) with hand-written expected labels, including traps: warnings that mention OTP, genuine IT and work requests, an OTP request without the word «كود», and a short reply that only makes sense with the two previous turns.</li>
<li><b>Brain:</b> one LLM call per sentence, temperature 0, forced tool call returning <code>tactics[label, confidence low|medium|high, quote]</code> and <code>needs_more_context</code>. 11 labels: identity_claim, authority, urgency, fear_threat, secrecy, verification_bypass, otp_request, credential_request, payment_request, remote_access_request, sensitive_action_request.</li>
<li><b>Safeguard:</b> every label must quote words that really appear in the sentence (after Arabic normalisation); otherwise it is dropped.</li>
<li><b>Correct</b> means the set of accepted labels equals the expected set exactly (confidence ignored).</li>
<li><b>Pass lines (pre-agreed):</b> at least 15 of 20 exactly correct; zero invalid quotes kept.</li>
</ul>

<h2>2. Results by model</h2>
<table><tr><th>Model</th><th>Exactly correct</th><th>Invalid quotes kept</th><th>Labels dropped by safeguard</th><th>Median time</th><th>Slowest</th><th>Result</th></tr>
{comparison}</table>
<p class="small">Both models used the identical prompt and safeguards. Gemini stopped after 18 sentences because its free tier allows only 20 requests per day per model; that limit also rules it out for live calls (one 3-minute call ≈ 15–30 requests). Groq was therefore made the default brain.</p>

<h2 style="page-break-before: always">3. Sentence by sentence</h2>
<table><thead><tr><th>#</th><th style="width:30%">Caller sentence</th><th style="width:13%">Expected</th>{model_heads}</tr></thead>
{''.join(rows)}</table>

<h2>4. The misses, explained</h2>
<ul>
<li><b>T09 (GPT-OSS):</b> correct <code>remote_access_request</code>, plus an extra <code>credential_request</code> for «ابعتلي الرقم اللي هيظهرلك». That number is the AnyDesk ID, which grants access — the extra label is arguable. <i>Needs a team decision on the expected answer.</i></li>
<li><b>T13 (GPT-OSS):</b> the model chose the right label but quoted «افتح» instead of the real «افتحه», so the safeguard dropped it. The safety net worked as designed; it cost one label. Not changed yet (one change at a time); watch for this in later tests.</li>
<li><b>T05 (Gemini):</b> extra <code>sensitive_action_request</code> for «أكدتش العملية» — the caller does indirectly push to confirm a transaction. Arguable.</li>
<li><b>T07 (Gemini):</b> extra <code>fear_threat</code> (medium) for «أوقف العملية المشبوهة» — mild fear framing. Arguable.</li>
</ul>
<p>All four misses are <b>extra</b> labels or a dropped label; no model missed a fraud request, and no model labelled a warning («عمرك ما تدي حد الـ OTP») or ordinary work request as a tactic.</p>

<h2>5. Limitations — read before quoting these numbers</h2>
<ul>
<li><b>Expected labels were drafted by an AI assistant</b> (Claude, 3 October 2026) and have not yet been reviewed by a native Egyptian-Arabic speaker. T05, T07 and T09 in particular need a team decision.</li>
<li><b>Typed, clean text.</b> Real calls add speech-recognition errors (expected word error rate 30–45%). Test 1 and Test 2 measure that.</li>
<li><b>DEV set only, 20 sentences.</b> Prompt tuning on these is allowed; these numbers must not be reported as final accuracy. Final results come from the held-out test set.</li>
<li><b>Latency is per sentence over the internet</b> on free tiers; occasional slow outliers come from provider overload and retries. Test 2 measures end-to-end delay.</li>
</ul>

<h2>6. Next steps</h2>
<ul>
<li>Team review of the expected labels (T05, T07, T09); fix the expected answers, not the prompt, if the model was right.</li>
<li>Test 1 — speech: 10–15 phone-quality clips through Speechmatics; keyword recall on protected words ≥ 70%.</li>
<li>Keep Claude Sonnet 5.5 as the target brain when credit is available; re-run this exact test for a fair comparison (one setting: <code>LLM_PROVIDER</code>).</li>
</ul>
<p class="small">Reproduce: <code>python ml/brain/run_test0.py</code> (re-uses saved answers) · regenerate this report: <code>python ml/brain/make_test0_report.py</code></p>
</body></html>"""


def main() -> int:
    data = json.loads((HERE / "test0" / "dev_sentences.json").read_text(encoding="utf-8"))
    items = data["items"]
    scored = {}
    for filename, name, _ in MODELS:
        path = HERE / "test0" / filename
        if path.exists():
            scored[name] = score_model(items, path)
    if MODELS[0][1] not in scored:
        print("No results for the default model yet; run run_test0.py first.")
        return 1

    out_dir = ROOT / "docs" / "reports"
    out_dir.mkdir(parents=True, exist_ok=True)
    html_path = out_dir / "ECFD_Test0_Report.html"
    pdf_path = out_dir / "ECFD_Test0_Report.pdf"
    html_path.write_text(build_html(items, scored, data.get("note", "")), encoding="utf-8")

    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("msedge") or shutil.which("chrome")
    if not browser:
        print(f"No Edge/Chrome found; open {html_path} and print it to PDF.")
        return 1
    # A throwaway profile stops Edge/Chrome from handing the job to an already-open window.
    profile = Path(tempfile.mkdtemp(prefix="ecfd-pdf-"))
    subprocess.run([browser, "--headless", "--disable-gpu", "--no-pdf-header-footer",
                    f"--user-data-dir={profile}",
                    f"--print-to-pdf={pdf_path}", html_path.as_uri()], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    for _ in range(30):  # Edge can return before the file is fully written
        if pdf_path.exists() and pdf_path.stat().st_size > 0:
            break
        time.sleep(0.5)
    shutil.rmtree(profile, ignore_errors=True)
    if not pdf_path.exists():
        print("The browser did not produce the PDF.")
        return 1
    print(f"Wrote {pdf_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
