# ECFD — Technical Architecture & Engineering Specification

## 1. Technical Objective

Build a real-time prototype that accepts authorized VoIP call media, analyzes speech and voice authenticity, converts model outputs into structured security evidence, models attack progression, calculates an explainable risk score, and streams the result to a monitoring dashboard.

---

# 2. Recommended Technology Stack

## Telephony

- Asterisk
- PJSIP
- SIP
- RTP
- ARI
- Asterisk External Media

Asterisk is the PBX/media engine. It handles the controlled VoIP call.

ARI provides application-level control and asynchronous call events. Asterisk External Media can send bridge media to an external media service for analysis.

For an MVP, prefer a supported External Media path over raw packet sniffing.

---

# 3. Backend

## ASP.NET Core

Use ASP.NET Core as the central application.

Responsibilities:

```text
API
Call Sessions
ML Orchestration
Evidence
Progression
Risk
Policy
Persistence
SignalR
Audit
Health
```

Recommended libraries:

- ASP.NET Core
- SignalR
- EF Core
- Npgsql
- FluentValidation or equivalent validation approach
- System.Text.Json
- OpenTelemetry instrumentation where practical
- Serilog or structured Microsoft logging

---

# 4. Real-Time Communication

SignalR is the dashboard's real-time channel.

The backend publishes:

```text
CallStarted
TranscriptPartial
TranscriptFinal
TacticDetected
VoiceEvidenceUpdated
AttackStageChanged
RiskUpdated
AlertRaised
CallEnded
```

SignalR is for **application events to the browser**.

It is not the RTP media transport.

---

# 5. ML Services

Use Python for model development and inference.

Recommended baseline:

- Python
- PyTorch
- FastAPI
- Hugging Face Transformers where appropriate
- torchaudio
- NumPy
- scikit-learn for evaluation/calibration

Each ML service exposes a stable HTTP contract.

The backend owns orchestration.

The ML services own models.

---

# 6. ML Service Boundaries

## ASR

```text
POST /v1/asr/analyze
```

Input:

```json
{
  "sessionId": "uuid",
  "segmentId": "uuid",
  "sampleRate": 16000,
  "audioFormat": "pcm_s16le",
  "languageHint": "ar",
  "audioBase64": "..."
}
```

Output:

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

For a first prototype, short audio windows can be sent over HTTP. If performance requires it later, use a streaming protocol.

---

# 7. NLP Contract

```text
POST /v1/nlp/analyze
```

Input:

```json
{
  "sessionId": "uuid",
  "segmentId": "uuid",
  "text": "قول لي الكود اللي وصلك",
  "language": "ar"
}
```

Output:

```json
{
  "segmentId": "uuid",
  "tactics": [
    {
      "type": "OTP_REQUEST",
      "confidence": 0.97
    },
    {
      "type": "CREDENTIAL_REQUEST",
      "confidence": 0.90
    }
  ],
  "modelVersion": "nlp-v1"
}
```

---

# 8. Anti-Spoof Contract

```text
POST /v1/voice/analyze
```

Input:

```json
{
  "sessionId": "uuid",
  "windowId": "uuid",
  "sampleRate": 16000,
  "audioFormat": "pcm_s16le",
  "audioBase64": "..."
}
```

Output:

```json
{
  "windowId": "uuid",
  "spoofProbability": 0.87,
  "qualityScore": 0.81,
  "modelVersion": "antispoof-v1"
}
```

The service should also be able to report insufficient-quality windows.

---

# 9. Media Gateway

The media gateway is responsible for:

1. receiving media from Asterisk
2. decoding the selected codec
3. associating audio with a call/session
4. normalizing the sample format
5. buffering short windows
6. forwarding windows to ML services
7. dropping or backpressuring safely when overloaded

Recommended internal representation:

```text
PCM
16 kHz
mono
signed 16-bit
```

The gateway must not write every packet to PostgreSQL.

---

# 10. Asterisk Integration

Controlled environment:

```text
SIP Endpoint 1001
SIP Endpoint 1002
        |
        v
     Asterisk
        |
      Bridge
        |
        v
 External Media
        |
        v
ECFD Media Gateway
```

