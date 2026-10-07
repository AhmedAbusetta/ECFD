"""Test 2 - split the calls' CALLER and EMPLOYEE parts across the team and make each person's sheet.

Reads calls.json and team.json, balances the number of lines per person, never gives one person
both sides of the same call, and mixes roles (everyone records some caller and some employee lines).

Outputs (pack/, git-ignored: it contains teammates' names):
  pack/assignments.json             who speaks which part of which call (used by run_recorded_calls.py)
  pack/ECFD_Test2_<Name>.pdf        one recording sheet per person: a numbered list of their lines (Arabic)
  pack/WhatsApp_<Name>.txt          the same list as a message to paste into WhatsApp
  pack/ECFD_Test2_Overview.pdf      the whole plan, for the person collecting the files

team.json:  [{"name": "AhmedAbusetta", "display": "Ahmed Abusetta"}, ...]
            name = used in file names (letters and digits only)

Usage (repo root):  python ml/asr/test2/assign_roles.py
"""

import html
import json
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
PACK = HERE / "pack"
BROWSERS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]


def e(text) -> str:
    return html.escape(str(text))


def assign(calls: list, team: list) -> dict:
    """Greedy balance: give each call's parts to the people with the fewest lines so far."""
    load = {p["name"]: 0 for p in team}
    roles = {p["name"]: {"CALLER": 0, "EMPLOYEE": 0} for p in team}
    order = {p["name"]: i for i, p in enumerate(team)}
    plan = {}
    # longest calls first so the big pieces get spread before the small ones
    for call in sorted(calls, key=lambda c: -len(c["turns"])):
        lines = {r: sum(t["speaker"] == r for t in call["turns"]) for r in ("CALLER", "EMPLOYEE")}
        chosen = {}
        for role in sorted(lines, key=lambda r: -lines[r]):
            candidates = [n for n in load if n not in chosen.values()]
            # fewest lines first, then whoever has played this role least, then team order
            best = min(candidates, key=lambda n: (load[n], roles[n][role], order[n]))
            chosen[role] = best
            load[best] += lines[role]
            roles[best][role] += 1
        plan[call["id"]] = chosen
    return {c["id"]: plan[c["id"]] for c in calls}


def person_lines(calls: list, plan: dict, name: str) -> list:
    out = []
    for call in calls:
        for role, who in plan[call["id"]].items():
            if who != name:
                continue
            for i, turn in enumerate(call["turns"]):
                if turn["speaker"] == role:
                    cue = call["turns"][i - 1]["text"] if i > 0 else None
                    out.append((call, i + 1, role, turn["text"], cue))
    return out


CSS = """
@page { size: A4; margin: 14mm; }
body { font-family: "Segoe UI", Arial, sans-serif; font-size: 10.5pt; color: #1b1f24; line-height: 1.5; }
h1 { color: #1f4e79; font-size: 19pt; margin: 0 0 4px; } h2 { color: #1f4e79; font-size: 13pt; margin: 14px 0 6px; }
.sub { color: #5b6470; font-size: 9.5pt; }
.box { border: 1px solid #d9dee4; border-radius: 6px; padding: 8px 12px; margin: 8px 0; background: #f3f6f9; }
.box li { margin: 2px 0; }
.card { border: 1px solid #d9dee4; border-radius: 6px; padding: 8px 12px; margin: 8px 0; break-inside: avoid; }
.meta { font-size: 9pt; color: #5b6470; display: flex; justify-content: space-between; }
.cue { direction: rtl; color: #7a828c; font-size: 10pt; margin-top: 4px; }
.line { direction: rtl; font-size: 15pt; margin: 4px 0; }
.file code { font-family: Consolas, monospace; background: #eaf2fa; padding: 1px 5px; border-radius: 3px; font-size: 10pt; }
.role-CALLER { color: #a3261f; font-weight: 600; } .role-EMPLOYEE { color: #1f4e79; font-weight: 600; }
table { width: 100%; border-collapse: collapse; font-size: 9.5pt; } th { background: #1f4e79; color: #fff; text-align: left; padding: 4px 6px; }
td { border-bottom: 1px solid #d9dee4; padding: 4px 6px; } tr { break-inside: avoid; }
"""

