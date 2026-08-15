# ECFD — Engineering Specification v1.0
## From Architecture to First Working Implementation

**Purpose:** This document converts the ECFD architecture into an implementation-level engineering specification. It is intended to be followed by the team during development and used by the backend/.NET engineer as the starting point for implementation.

---

# 1. Final MVP Architecture

```text
┌─────────────────────────────────────────────────────────────────┐
│                        CONTROLLED VOIP LAB                       │
│                                                                 │
│  Softphone 1001                         Softphone 1002          │
│  Employee                               Attacker                │
│       │                                      │                  │
│       └────────────── SIP/RTP ───────────────┘                  │
│                          │                                      │
│                       Asterisk                                  │
│                    (PJSIP + ARI)                                │
│                          │                                      │
│                  External Media                                 │
│                          │ RTP                                  │
└──────────────────────────┼──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ECFD BACKEND (.NET)                        │
│                                                                 │
│  Asterisk Integration Service                                   │
│           │                                                     │
│           ▼                                                     │
│  Call Session Manager                                           │
│           │                                                     │
│           ▼                                                     │
│  Media Gateway                                                  │
│           │                                                     │
│      Audio Windows                                              │
│           │                                                     │
│     ┌─────┼──────────────┐                                      │
│     │     │              │                                      │
│     ▼     ▼              ▼                                      │
│   ASR    NLP        Anti-Spoof                                  │
│   API    API            API                                     │
│     │     │              │                                      │
│     └─────┼──────────────┘                                      │
│           ▼                                                     │
│      Evidence Store                                             │
│           │                                                     │
│           ▼                                                     │
│   Attack Progression Engine                                     │
│           │                                                     │
│           ▼                                                     │
│      Risk Fusion                                                │
│           │                                                     │
│           ▼                                                     │
│      Policy Engine                                              │
│           │                                                     │
│     ┌─────┴────────┐                                            │
│     ▼              ▼                                            │
│ PostgreSQL      SignalR                                         │
└─────────────────┼───────────────────────────────────────────────┘
                  │
                  ▼
          React / Next.js Dashboard
```

---

# 2. Repository Structure

Use a monorepo.

```text
ecfd/
│
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
│   └── asterisk/
│       ├── pjsip.conf
│       ├── extensions.conf
│       ├── ari.conf
│       └── http.conf
│
├── infra/
│   └── docker-compose.yml
│
├── docs/
│
└── README.md
```

---

# 3. Backend Solution Structure

## 3.1 ECFD.Domain

Contains pure business models.

```text
Entities
Enums
ValueObjects
```

### Entities

```text
CallSession
CallParticipant
TranscriptSegment
Evidence
AttackEvent
RiskSnapshot
Alert
```

### Enums

```csharp
CallStatus
{
    Ringing,
    Answered,
    Analyzing,
    Ended
}

AttackStage
{
    Normal,
    IdentityClaim,
    Pressure,
    SensitiveAction,
    CredentialExtraction
}

EvidenceType
{
    Impersonation,
    Authority,
    Urgency,
    OtpRequest,
    CredentialRequest,
    PaymentRequest,
    VoiceSpoof
}
```

**Rule:** No EF Core or infrastructure dependencies in the Domain project.

---

# 4. ECFD.Application

Contains application use cases.

```text
Calls/
Evidence/
Risk/
Progression/
Policies/
Dashboard/
```

Example use cases:

```text
StartCallSessionCommand
EndCallSessionCommand
ProcessTranscriptCommand
ProcessVoiceEvidenceCommand
RecalculateRiskCommand
AdvanceAttackStageCommand
RaiseAlertCommand
```

This layer defines **what the application does**.

---

# 5. ECFD.Infrastructure

Contains external implementations.

```text
Persistence/
Asterisk/
MLClients/
SignalR/
Logging/
```

Example implementations:

```text
PostgresCallRepository
AsteriskAriClient
AsrClient
NlpClient
AntiSpoofClient
SignalRNotifier
```

---

# 6. ECFD.Api

Contains the executable ASP.NET Core application.

```text
Controllers/
Hubs/
HostedServices/
Middleware/
DependencyInjection/
Configuration/
```

Examples:

