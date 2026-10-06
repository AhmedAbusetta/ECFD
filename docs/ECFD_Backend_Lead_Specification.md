# ECFD — Backend Engineering Specification & Contribution Plan

**Document Type:** Individual Role Specification & Technical Contribution Plan  
**Role:** Backend & Real-Time Systems Lead  
**Lead Engineer:** Ahmed Abusetta  
**Technology Stack:** C# / .NET 8, ASP.NET Core, SignalR, EF Core, PostgreSQL, Asterisk ARI/RTP  
**Project Ownership Weight:** **25%** (System Architect & Integration Core)  

---

## Executive Summary of Contribution

As the **Backend & Real-Time Systems Lead**, my primary responsibility is engineering the central nervous system of **ECFD (Egyptian Conversational Fraud Defense)**. I am responsible for taking raw, uncoordinated signals from external telephony networks and AI microservices and transforming them into deterministic, real-time security interventions before an attack succeeds.

```text
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              MY HIGH-LEVEL RESPONSIBILITY                              │
│                                                                                        │
│   Incoming RTP Voice Media (Asterisk) ──► Ingested & Normalized (16kHz PCM)            │
│                                                │                                       │
│                                                ▼                                       │
│   Dispatched to AI Services (FastAPI) ──► Multi-Modal Evidence Collected               │
│                                                │                                       │
│                                                ▼                                       │
│   Processed Through Decision Engines  ──► FSM Progression + Explainable Risk (0–100)   │
│                                                │                                       │
│                                                ▼                                       │
│   Pushed Real-Time via SignalR        ──► Live Next.js Security Console (<100ms)       │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### Core Contributions at a Glance:
1. **Clean Architecture Foundation (.NET 8):** Architected and scaffolded the 4-layer enterprise solution (`ECFD.Domain`, `ECFD.Application`, `ECFD.Infrastructure`, `ECFD.Api`).
2. **Telephony & Media Ingestion Engine:** Engineering the background services that interface with Asterisk PBX via **ARI (Asterisk REST Interface)** and capture binary audio streams over a **UDP RTP socket listener**.
3. **In-Memory Concurrency Management:** Building the thread-safe `CallSessionManager` (`ConcurrentDictionary<Guid, LiveCallSession>`) to track live call states without database I/O bottlenecks.
4. **AI Microservice Orchestration:** Designing the asynchronous HTTP dispatcher communicating with Python AI microservices (Speech ASR, NLP Tactic Classifier, Voice Anti-Spoofing).
5. **The Brain (Decision Engines):**
   * **Attack Progression Engine:** A deterministic Finite State Machine (FSM) modeling conversational attack progression (`Normal` $\to$ `IdentityClaim` $\to$ `Pressure` $\to$ `SensitiveAction` $\to$ `CredentialExtraction`).
   * **Explainable Risk Fusion Engine:** A calibrated mathematical formula calculating a $0 - 100$ score with transparent contributor weights (`+35 OTP_REQUEST`, `+20 IMPERSONATION`).
6. **Real-Time Event Streaming (SignalR):** A persistent WebSocket hub streaming sub-100ms events to the Next.js frontend console.
7. **Durable Persistence & Audit:** Managing PostgreSQL via Entity Framework Core (EF Core) migrations for regulatory investigations and audit trails.

---

## Detailed Technical Scope & Subsystem Breakdown

---

### 1. Architectural Solution Design (Clean Architecture)

I own the backend solution structure, ensuring strict separation of concerns and dependency inversion:

```text
backend/
├── ECFD.Domain/                  # Pure enterprise logic, Zero external dependencies
│   ├── Entities/                 # CallSession, TranscriptSegment, Evidence, Alert
│   ├── Enums/                    # CallStatus, AttackStage, EvidenceType, RiskSeverity
│   └── ValueObjects/
│
├── ECFD.Application/             # Use cases, interfaces, and core business rules
│   ├── Interfaces/               # IAsrClient, INlpClient, IAntiSpoofClient, IRiskEngine
│   ├── Progression/              # AttackProgressionEngine (FSM)
│   ├── Risk/                     # RiskEngine (Weighted Fusion)
│   └── Calls/                    # Call orchestration commands
│
├── ECFD.Infrastructure/          # External technology implementations
│   ├── Asterisk/                 # ARI WebSocket client & bridge management
│   ├── Media/                    # UDP socket listener & RTP packet parsing
│   ├── MLClients/                # HttpClient adapters for Python microservices
│   ├── Persistence/              # PostgreSQL DbContext & EF Core migrations
│   └── SignalR/                  # SignalRNotifier implementation
│
├── ECFD.Api/                     # Entry point & execution runtime
│   ├── Controllers/              # REST management & health endpoints
│   ├── Hubs/                     # DashboardHub (SignalR WebSocket gateway)
│   ├── HostedServices/           # AsteriskHostedService, MediaGatewayHostedService
│   └── Program.cs                # Dependency Injection & middleware pipeline
│
└── ECFD.Tests/                   # xUnit test suites for progression & risk logic
```

---

### 2. Telephony Bridge & Media Gateway

The backend connects directly to the enterprise PBX to ingest audio without third-party telecom black boxes:

#### A. Asterisk Integration (`AsteriskHostedService`)
* Connects to Asterisk via **ARI (Asterisk REST Interface)** over WebSockets: `ws://asterisk:8088/ari/events?app=ecfd-stasis`.
* Listens for lifecycle events: `StasisStart`, `ChannelStateChange`, and `StasisEnd`.
* Calls the ARI REST API to create an **External Media channel** and dynamically injects it into the active call bridge.