ROLE_AR = {"CALLER": "إنت المتصل", "EMPLOYEE": "إنت الموظف"}

STEPS_AR = [
    "افتح برنامج تسجيل الصوت على موبايلك (Voice Recorder).",
    "سجّل كل جملة في تسجيل لوحده.",
    "قول الجملة زي ما بتتكلم في التليفون عادي، مش كأنك بتقرا. امسك الموبايل جنب ودنك زي المكالمة.",
    "قول الكلام زي ما هو مكتوب بالظبط، حتى لو مكتوب بصيغة ولد أو بنت.",
    "<b>سمّي كل تسجيل باسمك ورقم الجملة</b>، زي اللي مكتوب تحت كل جملة (مثلاً: {name} 1 ، {name} 2).",
    "لو غلطت في جملة: سجّلها تاني بنفس الاسم واحذف القديمة.",
    "لما تخلص، ابعت كل التسجيلات لأحمد أبوستة على Drive أو كـ Document على واتساب (عشان الأسامي متتغيرش).",
]


def person_html(person: dict, lines: list) -> str:
    steps = "".join(f"<li>{s.format(name=e(person['name']))}</li>" for s in STEPS_AR)
    rows = "".join(f"""
<div class="card">
  <div class="meta"><span class="num">{n}</span><span class="role-{role}">{e(ROLE_AR[role])}</span></div>
  <div class="line">{e(text)}</div>
  <div class="file">اسم الملف: <code dir="ltr">{e(person['name'])} {n}</code></div>
</div>""" for n, (_, _, role, text, _) in enumerate(lines, 1))
    return f"""<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>{CSS}
body {{ direction: rtl; }} .meta {{ justify-content: flex-start; gap: 14px; font-size: 11pt; }}
.num {{ background: #1f4e79; color: #fff; border-radius: 50%; width: 28px; height: 28px; display: inline-flex;
        align-items: center; justify-content: center; font-weight: 600; }}
.box ol {{ margin: 4px 22px 0 0; }} .line {{ font-size: 16pt; }}</style></head><body>
<h1>ECFD — التسجيلات بتاعتك</h1>
<div class="sub">{e(person['display'])} · {len(lines)} جملة · حوالي {max(5, len(lines) // 2)} دقايق</div>
<div class="box"><b>إزاي تسجّل</b><ol>{steps}</ol></div>
{rows}
</body></html>"""


def person_whatsapp(person: dict, lines: list) -> str:
    out = [f"التسجيلات بتاعتك يا {person['display']} ({len(lines)} جملة)",
           "سجّل كل جملة في تسجيل لوحده، زي ما بتتكلم في التليفون عادي.",
           f"سمّي كل تسجيل باسمك ورقم الجملة: {person['name']} 1 ، {person['name']} 2 ، وهكذا.",
           "لو غلطت سجّلها تاني بنفس الاسم. وابعتهم كلهم لأحمد أبوستة كـ Document أو على Drive لما تخلص.",
           ""]
    out += [f"{n}) ({ROLE_AR[role]}) {text}\n   ← اسم الملف: {person['name']} {n}\n"
            for n, (_, _, role, text, _) in enumerate(lines, 1)]
    return "\n".join(out) + "\n"