```text
CallsController
HealthController
DashboardHub
AsteriskHostedService
MediaGatewayHostedService
```

---

# 7. Exact Backend Components

## 7.1 AsteriskHostedService

This is the bridge between Asterisk and ASP.NET.

Responsibilities:

1. Connect to ARI.
2. Listen for call lifecycle events.
3. Detect when a channel enters the ECFD application.
4. Create a `CallSession`.
5. Identify the bridge.
6. Create an External Media channel.
7. Add the media channel to the appropriate bridge.
8. Detect call termination.

Conceptual flow:

```text
Asterisk
   |
   | ARI event
   v
AsteriskHostedService
   |
   v
CallSessionManager
   |
   v
External Media setup
```

Important Asterisk concepts:

- PJSIP handles SIP endpoints.
- ARI provides application-level call-control events.
- External Media provides the media path from the Asterisk bridge to ECFD.

---

# 8. MediaGatewayHostedService

The media gateway is responsible for receiving the authorized media stream.

Responsibilities:

```text
Receive RTP
Parse RTP headers
Extract audio payload
Decode codec if necessary
Normalize audio
Buffer short windows
Associate window with session
Forward audio to ML services
```

Recommended internal format:

```text
PCM
16 kHz
mono
signed 16-bit
```

Do not store every RTP packet in PostgreSQL.

The database stores metadata and analysis results, not the live audio packet stream.

---

# 9. CallSessionManager

The live state of a call should be maintained in memory.

Example:

```csharp
ConcurrentDictionary<Guid, LiveCallSession>
```

`LiveCallSession`:

```text
SessionId
ExternalCallId
StartedAt
CurrentRisk
CurrentStage
AudioBuffer
RecentEvidence
```

The database contains durable investigation records.

The in-memory object contains transient processing state.

---

# 10. MLOrchestrator

The backend is the only component that communicates with Python ML services.

Responsibilities:

```text
Audio window
    ↓
ASR

Transcript
    ↓
NLP

Audio window
    ↓
Anti-Spoof
```

The orchestrator should hide the transport details from the rest of the application.

Example interfaces:

```csharp
public interface IAsrClient
{
    Task<AsrResult> AnalyzeAsync(
        AudioSegment segment,
        CancellationToken cancellationToken);
}

public interface INlpClient
{
    Task<NlpResult> AnalyzeAsync(
        TranscriptSegment segment,
        CancellationToken cancellationToken);
}

public interface IAntiSpoofClient
{
    Task<VoiceAnalysisResult> AnalyzeAsync(
        AudioSegment segment,
        CancellationToken cancellationToken);
}
```

---

# 11. EvidenceService

ML output must be converted into domain evidence.

Example:

```text
NLP:
OTP_REQUEST = 0.97

↓

Evidence:

Type:
OTP_REQUEST

Confidence:
0.97

Source:
nlp-v1
```

Everything downstream should work with standardized `Evidence` objects rather than model-specific JSON.

---

# 12. AttackProgressionEngine

The first version should be a deterministic state machine.

Example:

```text
NORMAL
   |
   | impersonation
   v
IDENTITY_CLAIM
   |
   | authority / urgency
   v
PRESSURE
   |
   | sensitive request
   v
SENSITIVE_ACTION
   |
   | OTP / credential / payment
   v
CREDENTIAL_EXTRACTION
```

Example transition:

```text
Current stage:
PRESSURE

New evidence:
OTP_REQUEST = 0.97

Transition:
SENSITIVE_ACTION
→
CREDENTIAL_EXTRACTION
```

The progression engine should not inspect raw audio.

It consumes structured evidence.

---

# 13. RiskEngine

The first version should be deterministic and explainable.

Example conceptual model:

```text
risk =
    0.45 * contentRisk
  + 0.30 * progressionRisk
  + 0.15 * voiceRisk
  + 0.10 * contextRisk
```

These weights are **initial engineering values**, not final scientific claims.

They must be tuned against validation data.

Interface:

```csharp
public interface IRiskEngine
{
    RiskResult Calculate(
        IReadOnlyCollection<Evidence> evidence,
        AttackStage stage);
}
```

Output:

```text
Score
Severity
Contributors
```

---

# 14. Risk Result

Example:

