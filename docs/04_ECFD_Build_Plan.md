# ECFD — From Zero to Working MVP
## One-Year Execution Plan

This document is the team's implementation roadmap.

The project is finished when the final definition-of-done checklist at the end is satisfied.

---

# 1. Build Philosophy

ECFD must be built as a **vertical slice first**.

Do not spend six months building independent pieces.

The first working version should be:

```text
Real SIP call
 -> Asterisk
 -> ECFD
 -> dummy analysis
 -> dashboard
```

Then replace dummy analysis with real AI.

This reduces integration risk.

---

# 2. Phase 0 — Week 1
## Freeze Scope

Decide:

- attack taxonomy
- supported language(s)
- target audio format
- Asterisk version
- backend version
- frontend stack
- database
- Python version
- repository structure

Create:

```text
/docs
/src
/services
/infra
/tests
/data
```

Create a single architecture document and keep it updated.

---

# 3. Phase 1 — Weeks 2–4
## Build the VoIP Lab

### Goal

Two endpoints can make a real call.

Build:

```text
Asterisk
PJSIP
Endpoint 1001
Endpoint 1002
Dialplan
```

Verify:

```text
1002 -> 1001
```

Both users can hear each other.

### Definition of done

A team member can reproduce the call from a clean machine using the documented setup.

---

# 4. Phase 2 — Weeks 4–6
## Capture / Forward Authorized Media

Implement the ECFD media path.

Target:

```text
Asterisk
   |
External Media
   |
ECFD Media Gateway
```

The gateway should:

- receive media
- decode it
- normalize it
- associate it with session ID
- produce audio windows

### First milestone

The backend logs:

```text
CALL STARTED
MEDIA RECEIVED
CALL ENDED
```

No ML yet.

---

# 5. Phase 3 — Weeks 6–8
## ASP.NET Core Foundation

Build:

```text
CallSession
CallParticipant
Transcript
Evidence
Risk
Alert
```

Implement:

- PostgreSQL
- EF Core migrations
- REST API
- SignalR hub
- health checks
- structured logging

Dashboard can initially display fake values.

---

# 6. Phase 4 — Weeks 8–10
## End-to-End Skeleton

Connect:

```text
Asterisk
 -> media gateway
 -> ASP.NET
 -> SignalR
 -> frontend
```

Use dummy components.

Example:

```text
Every 3 seconds:
Transcript = "test"
Risk = 25
```

The browser should update live.

### Critical milestone

By the end of this phase:

> A real call causes the dashboard to show a live call session.

---

# 7. Phase 5 — Weeks 10–14
## ASR Baseline

Select a pretrained ASR model.

Do not fine-tune immediately.

Benchmark:

- Egyptian Arabic
- English
- mixed Arabic/English
- telephone-quality audio
- noise

Measure:

- WER
- latency

Build:

```text
audio window
 -> Python service
 -> transcript
 -> ASP.NET
 -> SignalR
```

---

# 8. Phase 6 — Weeks 12–16
## NLP Baseline

Implement the tactic taxonomy.

Start with a simple baseline before a Transformer.

For example:

```text
keyword/rule baseline
```

Then compare it with a trained classifier.

This gives the project a useful experimental story:

```text
Baseline
   vs
ML classifier
```

---

# 9. Phase 7 — Weeks 14–20
## Dataset Construction

Create controlled scenarios.

Each scenario should have:

- scenario ID
- attack type
- dialogue script
- speakers
- language
- audio conditions
- annotations

Example:

```text
SCN-OTP-001

Caller:
"I am from IT."

Tactics:
IMPERSONATION
AUTHORITY

Caller:
"I need the verification code."

Tactics:
OTP_REQUEST
SENSITIVE_ACTION
```

Record multiple variations.

Do not train and test on identical recordings or the same speaker/script without controls.

---

# 10. Phase 8 — Weeks 16–22
## NLP Training

Fine-tune the selected classifier.

Track experiments.

Each experiment should record:

```text
dataset version
model
hyperparameters
training seed
metrics
model artifact
```

Select a model based on validation results.

---

# 11. Phase 9 — Weeks 18–24
## Anti-Spoofing

Start with a public pretrained/research model where licensing permits.

Evaluate:

- genuine speech
- replay
- synthetic speech
- compression
- noise

Do not attempt to build a new architecture unless the baseline is clearly insufficient.

Integrate:

```text
audio window
 -> anti-spoof service
 -> voice evidence
```

---

# 12. Phase 10 — Weeks 22–28
## Attack Progression

Implement the state machine.

Example:

```text
NORMAL
IDENTITY_CLAIM
PRESSURE
SENSITIVE_ACTION
CREDENTIAL_OR_FINANCIAL_EXTRACTION
```

Transitions must be triggered by evidence.

Example:

```text
OTP_REQUEST
+
SENSITIVE_ACTION
=
CREDENTIAL_OR_FINANCIAL_EXTRACTION
```

Add tests for every transition.

---

# 13. Phase 11 — Weeks 26–30
## Risk Fusion