#### B. Media Gateway Engine (`MediaGatewayHostedService`)
* Binds to a dedicated **UDP socket on port 10000** to receive raw RTP binary packets.
* **Packet Processing:** Parses 12-byte RTP headers, strips sequence numbers, and extracts audio payloads.
* **Transcoding & Normalization:** Converts G.711 $\mu$-law/A-law 8 kHz audio into **16 kHz 16-bit Mono Linear PCM**.
* **Sliding Buffer:** Slices streaming audio into structured $500\text{ms}$ audio windows for speech recognition and acoustic analysis.

---

### 3. In-Memory Concurrency & Session Management

To guarantee sub-second performance, high-frequency audio packets cannot hit the database directly:

* **Transient State (`CallSessionManager`):** Active calls live in high-performance concurrent memory (`ConcurrentDictionary<Guid, LiveCallSession>`).
* **Session Metadata:** Tracks session UUID, external caller/callee IDs, start timestamp, active stage, and rolling evidence buffers.
* **Lifecycle Synchronization:** Gracefully frees memory buffers when the call terminates via SIP `BYE`.

---

### 4. Machine Learning Orchestration (`MLOrchestrator`)

I manage the communication layer connecting C# to the Python microservices:

| Service | Endpoint | Payload | Output | Target Latency |
| :--- | :--- | :--- | :--- | :---: |
| **ASR (Speech AI)** | `POST /v1/asr/analyze` | 16 kHz PCM Audio Base64 | Text transcript, confidence, `isFinal` | $< 700\text{ ms}$ |
| **NLP (Tactics)** | `POST /v1/nlp/analyze` | Transcript segment text | Array of detected tactic labels + confidence | $< 150\text{ ms}$ |
| **Anti-Spoofing** | `POST /v1/voice/analyze`| 16 kHz PCM Audio Base64 | `spoofProbability`, `qualityScore` | $< 500\text{ ms}$ |

* **Fault Tolerance & Resilience:** Configured with **Polly** retry policies and timeouts. If an AI microservice experiences transient lag, the backend logs a warning and proceeds without stalling the active call.

---

### 5. The Decision Core: State Machine & Explainable Risk Fusion

This is the core intellectual contribution of the project:

#### A. Attack Progression Engine (Deterministic FSM)
Social engineering attacks follow psychological stages. I implemented an in-memory state machine to model this lifecycle:

$$\text{Normal} \longrightarrow \text{IdentityClaim} \longrightarrow \text{Pressure} \longrightarrow \text{SensitiveAction} \longrightarrow \text{CredentialExtraction}$$

* Evaluates incoming tactic evidence against confidence thresholds ($>0.60$).
* Prevents isolated benign words from triggering false alarms.

#### B. Explainable Risk Fusion Engine
Rather than relying on an opaque, uncalibrated black-box model, I built a deterministic, transparent scoring algorithm ($0 - 100$):

$$\text{Risk} = w_{\text{content}} \cdot R_{\text{content}} + w_{\text{progression}} \cdot R_{\text{progression}} + w_{\text{voice}} \cdot R_{\text{voice}} + w_{\text{context}} \cdot R_{\text{context}}$$

* **Explainable Contributor Output:**
  ```json
  {
    "sessionId": "b47c0b02-5c91-4e4b-...",
    "riskScore": 91,
    "severity": "CRITICAL",
    "topContributors": [
      { "type": "OTP_REQUEST", "contribution": 35 },
      { "type": "IMPERSONATION", "contribution": 20 },
      { "type": "URGENCY", "contribution": 15 },
      { "type": "ATTACK_PROGRESSION", "contribution": 35 }
    ],
    "stage": "CredentialExtraction"
  }
  ```

