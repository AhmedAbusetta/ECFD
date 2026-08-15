# ECFD — Team Workflow & Sprint Guide
## How Six People Work on One Project at the Same Time

**Document type:** Team Process Guide
**Audience:** All 6 (or 5/7) team members
**Purpose:** Explain, concretely, how everyone works in parallel during every phase of `04_ECFD_Build_Plan.md`, using a sprint rhythm that assumes zero prior team/Agile experience.

This document does not replace the Build Plan. The Build Plan says **what** gets built and **when**. This document says **who is doing what, on which day, and how it doesn't collide.**

---

# 1. The Problem This Document Solves

If you only read `04_ECFD_Build_Plan.md`, the phases look sequential:

```text
Phase 1: VoIP Lab
Phase 2: Media Gateway
Phase 3: ASP.NET Foundation
Phase 5: ASR Baseline
...
```

Read literally, this looks like: "first someone builds the phone system, then someone builds the backend, then someone builds the AI." That would mean 5 people sit idle while 1 person works. **That is not how the project should run.**

In reality, almost every phase needs **all six roles working simultaneously on different parts of the same phase**, because:

- Member 1 (Backend) can build the API and database *before* Member 2 (ASR) has a working model — they agree on a contract (JSON shape) and Member 1 codes against a fake/mock response.
- Member 2, 3, 4 (ASR / NLP / Anti-Spoof) can all build and evaluate their models independently and in parallel — none of them need to wait for each other.
- Member 5 (Data/Eval) can be building the scenario dataset and recording protocol from Week 1, long before there is anything to evaluate.
- Member 6 (Frontend/DevOps) can build the dashboard against fake SignalR events before real ones exist, and can set up Docker Compose from day one.

The technique that makes this possible is **contract-first development**, which is already implicit in `03_ECFD_Technical_Architecture.md` Section 19 ("Event Contracts") and Section 23 ("Integration Rule") of the Build Plan. This document turns that principle into a weekly routine.

---

# 2. The Core Rule: Contracts Unblock Parallel Work

Before any two roles can work at the same time on connected pieces, they need to agree on **the shape of the data that passes between them** — not the implementation.

Example: Member 1 and Member 2 do not need to wait for each other. They need five minutes to agree on this JSON (already defined in `03_ECFD_Technical_Architecture.md` Section 6):

```json
{
  "segmentId": "uuid",
  "text": "...",
  "confidence": 0.94,
  "isFinal": true
}
```

Once that's agreed:

- Member 1 builds the API assuming this JSON exists (using a hardcoded fake response).
- Member 2 builds the real ASR service that must eventually return this exact JSON.

They integrate later in a scheduled **integration slot** (see Section 6), not continuously.

**Rule of thumb:** if two people are blocked waiting on each other, the actual problem is almost always a missing or unclear contract — not a scheduling problem.

---

# 3. Sprint Length and Rhythm

Recommended: **2-week sprints**, aligned to the Build Plan's phase windows (most phases are already sized in 2–6 week blocks).

Each sprint has the same weekly shape:

```text
MONDAY (Week 1)      Sprint Planning (45–60 min)
TUE–FRI (Week 1)      Individual work + 15-min daily standup
MONDAY (Week 2)       Mid-sprint check (async, 10 min)
TUE–THU (Week 2)      Individual work + 15-min daily standup
FRIDAY (Week 2)       Integration Session (60–90 min, mandatory, everyone present)
FRIDAY (Week 2)       Sprint Review + Retro (30 min)
```

If a 2-week cadence feels too fast early on (Phase 0–2, when telephony setup can be unpredictable), it's fine to run the first sprint as 3 weeks. Keep every sprint after that at 2 weeks — consistency matters more than speed.

---

# 4. The Four Rituals (Kept Deliberately Small)

A team that has never done Agile before does not need the full Scrum ceremony list. Use these four only:

### 4.1 Sprint Planning (start of sprint, 45–60 min, everyone)

Answer three questions as a group:

1. What does the Build Plan say this phase requires?
2. What contract(s) need to be agreed on *this sprint* so people aren't blocked?
3. What is each person committing to finish by Friday of week 2?

Output: a shared task board (see Section 7) with one card per person per deliverable.