Start with deterministic weighted fusion.

Inputs:

```text
tactic evidence
voice evidence
progression
context
```

Output:

```text
0–100
```

Record a risk snapshot whenever the score changes meaningfully.

Then evaluate whether learned fusion is worth adding.

If not, keep the deterministic model.

---

# 14. Phase 12 — Weeks 28–32
## Context Engine

Keep context intentionally small.

MVP examples:

- caller endpoint
- employee endpoint
- known internal extension
- call direction
- time of day
- previous risk evidence within current call

Do not build an enterprise knowledge graph.

The purpose is to show that the same utterance can have different risk depending on context.

---

# 15. Phase 13 — Weeks 30–34
## Dashboard

Implement the final view:

```text
Live call
Live transcript
Risk score
Risk timeline
Tactics
Attack progression
Voice evidence
Alerts
```

The dashboard should update from SignalR.

---

# 16. Phase 14 — Weeks 34–38
## Evaluation

Freeze a test set.

Run:

### Benign

Normal conversations.

### Fraud

All supported attack scenarios.

### Synthetic voice

AI-generated/replayed samples.

### Robustness

- noise
- codec compression
- different speakers
- different phrasing
- Arabic/English switching

Measure:

```text
ASR:
WER / CER / latency

NLP:
Precision / Recall / F1

Anti-spoof:
EER or suitable detection metrics

System:
precision
recall
F1
false positive rate
detection lead time
end-to-end latency
```

---

# 17. Phase 15 — Weeks 38–42
## Ablation Study

This is valuable academically.

Compare:

### Full system

```text
ASR + NLP + Anti-Spoof + Progression
```

### Without anti-spoof

### Without progression

### Without context

### Content-only baseline

Show how each component changes performance.

This gives your professor evidence that the architecture is doing more than simply adding UI around a classifier.

---

# 18. Phase 16 — Weeks 42–46
## Hardening

Fix:

- race conditions
- broken calls
- dropped audio
- ML timeout behavior
- stale SignalR sessions
- database failures
- model service failures

Add graceful degradation.

Example:

If anti-spoof is unavailable:

```text
Voice evidence = unavailable
```

Do not crash the whole call analysis.

---

# 19. Phase 17 — Weeks 46–50
## Reproducible Deployment

Create:

```text
docker-compose.yml
.env.example
database migrations
seed data
model startup scripts
Asterisk configuration
dashboard startup
```

One command should start the local environment as much as practical.

---

# 20. Phase 18 — Weeks 50–52
## Freeze and Demonstration

No major features.

Prepare:

- final demo
- architecture diagrams
- evaluation graphs
- limitations
- technical report
- presentation
- failure recovery procedure

---

# 21. Definition of Done

The project is READY when all of the following work.

## Telephony

- [ ] Endpoint 1001 registers
- [ ] Endpoint 1002 registers
- [ ] Call connects
- [ ] Two-way audio works
- [ ] Media reaches ECFD

## Backend

- [ ] Call session created
- [ ] Participants recorded
- [ ] Session state updated
- [ ] Events processed
- [ ] Evidence persisted
- [ ] Risk calculated
- [ ] Alerts generated

## ASR

- [ ] Audio reaches model
- [ ] Transcript returned
- [ ] partial/final handling works
- [ ] latency measured

## NLP

- [ ] tactics returned
- [ ] confidence returned
- [ ] model version returned
- [ ] evaluation completed

## Anti-Spoof

- [ ] audio windows analyzed
- [ ] result returned
- [ ] low-quality audio handled
- [ ] evaluation completed

## Progression

- [ ] stages defined
- [ ] transitions tested
- [ ] evidence linked to transitions

## Dashboard

- [ ] live call visible
- [ ] transcript visible
- [ ] risk changes live
- [ ] tactics appear
- [ ] progression appears
- [ ] alerts appear

## Evaluation

- [ ] held-out test set
- [ ] benign scenarios
- [ ] fraud scenarios
- [ ] metrics
- [ ] latency
- [ ] false-positive analysis
- [ ] ablation

## Deployment

- [ ] clean-machine setup documented
- [ ] Docker configuration
- [ ] environment variables
- [ ] database migration
- [ ] model versioning
- [ ] demo recovery procedure

---

# 22. Recommended Git Structure

```text
ecfd/
├── backend/
│   ├── ECFD.Api/
│   ├── ECFD.Application/
│   ├── ECFD.Domain/
│   ├── ECFD.Infrastructure/
│   └── ECFD.Tests/
│
├── ml/
│   ├── asr/
│   ├── nlp/
│   └── antispoof/
│
├── frontend/
│
├── telephony/
│   ├── asterisk/
│   └── configs/
│
├── infra/
│   └── docker/
│
├── data/
│   ├── schemas/
│   └── documentation/
│
└── docs/
```

---

# 23. Integration Rule

No one merges a component without:

- contract
- example request/response
- error behavior
- test
- version number

This prevents "works on my laptop" integration.