def overview_html(calls: list, plan: dict, team: list) -> str:
    display = {p["name"]: p["display"] for p in team}
    rows = "".join(
        f"<tr><td>{e(c['id'])}</td><td>{e(c['label'])} · {e(c['kind'])}</td><td>{e(c['description'])}</td>"
        f"<td>{e(display[plan[c['id']]['CALLER']])}</td><td>{e(display[plan[c['id']]['EMPLOYEE']])}</td></tr>"
        for c in calls)
    counts = []
    for p in team:
        lines = person_lines(calls, plan, p["name"])
        roles = {r: sum(1 for x in lines if x[2] == r) for r in ("CALLER", "EMPLOYEE")}
        counts.append(f"<tr><td>{e(p['display'])}</td><td>{e(p['name'])}</td><td>{len(lines)}</td>"
                      f"<td>{roles['CALLER']}</td><td>{roles['EMPLOYEE']}</td></tr>")
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>{CSS}</style></head><body>
<h1>ECFD Test 2 — recording plan</h1>
<div class="sub">{len(calls)} calls · {sum(len(c['turns']) for c in calls)} lines · {len(team)} people</div>
<h2>Who records what</h2>
<table><tr><th>Call</th><th>Type</th><th>Description</th><th>Caller</th><th>Employee</th></tr>{rows}</table>
<h2>Lines per person</h2>
<table><tr><th>Person</th><th>File name</th><th>Lines</th><th>As caller</th><th>As employee</th></tr>{''.join(counts)}</table>
<h2>Collecting the files</h2>
<p>Put all received files in one folder (e.g. <code>incoming/</code>; sub-folders are fine), then run<br>
<code>python ml/asr/test2/import_recordings.py incoming</code> - it reads the name and number from each file name.</p>
</body></html>"""


def to_pdf(html_text: str, pdf_path: Path) -> None:
    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("msedge") or shutil.which("chrome")
    if not browser:
        raise RuntimeError("Edge or Chrome is needed to make the PDFs")
    # Edge can still hold its profile files for a moment after printing, so don't fail on cleanup
    with tempfile.TemporaryDirectory(prefix="ecfd-t2-", ignore_cleanup_errors=True) as tmp:
        src = Path(tmp) / "page.html"
        src.write_text(html_text, encoding="utf-8")
        if pdf_path.exists():
            pdf_path.unlink()
        subprocess.run([browser, "--headless", "--disable-gpu", "--no-pdf-header-footer",
                        f"--user-data-dir={Path(tmp) / 'profile'}", f"--print-to-pdf={pdf_path}", src.as_uri()],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
        for _ in range(30):
            if pdf_path.exists() and pdf_path.stat().st_size > 0:
                return
            time.sleep(0.5)
    raise RuntimeError(f"PDF was not written: {pdf_path}")


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    calls = json.loads((HERE / "calls.json").read_text(encoding="utf-8"))["calls"]
    team = json.loads((HERE / "team.json").read_text(encoding="utf-8"))
    bad = [p["name"] for p in team if not p["name"].isalnum()]
    if bad or len(team) < 2:
        print(f"team.json needs at least 2 people and letters/digits-only names: {bad}")
        return 1

    plan = assign(calls, team)
    PACK.mkdir(exist_ok=True)
    # order = the person's numbered list; import_recordings.py maps their files to it by position
    order = {p["name"]: [f"{c['id']}_{n:02d}" for c, n, _, _, _ in person_lines(calls, plan, p["name"])] for p in team}
    (PACK / "assignments.json").write_text(json.dumps({"roles": plan, "order": order}, ensure_ascii=False, indent=1),
                                           encoding="utf-8")
    for p in team:
        lines = person_lines(calls, plan, p["name"])
        to_pdf(person_html(p, lines), PACK / f"ECFD_Test2_{p['name']}.pdf")
        (PACK / f"WhatsApp_{p['name']}.txt").write_text(person_whatsapp(p, lines), encoding="utf-8")
        roles = {r: sum(1 for x in lines if x[2] == r) for r in ("CALLER", "EMPLOYEE")}
        print(f"{p['display']:<18} {len(lines):>3} lines  (caller {roles['CALLER']}, employee {roles['EMPLOYEE']})")
    to_pdf(overview_html(calls, plan, team), PACK / "ECFD_Test2_Overview.pdf")
    print(f"\nWrote {len(team)} sheets + overview to {PACK}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
