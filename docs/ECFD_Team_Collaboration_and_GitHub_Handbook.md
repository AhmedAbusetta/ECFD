# ECFD — Team Collaboration & GitHub Handbook

**Document type:** Team Process & Engineering Practice Guide
**Audience:** All 6 (or 5/7) team members
**Purpose:** The single reference for how the team actually uses GitHub, day to day, from the first commit to the graduation freeze. This document assumes no prior team-Git experience and is meant to be followed literally, not interpreted.

This handbook does not replace `04_ECFD_Build_Plan.md` (what gets built, and when), `06_ECFD_Team_Workflow_and_Sprints.md` (the sprint rhythm and rituals), or `ECFD_Engineering_Specification_v1_0.md` (the implementation spec). It is the mechanical layer underneath all three: exactly how a change goes from an idea in someone's head to a merged, working piece of ECFD.

---

# 1. GitHub Repository Setup

## 1.1 One repository, monorepo style

ECFD uses a single GitHub repository, matching the structure already defined in `04_ECFD_Build_Plan.md` Section 22 and `ECFD_Engineering_Specification_v1_0.md` Section 2:

```text
ecfd/
├── backend/
│   ├── ECFD.Api/
│   ├── ECFD.Application/
│   ├── ECFD.Domain/
│   ├── ECFD.Infrastructure/
│   └── ECFD.Tests/
├── ml/
│   ├── asr/
│   ├── nlp/
│   └── antispoof/
├── frontend/
├── telephony/
│   └── asterisk/
├── infra/
│   └── docker/
├── data/
│   ├── schemas/
│   └── documentation/
└── docs/
```

A monorepo is deliberate for a 6-person student team: one issue tracker, one project board, one PR history, one place contracts live. Splitting into multiple repos early adds coordination overhead the team doesn't need yet.

## 1.2 Repository settings (set these in Week 1)

- **Visibility:** Private, until the team has explicitly decided (see the companion `ECFD_IP_Ownership_and_Protection_Guide.md`) what, if anything, becomes public before graduation.
- **Default branch:** `main`, protected (see Section 2.3).
- **Collaborators:** Add all team members as members with write access. Add the supervisor as a read-only collaborator if they want visibility — do not give write access to non-team members.
- **`.gitignore`:** Add language-appropriate ignores on day one — `bin/`, `obj/`, `__pycache__/`, `.venv/`, `node_modules/`, `.env`, model checkpoint files, audio sample directories over a sane size. Nothing large or secret should ever reach a commit.
- **`.gitattributes`:** Normalize line endings (`* text=auto`) since the team will mix Windows and non-Windows machines.
- **README.md at repo root:** One page — what ECFD is, link to `/docs`, and the exact commands to get the environment running (this becomes real once Phase 17 / Section 41 of the Build Plan and Engineering Spec is reached; keep a "coming soon" stub until then).

## 1.3 `/docs` folder

Copy the eight planning documents (this handbook, the IP guide, and the six numbered docs) into `/docs` in Week 1, and keep them there as the living source of truth. Contract changes, scope changes, and role changes get edited into these files — not just discussed in chat and forgotten. `06_ECFD_Team_Workflow_and_Sprints.md` Section 9 already establishes this rule for contracts specifically; this handbook extends it to the whole documentation set.

---

# 2. Branching Strategy

## 2.1 Branch model

```text
main                    → always working, always demo-able
feature/<short-name>    → one branch per task
fix/<short-name>        → one branch per bug fix
```

This matches the simplified model in `06_ECFD_Team_Workflow_and_Sprints.md` Section 8. A `develop` branch is optional; for a 6-person team shipping every 2 weeks, it usually adds more overhead than it saves. If the team later finds `main` breaking too often, introduce `develop` as an integration branch and only promote to `main` after the Friday Integration Session — but don't start there.

## 2.2 Naming convention

```text
feature/signalr-dashboard
feature/ari-call-session
feature/rtp-media-gateway
feature/asr-client
feature/risk-engine
fix/edit-action-null-reference
fix/ef-migration-type-mismatch
```

Prefix with the area when it helps disambiguate in a monorepo: `feature/backend-risk-snapshot`, `feature/ml-nlp-baseline`, `feature/frontend-progression-view`.

## 2.3 Branch protection on `main`

