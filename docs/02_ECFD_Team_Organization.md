# ECFD — Team Structure & Responsibility Matrix

## 1. Team Philosophy

ECFD is a technical project. Roles are organized around **system ownership**, not generic university titles.

Recommended team size: **6 students**.

A 5-person team is possible by merging roles. A 7-person team is possible by splitting DevSecOps/Security and Frontend.

The percentages below describe **project responsibility/ownership**, not grading percentages and not a measurement of how many hours someone works.

---

# 2. Recommended 6-Person Team

| Member | Role | Responsibility |
|---|---|---:|
| Member 1 | Backend & Real-Time Systems Lead | **25%** |
| Member 2 | Speech AI / ASR Engineer | **18%** |
| Member 3 | NLP / Conversational Intelligence Engineer | **17%** |
| Member 4 | Voice Security / Anti-Spoofing Engineer | **15%** |
| Member 5 | Data, Evaluation & Security Engineer | **13%** |
| Member 6 | Frontend & DevSecOps Engineer | **12%** |

These percentages overlap conceptually because integration is shared; they should be treated as ownership weights rather than a literal percentage of code.

---

# 3. Member 1 — Backend & Real-Time Systems Lead
## Responsibility: 25%

This is the central orchestration role.

### Owns

- ASP.NET Core solution
- API design
- SignalR
- call-session management
- ML service orchestration
- event contracts
- attack progression engine
- risk-fusion engine
- policy/alert engine
- EF Core
- PostgreSQL schema
- audit records
- dashboard API
- integration with Asterisk
- system-level observability

### Main technologies

- C#
- ASP.NET Core
- SignalR
- EF Core
- PostgreSQL
- Docker
- REST/gRPC only where justified

### Main components

```text
CallSessionManager
MediaIngestionGateway
MLOrchestrator
EvidenceService
ProgressionEngine
RiskEngine
PolicyEngine
IncidentService
DashboardHub
```

### Deliverables

1. Call lifecycle API
2. Live session state
3. ML adapters
4. Event processing
5. Risk calculation
6. Progression state machine
7. Persistence
8. SignalR dashboard updates
9. integration tests
10. API documentation

### Must understand

- async programming
- concurrency
- streaming
- WebSockets/SignalR
- SIP/RTP concepts
- database transactions
- event-driven design

---

# 4. Member 2 — Speech AI / ASR
## Responsibility: 18%

### Owns

- audio preprocessing
- voice activity detection
- ASR model selection
- ASR fine-tuning if required
- Arabic/Egyptian Arabic evaluation
- code-switching evaluation
- streaming/near-real-time inference
- transcript confidence

### Technologies

- Python
- PyTorch
- Hugging Face ecosystem
- torchaudio
- FastAPI
- GPU where available

### Deliverables

```text
Audio
 ↓
VAD
 ↓
preprocessing
 ↓
ASR
 ↓
TranscriptEvent
```

### Evaluation

- WER
- CER
- latency
- robustness to noise
- Egyptian Arabic performance

### Does NOT own

- risk scoring
- attack progression
- dashboard
- database architecture

---

# 5. Member 3 — NLP / Conversational Intelligence
## Responsibility: 17%

### Owns

- tactic taxonomy
- annotation guidelines
- text preprocessing
- model selection
- multi-label classification
- fine-tuning
- threshold selection
- calibration
- NLP evaluation

### Technologies

- Python
- PyTorch
- Transformers
- scikit-learn
- FastAPI

### Input

```json
{
  "sessionId": "...",
  "segmentId": "...",
  "text": "...",
  "language": "ar"
}
```

### Output

```json
{
  "tactics": [
    {
      "type": "OTP_REQUEST",
      "confidence": 0.97
    }
  ]
}
```

### Evaluation

- precision
- recall
- macro F1
- per-class F1
- confusion matrix
- calibration

---

# 6. Member 4 — Voice Security / Anti-Spoofing
## Responsibility: 15%

### Owns

- synthetic speech detection
- replay detection experiments
- voice conversion detection
- audio quality robustness
- anti-spoof model selection
- model evaluation

### Technologies

- Python
- PyTorch
- torchaudio
- research anti-spoofing architectures
- appropriate public anti-spoof datasets

### Output

```json
{
  "spoofProbability": 0.87,
  "modelVersion": "anti-spoof-v1"
}
```

### Important limitation

The system must report this as **voice authenticity evidence**, not as proof that a person is malicious.

---

# 7. Member 5 — Data, Evaluation & Security
## Responsibility: 13%

This role protects the scientific credibility of the project.

### Data ownership

- scenario design
- recording protocol
- consent/documentation
- labeling
- dataset versions
- train/validation/test split
- leakage prevention

### Evaluation ownership

- benchmark harness
- precision/recall/F1
- false-positive rate
- detection lead time
- end-to-end latency
- per-scenario results
- ablation studies

### Security ownership

- threat model
- authentication
- authorization requirements
- secure secrets handling
- SIP/RTP hardening checklist
- ML endpoint authentication
- audit requirements

### Deliverables

A final evaluation report answering:

> Does ECFD actually detect the attacks it claims to detect?

---

# 8. Member 6 — Frontend & DevSecOps
## Responsibility: 12%

### Frontend

Owns:

- live dashboard
- SignalR client
- live transcript
- risk visualization
- attack timeline
- investigation view
- system status

Recommended:

- React or Next.js
- TypeScript
- Tailwind CSS
- SignalR JavaScript client

### DevOps

Owns:

- Dockerfiles
- Docker Compose
- local environment
- CI
- test automation
- deployment scripts
- model-service startup
- environment variables
- secrets management
- logs/health checks

---

# 9. 5-Person Variant

If only five students are available:

| Member | Combined role |
|---|---|
| 1 | Backend / Real-Time |
| 2 | Speech AI |
| 3 | NLP + Anti-Spoof |
| 4 | Data + Evaluation + Security |
| 5 | Frontend + DevOps |

The combined AI role is the highest-risk workload.

---

# 10. 7-Person Variant

If seven students are available:

| Member | Role |
|---|---|
| 1 | Backend / Real-Time |
| 2 | Speech AI / ASR |
| 3 | NLP |
| 4 | Voice Anti-Spoof |
| 5 | Data / Evaluation |
| 6 | Frontend |
| 7 | DevSecOps + Security |

This is the cleanest ownership model.

---

# 11. Shared Responsibilities

No component is truly isolated.

Everyone participates in:

### Integration

Each subsystem must expose and consume documented contracts.

### Testing

Every member writes tests for their component.

### Documentation

Every member documents:

- architecture
- setup
- assumptions
- limitations
- evaluation

### Weekly integration

At least one full end-to-end integration test should run regularly.

---

# 12. Responsibility Boundaries

## Backend should NOT

- train neural networks
- label the ML dataset
- invent ASR architecture
- tune anti-spoof models

## ML team should NOT

- directly modify PostgreSQL tables
- own risk business logic
- bypass backend contracts
- send random JSON structures

## Frontend should NOT

- calculate authoritative risk
- implement detection logic
- access ML services directly

## Data/Evaluation should NOT

- change evaluation methodology after seeing results
- mix test data into training
- report only successful scenarios

---

# 13. Ownership Principle

Every component has:

1. **One owner**
2. **One documented interface**
3. **One test suite**
4. **One definition of done**

The owner is responsible for keeping it working, but integration remains a team responsibility.