```json
{
  "sessionId": "uuid",
  "riskScore": 91,
  "severity": "CRITICAL",
  "topContributors": [
    {
      "type": "OTP_REQUEST",
      "contribution": 31
    },
    {
      "type": "IMPERSONATION",
      "contribution": 19
    }
  ],
  "stage": "CREDENTIAL_EXTRACTION"
}
```

The risk score is a prototype security score.

It must not be presented as:

> "91% probability that the call is fraudulent."

Unless the model has actually been calibrated to support such a probability interpretation.

---

# 15. PolicyEngine

MVP thresholds:

```text
0–29     LOW
30–59    MEDIUM
60–79    HIGH
80–100   CRITICAL
```

Actions:

```text
LOW       → record
MEDIUM    → monitor
HIGH      → dashboard warning
CRITICAL  → prominent alert
```

The MVP should not automatically terminate calls or block financial transactions.

---

# 16. SignalR Dashboard

SignalR is the application's real-time browser communication layer.

Events:

```text
call.started
transcript.partial
transcript.final
tactic.detected
voice.updated
stage.changed
risk.updated
alert.raised
call.ended
```

The frontend should not call ML services directly.

Correct architecture:

```text
Browser
   |
   v
ASP.NET
   |
   v
Python ML
```

---

# 17. Database

Use PostgreSQL.

Core tables:

```text
call_sessions
call_participants
transcript_segments
evidence
attack_events
risk_snapshots
alerts
model_versions
audit_events
```

---

# 18. Database Schema

## call_sessions

```text
id UUID PK
external_call_id TEXT
started_at TIMESTAMPTZ
answered_at TIMESTAMPTZ
ended_at TIMESTAMPTZ
status TEXT
current_risk INT
current_stage TEXT
created_at TIMESTAMPTZ
```

## call_participants

```text
id UUID PK
call_session_id UUID FK
endpoint_id TEXT
role TEXT
display_name TEXT
```

## transcript_segments

```text
id UUID PK
call_session_id UUID FK
sequence_no INT
start_ms BIGINT
end_ms BIGINT
text TEXT
confidence REAL
is_final BOOLEAN
model_version TEXT
created_at TIMESTAMPTZ
```

## evidence

```text
id UUID PK
call_session_id UUID FK
transcript_segment_id UUID NULL
type TEXT
source TEXT
confidence REAL
timestamp TIMESTAMPTZ
payload JSONB
model_version TEXT NULL
created_at TIMESTAMPTZ
```

## attack_events

```text
id UUID PK
call_session_id UUID FK
previous_stage TEXT
new_stage TEXT
trigger_type TEXT
trigger_confidence REAL
created_at TIMESTAMPTZ
```

## risk_snapshots

```text
id UUID PK
call_session_id UUID FK
score INT
severity TEXT
content_risk REAL
progression_risk REAL
voice_risk REAL
context_risk REAL
created_at TIMESTAMPTZ
```

## alerts

```text
id UUID PK
call_session_id UUID FK
severity TEXT
title TEXT
description TEXT
acknowledged BOOLEAN
created_at TIMESTAMPTZ
```

---

# 19. Event Contract

Every event must have:

```text
eventType
eventId
timestamp
sessionId
payload
```

Example:

```json
{
  "eventType": "transcript.final.v1",
  "eventId": "uuid",
  "timestamp": "2026-08-13T12:00:00Z",
  "sessionId": "uuid",
  "payload": {
    "segmentId": "uuid",
    "text": "قول لي الكود اللي وصلك",
    "confidence": 0.94
  }
}
```

Rules:

- IDs are unique.
- Timestamps are UTC.
- Contracts are versioned.
- ML outputs contain model version.
- Consumers should tolerate additional fields.

---

# 20. ML Service Contracts

## ASR

```http
POST /v1/asr/analyze
Content-Type: application/json
```

Request:

```json
{
  "sessionId": "uuid",
  "segmentId": "uuid",
  "sampleRate": 16000,
  "audioFormat": "pcm_s16le",
  "audioBase64": "..."
}
```

Response:

```json
{
  "segmentId": "uuid",
  "text": "أنا من البنك",
  "confidence": 0.94,
  "isFinal": true,
  "startMs": 10000,
  "endMs": 13000,
  "modelVersion": "asr-v1"
}
```