Enable, from Week 1:

- Require a pull request before merging (no direct pushes).
- Require at least 1 approving review.
- Require status checks (CI) to pass before merging.
- Require branches to be up to date before merging.

## 2.4 Branch lifetime

- Keep feature branches short-lived: 1–4 days, matching the card-sizing rule in `06_ECFD_Team_Workflow_and_Sprints.md` Section 7. If a branch is still open after a week, either split the work or merge behind a flag.
- Delete branches after merge (GitHub can do this automatically on merge).
- Rebase or merge `main` into a long-running feature branch regularly to avoid painful end-of-sprint conflicts.

---

# 3. Issues and Project Board

## 3.1 Board structure

Use GitHub Projects (or Trello if the team prefers a lighter tool), with the four columns already defined in `06_ECFD_Team_Workflow_and_Sprints.md` Section 7:

```text
BACKLOG  →  IN PROGRESS  →  IN REVIEW  →  DONE
```

## 3.2 Issue rules

- Every issue has **one assignee** (the "one owner" principle from `02_ECFD_Team_Organization.md` Section 13).
- Every issue references which **Build Plan phase** it belongs to, and which **contract** (if any) it depends on or defines.
- Every issue is small enough to finish in 1–4 days. Split anything bigger.
- Use labels for area: `backend`, `asr`, `nlp`, `antispoof`, `frontend`, `telephony`, `data`, `infra`, `docs`.
- Use labels for type: `contract`, `bug`, `spike` (exploration/research), `chore`.

## 3.3 Issue template (suggested)

```text
### What
One sentence describing the outcome.

### Build Plan phase
Phase N — <name>

### Contract dependency (if any)
Link to the contract section in 03_ECFD_Technical_Architecture.md
or ECFD_Engineering_Specification_v1_0.md

### Definition of done
- [ ] Implementation
- [ ] Tests
- [ ] Docs updated if contract changed
```

## 3.4 A card only moves to DONE when

It matches the Integration Rule already stated in `04_ECFD_Build_Plan.md` Section 23: **contract + example request/response + error behavior + test + version number.** This is not optional — it's the rule that prevents "works on my laptop" integration failures later.

---

# 4. Pull Requests and Code Reviews

## 4.1 PR rules

1. Never commit directly to `main`.
2. Open a PR even for solo-reviewed work early on — it creates a record and triggers CI.
3. Every PR description answers the four questions from the PR template (Section 4.2).
4. At least one teammate approves before merging — ideally the person on the other side of any contract the PR touches (e.g., the NLP owner reviews a backend PR that consumes the NLP contract).
5. Merge only when the branch is up to date and CI passes.
6. Prefer small PRs. A PR that touches five unrelated things is hard to review honestly.

## 4.2 PR template

```text
## What changed
...

## Why
...

## Tests added/updated
...

## Contract changes (if any)
...

## Screenshots (if UI)
...
```

This is the exact template from `06_ECFD_Team_Workflow_and_Sprints.md` Section 12 and `ECFD_Engineering_Specification_v1_0.md` Section 43 — kept identical across documents so it's copy-pasteable without translation.

## 4.3 Code review etiquette

- Review within a working day. A PR sitting unreviewed for a week defeats the purpose of small PRs.
- Comment on the code, not the person. "This will throw if `segmentId` is null" not "you forgot null checks again."
- The reviewer's job is to check: does this match the agreed contract, does it have tests, is it reasonably readable — not to rewrite the author's style preferences.
- If a review reveals a contract disagreement, stop and resolve the contract question in `03_ECFD_Technical_Architecture.md` first, not in PR comments. Contract changes get written down (Section 8 below), not negotiated inline in a diff.

---

# 5. Commit Conventions

## 5.1 Format

Use short, present-tense, conventional-style commit messages:

```text
<type>(<scope>): <short summary>

<optional longer body>
```

Types:

```text
feat     — new feature
fix      — bug fix
refactor — code change that doesn't change behavior
test     — adding or fixing tests
docs     — documentation only
chore    — tooling, dependencies, config
```

Examples:

```text
feat(backend): add CallSessionManager with in-memory session dictionary
fix(migrations): correct type mismatch between domain model and EF entity
feat(nlp): add rule-based tactic baseline for OTP_REQUEST
docs(architecture): add v2 transcript.final event contract
```