Use PJSIP for endpoints.

Use ARI for application-level call lifecycle/control.

Use External Media to deliver media to ECFD.

Asterisk supports external media channels and can deliver RTP to an external destination. Newer Asterisk versions also provide a WebSocket media driver that can reduce raw packet-handling work; the exact mechanism should be validated against the selected Asterisk release during implementation.

---

# 11. Call Lifecycle

```text
IDLE
  |
  v
RINGING
  |
  v
ANSWERED
  |
  v
ANALYZING
  |
  +--> HIGH RISK
  |
  v
ENDED
```

The call session state belongs to ASP.NET.

Asterisk remains the telephony engine.

---

# 12. Session Model

```csharp
CallSession
{
    Id
    ExternalCallId
    CallerEndpoint
    CalleeEndpoint
    StartedAt
    AnsweredAt
    EndedAt
    Status
    CurrentRisk
    CurrentStage
}
```

The in-memory representation may contain transient processing state.

The database contains durable investigation information.

---

# 13. Evidence Model

Every detector produces evidence.

```csharp
Evidence
{
    Id
    SessionId
    Type
    Source
    Confidence
    Timestamp
    SegmentId
    Payload
    ModelVersion
}
```

Examples:

```text
TacticDetected
VoiceSpoofEvidence
ASRConfidence
ContextSignal
AttackTransition
```

This allows the final risk decision to remain explainable.

---

# 14. Attack Progression Engine

Recommended implementation:

A deterministic state machine initially.

Example:

```text
NORMAL
  |
  | impersonation
  v
IDENTITY_CLAIM
  |
  | authority/urgency
  v
PRESSURE
  |
  | sensitive request
  v
SENSITIVE_ACTION
  |
  | OTP/credential/payment
  v
CREDENTIAL_OR_FINANCIAL_EXTRACTION
```

The progression engine should not directly inspect raw audio.

It consumes structured evidence.

---

# 15. Risk Fusion

Version 1 should be deterministic and explainable.

Example:

```text
Content contribution
Progression contribution
Voice contribution
Context contribution
```

Pseudo-model:

```text
risk =
    0.45 * contentRisk
  + 0.30 * progressionRisk
  + 0.15 * voiceRisk
  + 0.10 * contextRisk
```

These weights are placeholders.

They must be tuned and evaluated using the project's validation data.

Later, the team can compare this baseline with a learned fusion model.

The baseline must remain available because it is interpretable and provides a benchmark.

---

# 16. Risk Output

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
  "stage": "CREDENTIAL_OR_FINANCIAL_EXTRACTION"
}
```

---

# 17. Policy Engine

For the MVP, keep policy simple.

Example:

```text
0–29    LOW
30–59   MEDIUM
60–79   HIGH
80–100  CRITICAL
```

Actions:

```text
LOW       -> record
MEDIUM    -> monitor
HIGH      -> dashboard warning
CRITICAL  -> prominent alert
```

Do not automatically hang up calls or block transactions in the university prototype.

---

# 18. Database

Recommended database: PostgreSQL.

Core tables:

```text
call_sessions
call_participants
audio_segments
transcript_segments
tactic_evidence
voice_evidence
attack_events
risk_snapshots
alerts
model_versions
audit_events
```

### call_sessions

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

### call_participants

```text
id UUID PK
call_session_id UUID FK
endpoint_id TEXT
role TEXT
display_name TEXT
```

### transcript_segments

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

### tactic_evidence

```text
id UUID PK
call_session_id UUID FK
transcript_segment_id UUID FK
tactic_type TEXT
confidence REAL
model_version TEXT
created_at TIMESTAMPTZ
```

### voice_evidence

```text
id UUID PK
call_session_id UUID FK
window_start_ms BIGINT
window_end_ms BIGINT
spoof_probability REAL
quality_score REAL
model_version TEXT
created_at TIMESTAMPTZ
```

### attack_events

```text
id UUID PK
call_session_id UUID FK
previous_stage TEXT
new_stage TEXT
trigger_type TEXT
trigger_confidence REAL
created_at TIMESTAMPTZ
```

### risk_snapshots

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

### alerts

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

# 19. Event Contracts

Use versioned event names.

Example:

```json
{
  "eventType": "transcript.final.v1",
  "eventId": "uuid",
  "timestamp": "ISO-8601",
  "sessionId": "uuid",
  "payload": {
    "segmentId": "uuid",
    "text": "...",
    "confidence": 0.94
  }
}
```

Rules:

- every event has an ID
- every event has a session ID
- timestamps are UTC
- contracts are versioned
- model version is included for ML outputs
- unknown fields must not break consumers

---

# 20. Frontend Contract

The browser should never call the ML services.

```text
Browser
   |
   v