---

## NLP

```http
POST /v1/nlp/analyze
Content-Type: application/json
```

Request:

```json
{
  "sessionId": "uuid",
  "segmentId": "uuid",
  "text": "قول لي الكود اللي وصلك",
  "language": "ar"
}
```

Response:

```json
{
  "segmentId": "uuid",
  "tactics": [
    {
      "type": "OTP_REQUEST",
      "confidence": 0.97
    }
  ],
  "modelVersion": "nlp-v1"
}
```

---

## Anti-Spoof

```http
POST /v1/voice/analyze
Content-Type: application/json
```

Response:

```json
{
  "windowId": "uuid",
  "spoofProbability": 0.82,
  "qualityScore": 0.91,
  "modelVersion": "antispoof-v1"
}
```

The service should also support an insufficient-quality result.

---

# 21. Frontend Structure

Initial page:

```text
/monitor
```

Components:

```text
CallHeader
RiskGauge
AttackTimeline
LiveTranscript
TacticList
VoiceAuthenticityCard
AlertPanel
SystemHealth
```

SignalR connects once.

No polling.

---

# 22. Docker Compose

Target services:

```text
asterisk
postgres
backend
asr
nlp
antispoof
frontend
```

The final development environment should be reproducible from Docker Compose as much as practical.

---

# 23. Hardware

## Developer machines

Recommended:

- x64 CPU
- 16 GB RAM minimum
- SSD
- stable LAN

## Shared AI machine

Recommended:

- NVIDIA GPU
- 8–12 GB+ VRAM
- 32 GB RAM
- NVMe SSD
- Linux preferred

Cloud GPU can be used when necessary.

## Graduation setup

Possible:

```text
Machine 1:
Asterisk + ASP.NET + PostgreSQL

Machine 2:
AI services

Laptop 1:
Employee softphone

Laptop 2:
Attacker softphone

Display:
Dashboard
```

Several services may be consolidated if performance permits.

---

# 24. Real-Time Latency Budget

Target, not guaranteed specification:

| Stage | Target |
|---|---:|
| Audio window | 300–1000 ms |
| Media normalization | <50 ms |
| ASR | <700 ms |
| NLP | <150 ms |
| Anti-spoof | <500 ms |
| Risk fusion | <50 ms |
| SignalR update | <100 ms |
| User-visible update | ~1–2 seconds |

The team must measure actual latency.

---

# 25. Security Requirements

Minimum MVP:

- SIP endpoint authentication
- isolated VoIP demonstration network
- authentication between backend and ML services
- no hardcoded secrets
- role-based dashboard access
- audit logging
- least-privilege database credentials
- input validation
- bounded request sizes
- rate limiting where appropriate
- container isolation
- no real customer calls
- no real banking credentials
- no real OTPs

---

# 26. Observability

Every service should log:

```text
CorrelationId
SessionId
Timestamp
Operation
Duration
Success/Failure
ModelVersion
Error
```

At minimum implement:

- health endpoints
- structured logs
- request duration
- ML inference duration
- call-session correlation

---

# 27. Testing

## Unit

Test:

- risk calculation
- progression transitions
- policy thresholds
- validation
- event parsing

## Integration

Test:

- ASP.NET + PostgreSQL
- backend + mocked ML services
- SignalR
- Asterisk integration

## ML

Test:

- schema
- inference
- regression dataset
- model version

## End-to-End

Test:

```text
SIP call
→ Asterisk
→ media
→ ASR
→ NLP
→ evidence
→ progression
→ risk
→ SignalR
→ dashboard
```

---

# 28. Sequence — Call Start

```text
Softphone
    |
    | SIP INVITE
    v
Asterisk
    |
    | ARI event
    v
ASP.NET
    |
    | Create CallSession
    |
    | Create External Media
    v
Asterisk
    |
    | RTP
    v
MediaGateway
```

---

# 29. Sequence — Fraud Detection

```text
Attacker speaks
      |
      v
Asterisk
      |
      v
MediaGateway
      |
      +----------------+
      |                |
      v                v
     ASR          AntiSpoof
      |
      v
Transcript
      |
      v
     NLP
      |
      v
Evidence
      |
      v
Progression
      |
      v
RiskEngine
      |
      v
SignalR
      |
      v
Dashboard
```