---

### 6. Real-Time Streaming & SignalR Hub

* **`DashboardHub` (`/hubs/dashboard`):** Built on ASP.NET Core SignalR.
* Pushes real-time WebSocket events directly to the Next.js browser client:
  * `call.started` $\to$ Initializes call card on screen.
  * `transcript.final` $\to$ Streams live Arabic text to the transcript feed.
  * `tactic.detected` $\to$ Lights up detected tactic chips (`IMPERSONATION`, `URGENCY`).
  * `stage.changed` $\to$ Steps the visual attack timeline forward.
  * `risk.updated` $\to$ Updates the risk gauge and point contributors list.
  * `alert.raised` $\to$ Triggers high-priority visual alert banners.
  * `call.ended` $\to$ Finalizes session and locks audit record.
* **Latency:** End-to-end SignalR broadcast latency is **$< 100\text{ ms}$**.

---

### 7. Persistence & Database Modeling (PostgreSQL / EF Core)

I own the durable persistence schema and migration pipeline:

* **Tables Managed:**
  1. `call_sessions`: External IDs, endpoints, start/end timestamps, final risk, terminal stage.
  2. `call_participants`: Caller and callee identities and endpoint bindings.
  3. `transcript_segments`: Timestamped text chunks, model versions, confidence scores.
  4. `evidence`: Structured evidence payloads emitted by NLP and acoustic models.
  5. `attack_events`: State machine transition logs with triggering evidence.
  6. `risk_snapshots`: Periodic calculations capturing score evolution over time.
  7. `alerts`: Security warnings, severity tiers, and operator acknowledgment status.
* **Ownership Rule:** To prevent database conflicts, I exclusively generate and apply EF Core migrations (`dotnet ef migrations add`).

---

## Technical Delivery Roadmap (Week-by-Week)

| Phase | Milestone / Deliverable | Target Output |
| :--- | :--- | :--- |
| **Phase 1 (Wks 1–4)** | Clean Architecture scaffolding, PostgreSQL setup, Asterisk ARI connection | Project builds, health checks pass, incoming calls trigger `CallSession` creation. |
| **Phase 2 (Wks 5–8)** | UDP RTP socket listener, media normalization (16kHz PCM), SignalR Hub | Live call media arrives at backend; dashboard shows "Call Active" with packet counts. |
| **Phase 3 (Wks 9–14)** | `MLOrchestrator` HTTP clients, `EvidenceService`, mock vs. real adapters | Real transcript and tactic events reach backend from Python microservices. |
| **Phase 4 (Wks 15–20)** | `AttackProgressionEngine` (FSM) & `RiskEngine` (Fusion formula) | Full decision loop working: dialogue advances stages and calculates explainable risk score. |
| **Phase 5 (Wks 21–24)** | Fault tolerance, Polly resilience, database indexes, rehearsal integration | System gracefully handles dropped packets/ML timeouts; 100% ready for graduation defense. |

---

## Latency Budget & Performance Commitments

To meet the system's **$< 1.5\text{s}$** real-time update target, my backend processing budget is strictly bounded:

| Processing Step | Component | Budget Target |
| :--- | :--- | :---: |
| Media Normalization & Buffering | `MediaGatewayHostedService` | $< 50\text{ ms}$ |
| ML Dispatch & Payload Parsing | `MLOrchestrator` | $< 20\text{ ms}$ (network overhead) |
| State Machine Progression Evaluation | `AttackProgressionEngine` | $< 5\text{ ms}$ |
| Risk Fusion Calculation | `RiskEngine` | $< 5\text{ ms}$ |
| WebSocket Broadcast to Frontend | `DashboardHub` (SignalR) | $< 50\text{ ms}$ |
| **Total Backend Internal Overhead** | — | **$< 130\text{ ms}$** |

---

## Summary of Thesis & Presentation Deliverables

On graduation discussion day, I will defend:
1. **The Real-Time Multimodal Architecture:** Defending how .NET 8 coordinates telecommunications, speech recognition, NLP, and acoustic analysis in parallel.
2. **The Progression State Machine:** Demonstrating why stateful conversational modeling is mathematically and operationally superior to naive keyword detection.
3. **The Explainable Risk Algorithm:** Explaining the weighted contributor formula and how it prevents black-box AI distrust in corporate enterprise security.
4. **Live System Demonstration:** Running the backend live, demonstrating real-time call tracking, and proving sub-second alert generation during an active social engineering attack.
