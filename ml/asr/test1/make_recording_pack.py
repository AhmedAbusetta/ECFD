"""Build the Test 1 recording pack (PDF for teammates) from scripts.json.

Usage (repo root):  python ml/asr/test1/make_recording_pack.py
Output:             docs/test1/ECFD_Test1_Recording_Scripts.pdf
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
ROOT = HERE.parents[2]
BROWSERS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]


def e(text) -> str:
    return html.escape(str(text))


def build_html(data) -> str:
    cards = []
    for c in data["clips"]:
        tags = []
        if c.get("noise"):
            tags.append("<span class='tag noise'>record with background noise (TV, street, fan)</span>")
        tags.append("<span class='tag'>quiet room</span>" if not c.get("noise") else "")
        cards.append(f"""
<div class="card">
  <div class="head"><span class="cid">{e(c['id'])}</span> {''.join(tags)}</div>
  <div class="ar script">{e(c['text'])}</div>
  <div class="file">Save as: <code>T1_{e(c['id'])}_YourName.m4a</code> <span class="tick">☐ done</span></div>
</div>""")

    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>ECFD Test 1 Recording Scripts</title>
<style>
@page {{ size: A4; margin: 15mm 14mm; }}
body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #1c2430; font-size: 10.5pt; line-height: 1.45; }}
h1 {{ font-size: 19pt; margin: 0 0 2pt; }}
h2 {{ font-size: 13pt; margin: 14pt 0 5pt; border-bottom: 1.5pt solid #1c2430; padding-bottom: 2pt; }}
.sub {{ color: #5b6675; margin-bottom: 10pt; }}
.box {{ border: 1.2pt solid #c9d0d8; background: #f6f8fa; border-radius: 6pt; padding: 8pt 11pt; margin: 6pt 0; }}
.box.consent {{ border-color: #b7791f; background: #fff8eb; }}
ol, ul {{ margin: 3pt 0; padding-left: 17pt; }} li {{ margin: 2.5pt 0; }}
.ar {{ direction: rtl; text-align: right; unicode-bidi: plaintext; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; }}
.card {{ border: 1pt solid #c9d0d8; border-radius: 6pt; padding: 7pt 10pt; margin: 7pt 0; page-break-inside: avoid; }}
.head {{ display: flex; gap: 6pt; align-items: center; }}
.cid {{ font-weight: 800; font-size: 12pt; }}
.tag {{ font-size: 8pt; background: #eef1f5; border-radius: 3pt; padding: 1pt 5pt; color: #3d4754; }}
.tag.noise {{ background: #fdecec; color: #8a1c1c; font-weight: 700; }}
.script {{ font-size: 16pt; line-height: 1.7; margin: 5pt 0 4pt; }}
.file {{ font-size: 8.5pt; color: #5b6675; }}
.tick {{ float: right; font-size: 10pt; color: #1c2430; }}
code {{ font-family: Consolas, monospace; }}
.small {{ font-size: 9pt; color: #5b6675; }}
</style></head><body>

<h1>ECFD — Test 1: Recording Scripts</h1>
<div class="sub">Egyptian Conversational Fraud Defense · 6th of October University · About 5–7 minutes of your time</div>

<div class="box"><b>Why:</b> we are testing how well the speech-to-text engine hears the important words in Egyptian phone calls —
<b>كود، OTP، باسورد، CVV، رقم الكارت، الحساب، تحويل، AnyDesk</b>. Your recordings are the test. You are playing the <b>caller</b>;
most lines sound like a scammer on purpose. Everything is fake — no real numbers or accounts.</div>

<div class="box consent"><b>Consent (please read):</b> by sending these recordings you agree that your voice is used only by the ECFD team
for this graduation project (testing and evaluation), may be sent to the speech-to-text and AI services we test, is stored in the team's private
folder, and is deleted on request or at the end of the project. Don't add anything personal or real (real names, numbers, cards).
If you don't agree, don't record — that's completely fine.</div>

<h2>How to record</h2>
<ol>
<li>Use your phone's <b>Voice Recorder</b> app (any default app is fine). Not WhatsApp voice notes.</li>
<li>Hold the phone <b>like a normal call</b> — next to your face, not far away on a table.</li>
<li>Talk <b>naturally, like a real phone call</b> in Egyptian Arabic — normal speed, not slow and robotic. Say English words (OTP, CVV, AnyDesk, account) the way you normally would.</li>
<li>Say the line <b>exactly as written</b>, especially the important words. If you stumble or change a word, just record the clip again.</li>
<li><b>One file per line.</b> Leave about 1 second of silence before and after you speak.</li>
<li>Quiet room for all lines <b>except C14 and C15</b>: record those with real background noise (TV, street, fan, café).</li>
<li>Don't edit, cut, speed up or filter the audio.</li>
</ol>

<h2>Naming and sending</h2>
<ul>
<li>Name each file <code>T1_C01_YourName.m4a</code>, <code>T1_C02_YourName.m4a</code> … (keep your phone's file type: .m4a, .mp3, .wav are all fine).</li>
<li>Upload all files to the team's shared <b>ECFD Test 1</b> folder (link from the project lead). <b>Not</b> the group chat — chat apps compress audio.</li>
<li>Please record <b>all 15 lines</b>. Different voices are what make the test fair: male and female, different accents, different phones.</li>
<li>Also reply with: your phone model, and whether you're male/female (to check the engine works for everyone). Nothing else.</li>
</ul>
<p class="small ar">باختصار: سجل كل جملة لوحدها من تطبيق التسجيل العادي، والموبايل على ودنك كأنك في مكالمة، واتكلم طبيعي بالمصري. C14 و C15 بس فيهم دوشة في الخلفية. سمي الملفات زي ما هو مكتوب وارفعهم على الفولدر المشترك.</p>

<h2 style="page-break-before: always">The 15 lines</h2>
{''.join(cards)}

<p class="small">Thank you! Questions → project lead. Scripts drafted on 4 October 2026; all numbers in them are fake.</p>
</body></html>"""


def main() -> int:
    data = json.loads((HERE / "scripts.json").read_text(encoding="utf-8"))
    out_dir = ROOT / "docs" / "test1"
    out_dir.mkdir(parents=True, exist_ok=True)
    html_path = out_dir / "ECFD_Test1_Recording_Scripts.html"
    pdf_path = out_dir / "ECFD_Test1_Recording_Scripts.pdf"
    html_path.write_text(build_html(data), encoding="utf-8")

    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("msedge") or shutil.which("chrome")
    if not browser:
        print(f"No Edge/Chrome found; open {html_path} and print it to PDF.")
        return 1
    profile = Path(tempfile.mkdtemp(prefix="ecfd-pdf-"))
    subprocess.run([browser, "--headless", "--disable-gpu", "--no-pdf-header-footer", f"--user-data-dir={profile}",
                    f"--print-to-pdf={pdf_path}", html_path.as_uri()],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
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