---

# 30. First Four Weeks — Exact Implementation Plan

## Week 1 — Foundation

### Backend

- Create .NET solution.
- Add Domain/Application/Infrastructure/API.
- Configure PostgreSQL.
- Create `CallSession`.
- Add health endpoint.

### Telephony

- Install Asterisk.
- Configure PJSIP.
- Create extension 1001.
- Create extension 1002.

### Milestone

```text
1001 calls 1002.
```

---

# 31. Week 2 — ARI

### Backend

- Enable ARI.
- Connect ASP.NET to ARI.
- Subscribe to call events.
- Log `StasisStart`.
- Log channel IDs.
- Create a `CallSession`.

### Milestone

```text
Incoming SIP call is detected by ASP.NET.
```

---

# 32. Week 3 — Media

### Backend

- Create External Media channel.
- Add it to bridge.
- Open UDP listener.
- Receive RTP.
- Count packets.
- Decode/normalize audio.

### Milestone

```text
ASP.NET receives live call media.
```

---

# 33. Week 4 — Live Dashboard

### Backend

- Add SignalR.
- Add call lifecycle events.
- Add live session state.

### Frontend

Display:

```text
CALL ACTIVE

Session:
Caller:
Callee:
Duration:
Packets received:
```

### Milestone

> A real SIP call appears live on the dashboard.

This is the first major ECFD vertical slice.

---

# 34. Weeks 5–8 — ASR

Build:

```text
Audio window
    ↓
Python ASR
    ↓
Transcript
    ↓
ASP.NET
    ↓
SignalR
    ↓
Dashboard
```

Target:

> Spoken audio becomes visible as a live transcript with measured latency.

Do not fine-tune immediately.

Benchmark a pretrained baseline first.

---

# 35. Weeks 9–12 — NLP

Implement:

```text
Transcript
    ↓
NLP classifier
    ↓
Tactics
    ↓
Evidence
```

Example:

```text
"أنا من البنك"

↓

IMPERSONATION
```

And:

```text
"قول لي الكود"

↓

OTP_REQUEST
```

---

# 36. Weeks 13–16 — Progression + Risk

Implement:

```text
Evidence
    ↓
AttackProgressionEngine
    ↓
RiskEngine
    ↓
PolicyEngine
```

Now the dashboard becomes a security system rather than a transcription system.

---

# 37. Weeks 17–20 — Anti-Spoof

Add:

```text
Audio
 ↓
AntiSpoof
 ↓
VoiceEvidence
 ↓
RiskEngine
```

If the model is not sufficiently reliable, keep it as a secondary signal instead of allowing it to dominate the entire risk decision.

---

# 38. Weeks 21–28 — Data & Evaluation

Freeze a held-out test set.

Evaluate:

### ASR

- WER
- CER
- latency

### NLP

- precision
- recall
- macro F1
- per-class F1
- confusion matrix

### Anti-Spoof

Use an appropriate metric such as EER or a threshold-based detection metric depending on the chosen model/dataset.

### System

- precision
- recall
- F1
- false-positive rate
- detection lead time
- end-to-end latency

---

# 39. Weeks 29–34 — Hardening

Test:

- noise
- codec compression
- different speakers
- different wording
- Arabic/English code-switching
- ML service failure
- database failure
- browser disconnect
- dropped media
- call termination

The system must degrade gracefully.

Example:

```text
AntiSpoof unavailable

↓

Voice evidence = UNAVAILABLE

↓

Continue with content/progression signals
```

---

# 40. Weeks 35–40 — Final Dashboard

Dashboard should show:

```text
LIVE CALL

┌─────────────────────────────┐
│ Risk: 91 / 100    CRITICAL  │
└─────────────────────────────┘

LIVE TRANSCRIPT

"أنا من الـIT..."
"الموضوع عاجل..."
"قول لي الـOTP..."

DETECTED TACTICS

✓ IMPERSONATION
✓ AUTHORITY
✓ URGENCY
✓ OTP_REQUEST

ATTACK PROGRESSION

Identity
   ↓
Authority
   ↓
Pressure
   ↓
Credential Extraction

VOICE AUTHENTICITY

Spoof probability: 0.82
```