### 4.2 Daily Standup (15 min, async is fine)

Each person answers three lines in the team chat (voice or text, doesn't need a meeting):

```text
1. What I finished since yesterday
2. What I'm doing today
3. What is blocking me (if anything)
```

The only rule: if someone posts a blocker, whoever can unblock them responds within a few hours — this is the mechanism that prevents "I was stuck for 3 days and didn't say anything."

### 4.3 Integration Session (end of sprint, 60–90 min, mandatory, everyone present)

This is the most important ritual and the one inexperienced teams skip — don't skip it.

Everyone brings their piece and it gets connected live, in the same room/call:

```text
Member 1 runs the backend
Member 2 points real or test audio at it
Member 3's NLP service is called with real transcript text
Member 4's anti-spoof service is called with a real audio window
Member 6's dashboard is open and watching for events
```

If something breaks, it breaks *here*, together, with everyone who can fix it in the room — not two weeks later during Phase 14 (Evaluation) when nobody remembers why the ASR service returns the wrong field name.

### 4.4 Sprint Review + Retro (30 min, everyone)

- **Review (15 min):** show what actually works now, end-to-end if possible.
- **Retro (15 min):** two questions only — "what slowed us down this sprint?" and "what's one thing we change next sprint?" Write the one change down. Don't let this become a complaint session; it should always end with one concrete action.

---

# 5. Parallel Work Map — What Everyone Does, Phase by Phase

This maps `04_ECFD_Build_Plan.md`'s phases onto simultaneous work for all six roles, using the ownership boundaries from `02_ECFD_Team_Organization.md`. Use this as the starting point for each Sprint Planning session, not as a rigid script — adjust based on actual progress.

| Build Plan Phase | M1 Backend | M2 ASR | M3 NLP | M4 Anti-Spoof | M5 Data/Eval/Security | M6 Frontend/DevOps |
|---|---|---|---|---|---|---|
| **0 — Freeze Scope** (Wk 1) | Draft repo structure, propose event contracts | Confirm Python/model versions to freeze | Confirm tactic taxonomy draft | Confirm anti-spoof approach/dataset licensing | Draft scenario/annotation format | Set up repo, CI skeleton, Docker Compose skeleton |
| **1 — VoIP Lab** (Wk 2–4) | Study ARI/PJSIP, plan session model | Start ASR model survey (offline, no dependency on telephony) | Start tactic taxonomy + annotation guideline doc | Start anti-spoof model survey | Design scenario dialogue format, write first 3 scenario scripts | Set up Asterisk container, help M1 test SIP endpoints |
| **2 — Media Gateway** (Wk 4–6) | Build gateway skeleton (receive/normalize/window) | Build ASR FastAPI skeleton returning a fake transcript | Build NLP FastAPI skeleton returning a fake tactic list | Build anti-spoof FastAPI skeleton returning a fake score | Finalize annotation schema; label 1 scenario end-to-end as a test | Docker Compose wiring for all skeleton services |
| **3 — ASP.NET Foundation** (Wk 6–8) | Build CallSession/Evidence/Risk/Alert models, EF Core, SignalR hub | Continue ASR model benchmarking (WER on sample audio) | Continue NLP baseline (keyword rules) | Continue anti-spoof baseline evaluation | Build 5–10 more scenario scripts across attack types | Build dashboard shell subscribing to SignalR with fake data |
| **4 — E2E Skeleton** (Wk 8–10) | Wire gateway → backend → SignalR with dummy values | Confirm ASR contract against real API (still fake output ok) | Confirm NLP contract against real API | Confirm anti-spoof contract against real API | Start test/validation split plan | Get dashboard showing a *real* live call session (Critical Milestone) |
| **5 — ASR Baseline** (Wk 10–14) | Build ML orchestrator adapter for ASR | Integrate real pretrained ASR, measure WER/latency | Continue NLP baseline vs. transformer comparison plan | Continue anti-spoof integration against real pipeline | Prepare labeled audio samples for ASR benchmarking | Wire live transcript into dashboard UI |
| **6 — NLP Baseline** (Wk 12–16) | Continue orchestrator, wire NLP into evidence pipeline | Keep improving ASR robustness (noise, code-switching) | Ship rule-based baseline, start classifier comparison | Continue anti-spoof dataset evaluation | Support NLP evaluation (precision/recall harness) | Show live tactics in dashboard |
| **7 — Dataset Construction** (Wk 14–20) | Support as needed; mostly stable | Support labeling audio quality issues | Help write attack dialogue variations | Help write voice/replay scenario variations | **Owns this phase**: full scenario set, annotations, versioning | Support with any tooling needed for dataset review |
| **8 — NLP Training** (Wk 16–22) | Stable; monitor integration | Continue ASR robustness work | **Owns this phase**: fine-tune classifier, track experiments | Continue anti-spoof integration/eval | Support with train/val/test split integrity | Stable |
| **9 — Anti-Spoofing** (Wk 18–24) | Stable; monitor integration | Stable; monitor integration | Stable; support NLP calibration | **Owns this phase**: integrate real anti-spoof model | Support anti-spoof evaluation (EER etc.) | Show voice evidence panel in dashboard |
| **10 — Attack Progression** (Wk 22–28) | **Owns this phase**: build state machine, wire to evidence | Support as needed | Support tactic-to-transition mapping | Support voice-evidence-to-transition mapping | Write transition test cases per scenario | Show attack progression view in dashboard |
| **11 — Risk Fusion** (Wk 26–30) | **Owns this phase**: deterministic weighted fusion, risk snapshots | Support as needed | Support content-risk weighting | Support voice-risk weighting | Help tune/validate weights against labeled scenarios | Show risk score + contributors panel |
| **12 — Context Engine** (Wk 28–32) | **Owns this phase**: small context engine (endpoint, time, direction) | — | — | — | Define what context signals are testable | Reflect context in dashboard if relevant |
| **13 — Dashboard** (Wk 30–34) | Support API needs for final views | — | — | — | — | **Owns this phase**: full live dashboard build-out |
| **14 — Evaluation** (Wk 34–38) | Support as needed | Run ASR eval on held-out set | Run NLP eval on held-out set | Run anti-spoof eval on held-out set | **Owns this phase**: freeze test set, run system-level metrics | Support with any reporting visuals |
| **15 — Ablation Study** (Wk 38–42) | Support toggling components on/off for ablation runs | Support | Support | Support | **Owns this phase**: run and document ablation comparisons | — |
| **16 — Hardening** (Wk 42–46) | **Owns this phase**: graceful degradation, failure handling | Handle ASR service failure modes | Handle NLP service failure modes | Handle anti-spoof service failure modes | Support with failure-scenario test cases | Handle stale SignalR sessions, dashboard error states |
| **17 — Deployment** (Wk 46–50) | Support docker-compose/migrations | Package ASR service startup script | Package NLP service startup script | Package anti-spoof service startup script | Seed data / demo scenario scripts | **Owns this phase**: docker-compose, env, one-command startup |
| **18 — Freeze & Demo** (Wk 50–52) | All hands: rehearse, fix only what's broken | All hands | All hands | All hands | Owns: evaluation graphs, limitations writeup | Owns: demo run-through, backup mode readiness |

**How to read this table:** in almost every row, at least 4–5 people have real, independent work. Nobody should ever be "waiting for Phase 5 to start ASR" — ASR work (model survey, benchmarking) starts in Phase 1, long before the pipeline exists to plug it into.

---

# 6. Integration Slots (Where Parallel Work Reconnects)

Parallel work only stays parallel if it reconnects on a schedule — otherwise pieces drift apart and integration becomes a nightmare at the end (exactly what Build Plan Phase 4 and Section 23 "Integration Rule" are trying to prevent).

Two integration mechanisms:

### 6.1 Weekly Integration Session (Section 4.3)
Every sprint ends with everyone plugging their real components into the shared environment together.

### 6.2 Continuous Integration (automated)
Set this up in Phase 0/1 (Member 6 owns it):

```text
On every pull request:
  - build backend
  - run backend unit tests
  - run ML service schema/contract tests
  - lint frontend
```

This catches "I changed the JSON field name and broke someone else" the moment it happens, not two weeks later.

---

# 7. Task Board (Keep It Simple)

Use a free Kanban board (GitHub Projects, Trello, or similar). Four columns only:

```text
BACKLOG  →  IN PROGRESS  →  IN REVIEW  →  DONE
```

Rules:

- Every card has **one owner** (matches the "one owner" principle in `02_ECFD_Team_Organization.md` Section 13).
- Every card references which **phase** it belongs to (from the Build Plan) and, if relevant, which **contract** it depends on.
- A card only moves to DONE when it matches the Integration Rule from `04_ECFD_Build_Plan.md` Section 23: contract + example request/response + error behavior + test + version number.
- Cards should be small enough to finish in 1–4 days. If a card will take longer than a week, split it.

---

# 8. Git Workflow (Matches `ECFD_Engineering_Specification_v1_0.md`)

```text
main                      → always working, always demo-able
feature/<short-name>      → one branch per task, e.g. feature/signalr-dashboard
```

Rules for a first-time team:

1. Never commit directly to `main`.
2. Open a pull request even if you're the only one who will review it at first — it creates a record and a CI run.
3. Every PR description answers: **What changed? Why? Tests? Contract changes? Screenshots if UI?**
4. At least one other teammate approves before merging — ideally the person on the other side of the contract you touched.
5. Merge to `main` only when it builds and passes CI.

---

# 9. Communication Structure

| Channel | Purpose |
|---|---|
| Team chat (WhatsApp/Discord/Slack) | Daily standups, quick questions, blockers |
| Shared doc (this pack) | Source of truth for contracts, scope, decisions |
| Task board | Source of truth for who is doing what right now |
| Weekly Integration Session | Source of truth for what actually works |

**Decision rule:** if a decision changes a contract (a JSON shape, an API route, a database column), it must be written into `03_ECFD_Technical_Architecture.md` (or this doc) before anyone codes against it — not agreed verbally and forgotten.

---

# 10. Common First-Team Mistakes (and the Fix)

| Mistake | Why it happens | Fix |
|---|---|---|
| Everyone waits for Member 1 to "finish the backend" first | Treating phases as sequential instead of contract-first | Agree the contract in 5 minutes, build against a fake response, integrate later |
| Two people quietly build the same thing differently | No task board, no single owner | Every task has exactly one owner on the board |
| Nobody notices integration is broken until Week 40 | Skipping the weekly Integration Session | Never skip Friday's integration session, even if only 2 pieces are ready |
| Standups turn into 45-minute meetings | No structure | Keep the 3-line format; take longer discussions offline into a separate thread |
| A blocked person suffers in silence for days | No visible signal | Blockers must be posted in the daily standup — that's the whole point of the ritual |
| Scope creep ("let's also detect deepfake video") | No owner for scope decisions | Anything outside `01_ECFD_Project_Scope.md` Section 5 needs explicit team agreement, not solo initiative |
| Contract changes break someone else's work silently | No versioning discipline | Follow Section 19 of `03_ECFD_Technical_Architecture.md`: every event/contract is versioned; breaking changes get a new version, not a silent edit |

---

# 11. Standup Template (copy/paste into chat)

```text
[Name] — [Date]
Done: ...
Doing: ...
Blocked by: ... (or "nothing")
```

# 12. PR Template (copy/paste into PR description)

```text
What changed:
Why:
Tests added/updated:
Contract changes (if any):
Screenshots (if UI):
```

# 13. Sprint Planning Template

```text
Sprint #: ___   Phase(s) covered: ___   Dates: ___ to ___

Contracts to agree/lock this sprint:
- ...

Per-person commitment:
- M1: ...
- M2: ...
- M3: ...
- M4: ...
- M5: ...
- M6: ...

Integration session date/time: ___
```

---

# 14. Summary

The Build Plan tells you *what* to build, in what order. This document tells you *how six people build it at the same time* without stepping on each other:

1. Agree contracts before code, not after.
2. Build against fake data until the real thing exists.
3. Work in 2-week sprints with four small rituals: planning, daily standup, integration session, review/retro.
4. Never skip the Friday integration session — that is where parallel work becomes one working system.
5. One owner per task, tracked on a simple board.
6. If two people are blocked on each other, the fix is almost always a clearer contract, not a longer meeting.