ASP.NET API / SignalR
   |
   v
ML services
```

This keeps the architecture secure and centralized.

---

# 21. Real-Time Latency Budget

The MVP target should be **near-real-time**, not mathematical zero-latency.

A reasonable target:

| Stage | Target |
|---|---:|
| Audio buffering/window | 300–1000 ms |
| Media normalization | <50 ms |
| ASR inference | <700 ms |
| NLP inference | <150 ms |
| Anti-spoof inference | <500 ms |
| Backend fusion | <50 ms |
| SignalR/dashboard update | <100 ms |
| **Target user-visible update** | **~1–2 seconds** |

The team should measure actual values rather than claiming these numbers.

A slower system is acceptable if documented.

---

# 22. Hardware

## Development

Each developer:

- modern x64 laptop/desktop
- 16 GB RAM minimum
- SSD
- stable LAN/Wi-Fi

AI training benefits from an NVIDIA GPU.

## Shared AI machine

Recommended:

- NVIDIA GPU with at least 8–12 GB VRAM where available
- 32 GB RAM
- NVMe SSD
- Linux preferred for model experimentation

A cloud GPU can be used when local hardware is insufficient.

## Graduation demo

Possible setup:

- 1 machine for Asterisk/backend
- 1 AI machine
- 2 laptops/phones as SIP endpoints
- 1 display/projector

The system can also be consolidated onto fewer machines if performance permits.

---

# 23. Security Controls

Minimum MVP:

- SIP endpoint authentication
- isolated demo network
- HTTPS for HTTP services where deployment requires it
- authentication between backend and ML services
- no hardcoded production secrets
- role-based dashboard access
- audit logging
- least-privilege database credentials
- input validation
- bounded request sizes
- rate limiting on external APIs
- container isolation
- no real customer calls/data

---

# 24. Hardware/Software Separation

The project should distinguish:

### Telephony

Asterisk/PJSIP/RTP

### Application

ASP.NET Core

### Intelligence

Python/PyTorch

### Persistence

PostgreSQL

### Live UI

React/Next.js + SignalR

### Infrastructure

Docker Compose

---

# 25. Why ASP.NET + Python?

ASP.NET is appropriate for:

- application orchestration
- state management
- APIs
- SignalR
- persistence
- security
- business/risk logic

Python/PyTorch is appropriate for:

- model training
- GPU inference
- experimentation
- speech processing
- NLP
- anti-spoof research

The boundary is deliberate.

```text
ASP.NET
   |
   | stable contracts
   v
Python ML
```

The backend should never depend on Python implementation details.

---

# 26. Observability

Every major component should emit:

- structured logs
- correlation ID
- session ID
- model version
- processing duration
- success/failure
- queue depth where applicable

At minimum, the dashboard should show system health.

---

# 27. Testing Strategy

### Unit tests

- risk calculation
- progression transitions
- policy thresholds
- validators

### Integration tests

- API + PostgreSQL
- backend + ML mock services
- SignalR

### Telephony tests

- endpoint registration
- call establishment
- media delivery
- call termination

### ML tests

- model inference
- schema validity
- regression datasets

### End-to-end tests

```text
SIP call
 -> audio
 -> ASR
 -> NLP
 -> progression
 -> risk
 -> SignalR
 -> dashboard
```