## 5.2 Rules

- One logical change per commit where practical; don't squash three unrelated fixes into one commit message.
- Reference the issue number when relevant: `fix(backend): resolve null reference in edit action (#42)`.
- Don't commit commented-out code, debug prints, or secrets. If a secret is accidentally committed, treat it as compromised (rotate it) even after removing it from history — see Section 11.

---

# 6. Team Roles and Ownership

This section is a pointer, not a duplicate — the authoritative role definitions live in `02_ECFD_Team_Organization.md`. What this handbook adds is how ownership maps onto GitHub mechanics:

| Role (from `02_ECFD_Team_Organization.md`) | GitHub responsibility |
|---|---|
| M1 — Backend & Real-Time Systems Lead | Owns `backend/`, reviews contract-touching PRs across ML boundaries, maintains `03_ECFD_Technical_Architecture.md` event contracts |
| M2 — Speech AI / ASR | Owns `ml/asr/`, owns the ASR HTTP contract and its versioning |
| M3 — NLP | Owns `ml/nlp/`, owns the NLP contract and taxonomy doc |
| M4 — Voice Security / Anti-Spoofing | Owns `ml/antispoof/`, owns the anti-spoof contract |
| M5 — Data, Evaluation & Security | Owns `data/`, owns the CI security checks (secret scanning, dependency checks), owns the evaluation reports folder |
| M6 — Frontend & DevSecOps | Owns `frontend/`, `infra/`, CI pipeline itself, Docker Compose, branch protection settings |

**The "one owner" principle** (`02_ECFD_Team_Organization.md` Section 13) applies at the folder level, not just the task level: a PR that changes files inside `ml/nlp/` should be authored or reviewed by M3, even if M1 is the one who requested the change.

---

# 7. Working in Parallel

The mechanics of parallel work — contract-first development, the weekly Integration Session, the phase-by-phase parallel work map — are fully specified in `06_ECFD_Team_Workflow_and_Sprints.md` Sections 2 through 6. This handbook does not repeat that content; read it there.

What belongs here is the GitHub-specific consequence of that model:

- **Mock/skeleton services get merged to `main` early**, even though they return fake data. A fake-but-contract-shaped ASR service merged in Week 4 is more valuable than a perfect one that doesn't exist until Week 12, because it unblocks the backend and frontend immediately.
- **Every skeleton PR is labeled `mock`** so the team can find and later replace every stubbed component (see Section 9).
- **Integration Session outcomes get written down** as GitHub issues immediately, not left as verbal notes — "M2's real ASR output doesn't match the mocked contract's `confidence` field type" becomes a same-day issue, not a task someone remembers two weeks later.

---

# 8. API / ML Contracts

## 8.1 Where contracts live

The authoritative contracts are `03_ECFD_Technical_Architecture.md` (Sections 6–8, 13, 19) and `ECFD_Engineering_Specification_v1_0.md` (Sections 14, 16, 19–20). GitHub's job is to enforce that code never silently drifts from what's written there.

## 8.2 Contract change process

1. Anyone proposing a contract change opens an issue tagged `contract`, describing the current shape, the proposed shape, and why.
2. The owning role (per Section 6) and the consuming role both weigh in before it's accepted — a contract has (at minimum) one producer and one consumer, and both must agree.
3. Once agreed, the change is committed to `03_ECFD_Technical_Architecture.md` (or `ECFD_Engineering_Specification_v1_0.md`) in the same PR that implements it, or in a `docs`-only PR that lands first.
4. **Breaking changes get a new version, not a silent edit** — `transcript.final.v1` becomes `transcript.final.v2`, both may coexist during a transition, per `03_ECFD_Technical_Architecture.md` Section 19.

## 8.3 Contract tests

Every service that implements a contract should have a schema/contract test that fails CI if the actual response shape drifts from the documented one. This is the automated version of the rule in `06_ECFD_Team_Workflow_and_Sprints.md` Section 6.2 — catching a renamed JSON field the moment it happens, not two sprints later.

---

# 9. Mock Services

## 9.1 Why mocks are a first-class deliverable

`06_ECFD_Team_Workflow_and_Sprints.md` Section 2 establishes contract-first development as the mechanism that lets all six roles work simultaneously. Mock/skeleton ML services are the concrete artifact of that principle — they are not throwaway code, they are the thing that unblocks the rest of the team.