---

# 41. Weeks 41–46 — Reproducible Deployment

Create:

```text
docker-compose.yml
.env.example
database migrations
Asterisk configs
model startup
frontend startup
README
```

The environment should be reproducible.

---

# 42. Weeks 47–52 — Freeze

No major new features.

Focus on:

- bugs
- performance
- documentation
- evaluation
- demo reliability
- architecture diagrams
- presentation
- backup demo

---

# 43. Git Workflow

Recommended:

```text
main
develop
feature/*
fix/*
```

Example:

```text
feature/ari-call-session
feature/rtp-media-gateway
feature/asr-client
feature/risk-engine
feature/signalr-dashboard
```

Pull requests should contain:

```text
What changed?
Why?
Tests?
Contract changes?
Screenshots if UI?
```

---

# 44. First Sprint Board

## Sprint 1

- [ ] Create .NET solution
- [ ] Domain project
- [ ] Application project
- [ ] Infrastructure project
- [ ] API project
- [ ] PostgreSQL
- [ ] Asterisk
- [ ] PJSIP extension 1001
- [ ] PJSIP extension 1002
- [ ] Successful SIP call

## Sprint 2

- [ ] ARI enabled
- [ ] ASP.NET ARI connection
- [ ] Receive call events
- [ ] Create CallSession
- [ ] External Media channel
- [ ] RTP listener

## Sprint 3

- [ ] RTP parsing
- [ ] Audio normalization
- [ ] Audio windows
- [ ] SignalR hub
- [ ] Live session state
- [ ] Dashboard call indicator

## Sprint 4

- [ ] ASR service skeleton
- [ ] ASP.NET ASR client
- [ ] Transcript events
- [ ] Live transcript UI

---

# 45. Definition of Done

## Telephony

- [ ] 1001 registers
- [ ] 1002 registers
- [ ] Calls connect
- [ ] Two-way audio works
- [ ] Media reaches ECFD

## Backend

- [ ] Call session created
- [ ] Participants recorded
- [ ] State updated
- [ ] Evidence persisted
- [ ] Risk calculated
- [ ] Alerts generated

## ASR

- [ ] Audio reaches service
- [ ] Transcript returned
- [ ] Partial/final handling
- [ ] Latency measured

## NLP

- [ ] Tactics returned
- [ ] Confidence returned
- [ ] Model version returned
- [ ] Evaluation completed

## Anti-Spoof

- [ ] Audio windows analyzed
- [ ] Result returned
- [ ] Quality handled
- [ ] Evaluation completed

## Progression

- [ ] Stages defined
- [ ] Transitions tested
- [ ] Evidence linked to transitions

## Dashboard

- [ ] Live call visible
- [ ] Transcript visible
- [ ] Risk updates live
- [ ] Tactics appear
- [ ] Progression appears
- [ ] Alerts appear

## Evaluation

- [ ] Held-out test set
- [ ] Benign scenarios
- [ ] Fraud scenarios
- [ ] Metrics
- [ ] Latency
- [ ] False-positive analysis
- [ ] Ablation

## Deployment

- [ ] Clean-machine setup documented
- [ ] Docker configuration
- [ ] Environment variables
- [ ] Database migration
- [ ] Model versioning
- [ ] Demo recovery procedure

---

# 46. Graduation-Day Target

The complete loop must be:

```text
REAL AUTHORIZED VOIP CALL
        ↓
ASTERISK
        ↓
REAL MEDIA
        ↓
ASR
        ↓
REAL TRANSCRIPT
        ↓
NLP
        ↓
REAL SECURITY EVIDENCE
        ↓
ATTACK PROGRESSION
        ↓
RISK FUSION
        ↓
REAL-TIME SIGNALR UPDATE
        ↓
LIVE DASHBOARD
```

The project should not rely on fake transcript/risk events for the main demonstration.

---

# 47. First Implementation Principle

Do not begin by training AI models.

The highest-risk first question is:

> **Can we establish a real SIP call, capture its authorized media through Asterisk, get that media into our ASP.NET backend, and show the live call session on the dashboard?**

Solve that first.

Once that works, ECFD has a real nervous system.

The AI components can then be attached to it incrementally.