## 9.2 Mock service rules

- Each ML service (`ml/asr`, `ml/nlp`, `ml/antispoof`) should have a `--mock` mode (or a separate lightweight FastAPI app) that returns realistic, contract-shaped fake data without loading any real model.
- Mock responses should include some variation (not always the exact same fake transcript) so downstream UI/logic isn't accidentally built assuming a static value.
- Docker Compose should default to mock mode locally unless a developer explicitly opts into real models — this keeps the whole stack runnable on a laptop without a GPU, matching `03_ECFD_Technical_Architecture.md` Section 22's acknowledgment that not every dev machine needs a GPU.
- Track which components are still mocked with a simple checklist in `docs/mock-status.md`, updated at every Integration Session. A component only leaves this list when its real implementation has passed the Integration Session with real data.

---

# 10. Database Migration Rules

## 10.1 Ownership

M1 (Backend & Real-Time Systems Lead) owns the EF Core migration history end to end, per `02_ECFD_Team_Organization.md` Section 3. No one else adds a migration without M1's review, since migrations are strictly ordered and conflicting migration branches are one of the most common sources of "it works on my machine" failures in student projects.

## 10.2 Rules

1. **One migration per PR.** Don't bundle an unrelated schema change into a migration meant for something else.
2. **Never edit a migration that has already been merged to `main` and applied anywhere shared** (a teammate's machine, a shared dev database). If the schema needs to change further, add a new migration.
3. **Every migration PR includes:** the generated migration file, the reason for the change, and — if it changes a table referenced in `03_ECFD_Technical_Architecture.md` Section 18 or `ECFD_Engineering_Specification_v1_0.md` Section 18 — an update to that schema documentation in the same PR.
4. **Rebase before generating a migration**, not after — always generate migrations against the latest `main` schema to avoid EF Core producing a migration that silently reverts a teammate's recent column addition.
5. **Seed data lives in a separate, idempotent seed script**, not baked into schema migrations, so demo data can be reset without touching schema history.
6. **Before the Friday Integration Session**, everyone pulls `main` and applies the latest migrations against a clean local database as a sanity check — this is the fastest way to catch a broken or non-reproducible migration before it becomes a Phase 17 (Reproducible Deployment) problem.

---

# 11. Secrets and Security

This section operationalizes `03_ECFD_Technical_Architecture.md` Section 23 and `ECFD_Engineering_Specification_v1_0.md` Section 25 for GitHub specifically.

## 11.1 Never commit secrets

- SIP credentials, database passwords, ML service auth tokens, API keys — none of these go in the repository, ever, including in commit history, comments, or example config files with "just for now" real values.
- Use `.env` files (git-ignored) locally, and a documented `.env.example` with placeholder values committed to the repo, matching `04_ECFD_Build_Plan.md` Phase 17 deliverables.
- Enable GitHub secret scanning on the repository (available free on private repos for public-known secret patterns) as an automated backstop.

## 11.2 If a secret is committed anyway

1. Rotate the secret immediately — assume it is compromised the moment it hits a pushed commit, even on a private repo, even if removed in a later commit.
2. Remove it from history (`git filter-repo` or GitHub's secret-removal guidance) as a secondary step, not the primary fix.
3. Post it in the team standup channel as a blocker/incident so everyone is aware, per the blocker-visibility rule in `06_ECFD_Team_Workflow_and_Sprints.md` Section 4.2.

## 11.3 CI security checks (M5 + M6 own this)

```text
On every pull request:
  - secret scanning
  - dependency vulnerability check (npm audit / dotnet list package --vulnerable / pip-audit)
  - lint
  - build
  - unit tests
```

This extends the CI list already defined in `06_ECFD_Team_Workflow_and_Sprints.md` Section 6.2 with the security-specific checks called for in the architecture documents.

## 11.4 Least privilege

- Database credentials used by the application are least-privilege (no superuser), per `03_ECFD_Technical_Architecture.md` Section 23.
- ML service-to-service auth uses a shared token or mutual auth appropriate for a controlled demo network — not left open on the local network, even for a university prototype.
- No real customer data, real OTPs, or real banking credentials ever enter the repository, sample data, or demo recordings, per Section 25 of the Engineering Spec. All demo scenarios use fabricated identities and fabricated "banks."

---

# 12. Dataset and Model Versioning

Ownership of the dataset itself belongs to M5 (Data, Evaluation & Security) per `02_ECFD_Team_Organization.md` Section 7 and `04_ECFD_Build_Plan.md` Phase 7. This section covers how that ownership is reflected in the repository.

## 12.1 What goes in Git, and what doesn't

- **In Git:** scenario scripts, annotation schemas, dataset documentation, small sample clips used for automated tests, model version manifests.
- **Not in Git (too large / binary-heavy):** full audio recordings, trained model checkpoints. Use `.gitignore` plus one of:
  - A `data/` mount documented in `.env.example` pointing at a shared drive or cloud bucket the team already has access to.
  - Git LFS if the team wants versioned large files inside the same repo and file sizes stay reasonable.

## 12.2 Dataset versioning

Every dataset used for training or evaluation gets a version tag recorded alongside the experiment, per `04_ECFD_Build_Plan.md` Phase 8's requirement to track "dataset version, model, hyperparameters, training seed, metrics, model artifact" for every experiment:

```text
data/
  documentation/
    dataset-v1.md   ← what's in it, scenario count, split sizes, known issues
    dataset-v2.md
```

Never silently edit a dataset version already used in a reported result. Create a new version and note what changed.

## 12.3 Train/validation/test split integrity

The split boundary is decided once by M5, documented, and never changed after evaluation has started — per `04_ECFD_Build_Plan.md` Phase 7's rule against training and testing on identical recordings, and `02_ECFD_Team_Organization.md` Section 12's rule that Data/Evaluation must not "mix test data into training" or "change evaluation methodology after seeing results." Store the split assignment itself (which scenario IDs are train/val/test) as a versioned file, not something regenerated randomly each run.

## 12.4 Model versioning

Every model artifact referenced anywhere in the system (ASR, NLP, anti-spoof) has a `modelVersion` string, per `03_ECFD_Technical_Architecture.md` Section 19. Keep a `model_versions` log (mirroring the `model_versions` database table from Section 18) recording, per version: training date, dataset version used, headline metrics, and where the artifact is stored. This is what makes the ablation study in `04_ECFD_Build_Plan.md` Phase 15 reproducible.

---

# 13. Weekly Workflow

The full sprint rhythm — sprint planning, daily standup, integration session, review/retro — is defined in `06_ECFD_Team_Workflow_and_Sprints.md` Sections 3–4 and should be read there in full. Summarized for GitHub context:

```text
MONDAY (Wk 1)     Sprint Planning → issues created/assigned on the board
TUE–FRI (Wk 1)    Daily standup in chat; PRs opened and reviewed as work completes
MONDAY (Wk 2)     Async mid-sprint check
TUE–THU (Wk 2)    Continued work; PRs merged as reviewed
FRIDAY (Wk 2)     Integration Session — real components connected live
FRIDAY (Wk 2)     Sprint Review + Retro; board reset for next sprint
```

**GitHub-specific addition:** before every Integration Session, everyone should have their sprint's PRs merged to `main` (or clearly flagged as still-in-review with a reason). Walking into an Integration Session with three unmerged PRs defeats the purpose of the session.

---

# 14. Architecture Decision Records (ADRs)

## 14.1 What qualifies as a decision worth recording

Not every choice needs an ADR. Record one when a decision:

- Changes a contract, a database table, or a service boundary.
- Chooses between two technically reasonable approaches (e.g., "streaming ASR vs. windowed HTTP calls," "deterministic risk fusion vs. learned fusion" — see `03_ECFD_Technical_Architecture.md` Section 15).
- Would be genuinely confusing to a future team member (or examiner) without the reasoning behind it.

## 14.2 Location and format

Store ADRs in `docs/adr/`, one file per decision, numbered sequentially:

```text
docs/adr/0001-external-media-over-packet-sniffing.md
docs/adr/0002-deterministic-risk-fusion-v1.md
docs/adr/0003-postgresql-over-mongodb.md
```

Minimal template:

```text
# ADR NNNN — <title>

## Status
Proposed / Accepted / Superseded by ADR-XXXX

## Context
What problem or choice prompted this?

## Decision
What was decided?

## Consequences
What does this make easier or harder later?
```

## 14.3 Why this matters for ECFD specifically

Several architecture documents already flag decisions that should be ADRs the moment they're made: the External Media vs. raw packet sniffing choice (`03_ECFD_Technical_Architecture.md` Section 2), the deterministic-vs-learned risk fusion choice (Section 15), and the decision to keep the anti-spoof signal secondary rather than dominant (`ECFD_Engineering_Specification_v1_0.md` Section 37). Writing these down turns them into evidence the team can cite during the graduation discussion (`05_ECFD_Discussion_Day_Demo.md` Section 14) instead of having to reconstruct the reasoning from memory in front of the examiner.

---

# 15. Release Process

ECFD is a graduation prototype, not a shipping product, so "release" here means **a tagged, demo-ready checkpoint**, not a public release channel.

## 15.1 Tagging convention

```text
v0.1.0   — first end-to-end skeleton with dummy data (Build Plan Phase 4)
v0.2.0   — real ASR wired in (Phase 5)
v0.3.0   — real NLP wired in (Phase 6)
v0.4.0   — anti-spoof + progression + risk wired in (Phases 9–11)
v0.5.0   — dashboard feature-complete (Phase 13)
v0.9.0   — hardened, evaluation complete (Phases 14–16)
v1.0.0   — freeze for graduation demo (Phase 18)
```

## 15.2 Tagging rules

- Tag on `main` only, after CI passes.
- Every tag has a short release note in the tag description: what works now that didn't before, and any known limitations.
- After `v1.0.0` (the Freeze in `04_ECFD_Build_Plan.md` Phase 18), no new tags until after the graduation defense — only hotfixes, and only if the demo itself is broken.

## 15.3 Pre-demo checklist

Before tagging a version intended for a live demonstration or rehearsal, run through `05_ECFD_Discussion_Day_Demo.md` Section 3 (Demo Preparation) in full: telephony, AI warm-up, backend health, dashboard connection.

---

# 16. Backups

A graduation project has no ops team and no second chance on demo day, so backups are not optional.

## 16.1 What to back up

```text
Repository        → GitHub is the primary backup; additionally, at least one
                     team member should keep a local clone up to date.
Database           → scheduled pg_dump before/after every Integration Session,
                     and always immediately before a demo rehearsal.
Model artifacts    → stored outside Git (Section 12.1); keep at least the last
                     two known-good versions of each model, not only the latest.
Demo recordings    → the backup-mode media described in
                     05_ECFD_Discussion_Day_Demo.md Section 12 must be backed
                     up in at least two locations (a laptop and a cloud drive).
Environment config → .env.example plus a private, securely shared copy of the
                     real .env values (not in Git — see Section 11).
```

## 16.2 Backup demo mode

`05_ECFD_Discussion_Day_Demo.md` Section 12 already defines the requirement: a pre-recorded, authorized call media path that still runs through the real backend, real models, real risk engine, and real dashboard if the live SIP call fails on demo day. Treat the backup-mode recording and its integration test as a tracked deliverable on the board (owned by M6, per the Workflow doc's Phase 17 table), not an afterthought assembled the night before.

---

# 17. What to Do When Something Breaks

## 17.1 During development (any day)

1. Post the blocker in the daily standup channel immediately — don't wait a day, per `06_ECFD_Team_Workflow_and_Sprints.md` Section 4.2.
2. If it's a contract mismatch, open a `contract`-labeled issue and tag both the producing and consuming role.
3. If `main` is broken (CI red after a merge), the person whose merge broke it fixes it or reverts within the same day — a broken `main` blocks everyone, so it takes priority over new feature work.

## 17.2 During an Integration Session

Per `06_ECFD_Team_Workflow_and_Sprints.md` Section 4.3, this is where breakage is expected and wanted — it's better to find it here than during Phase 14 (Evaluation) or on demo day. If something breaks:

1. Whoever owns either side of the broken contract stays and debugs it live, with help from anyone available.
2. If it can't be fixed in the session, it becomes the top-priority issue for the next sprint, not a "someday" backlog item.

## 17.3 During a demo or rehearsal

1. Switch to Backup Mode immediately (Section 16.2) rather than debugging live in front of an audience — `05_ECFD_Discussion_Day_Demo.md` Section 12 exists precisely for this.
2. After the demo, file an issue describing exactly what failed, so it doesn't recur at the actual graduation discussion.

## 17.4 Production-style incidents (shared dev database, shared demo network)

1. Roll back to the last known-good tag (Section 15) rather than trying to forward-fix under time pressure.
2. Restore the database from the most recent `pg_dump` (Section 16.1) if data was corrupted.
3. Write a one-paragraph postmortem in `docs/adr/` or a dedicated `docs/incidents.md` — not to assign blame, but because `06_ECFD_Team_Workflow_and_Sprints.md`'s retro rule (Section 4.4) applies here too: always end with one concrete change.

---

# 18. Complete Workflow: First Commit to Graduation

This is the single end-to-end picture, tying together everything above with the phase timeline from `04_ECFD_Build_Plan.md` and `ECFD_Engineering_Specification_v1_0.md`.

```text
WEEK 1 (Phase 0 — Freeze Scope)
  - Create the GitHub repository (Section 1)
  - Set branch protection on main (Section 2.3)
  - Create the project board (Section 3)
  - Copy all planning docs into /docs (Section 1.3)
  - Set up CI skeleton: build + lint (Section 11.3, owned by M6)
  - First ADR, if any early architecture choice was made (Section 14)

WEEKS 2–10 (Phases 1–4 — VoIP Lab through E2E Skeleton)
  - Feature branches per component (Section 2)
  - Mock services merged early and labeled `mock` (Section 9)
  - First database migrations, owned by M1 (Section 10)
  - Weekly Integration Sessions begin (Section 13)
  - First tagged checkpoint: v0.1.0 once the dummy end-to-end
    loop works (Section 15.1)

WEEKS 10–34 (Phases 5–13 — ASR through Dashboard)
  - Real ML services replace mocks one at a time; each replacement
    is its own PR, reviewed against the documented contract (Section 8)
  - Contract version bumps as needed, never silent edits (Section 8.2)
  - Dataset versioning begins in earnest during Phase 7 (Section 12)
  - Tag v0.2.0 through v0.5.0 as each major piece lands (Section 15.1)
  - Regular pg_dump backups after each Integration Session (Section 16.1)

WEEKS 34–46 (Phases 14–16 — Evaluation, Ablation, Hardening)
  - Test set frozen and referenced by version, never modified after
    evaluation starts (Section 12.3)
  - Ablation runs tracked with model version manifests (Section 12.4)
  - Hardening fixes tracked as `fix/` branches against real failure
    scenarios found in Integration Sessions (Section 17.2)
  - Tag v0.9.0 once evaluation and hardening are substantially complete

WEEKS 46–50 (Phase 17 — Reproducible Deployment)
  - docker-compose.yml, .env.example, seed data, and Asterisk config
    finalized and documented (Section 16.1)
  - A clean-machine setup is verified by someone who didn't write it
  - Backup demo mode built and tested end-to-end (Section 16.2)

WEEKS 50–52 (Phase 18 — Freeze and Demonstration)
  - Tag v1.0.0 — no new features merge after this except demo-blocking
    fixes (Section 15.2)
  - Full run-through of 05_ECFD_Discussion_Day_Demo.md Section 3
    (Demo Preparation) before the actual defense
  - Backup Mode rehearsed at least once, not just built (Section 16.2)

GRADUATION DISCUSSION DAY
  - If live demo fails: switch to Backup Mode immediately (Section 17.3)
  - Post-defense: tag any final fix as v1.0.1, and archive the
    repository state as it stood for the defense for the team's own
    record (this is also useful evidence for the companion
    ECFD_IP_Ownership_and_Protection_Guide.md)
```

---

# 19. Summary

1. One repository, `main` protected, feature/fix branches, small PRs.
2. Every task has one owner, one issue, one card on the board.
3. Contracts are written down before code is built against them, and versioned — never silently edited.
4. Mocks are a real deliverable, not a placeholder to be ashamed of.
5. Migrations are owned by one person and never edited after merge.
6. Secrets never touch the repository; if they do, rotate first, clean history second.
7. Datasets and models are versioned and never silently modified once used in a reported result.
8. The weekly Integration Session is where parallel work becomes one working system — never skip it.
9. Decisions worth remembering become ADRs, not verbal history.
10. Tag checkpoints, back up the database and demo media, and always have a working Backup Mode ready before demo day.
