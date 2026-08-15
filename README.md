# ECFD — Egyptian Conversational Fraud Defense
### Real-Time Multimodal Social-Engineering Detection for Active VoIP Enterprise Communications

[![CI - .NET 8](https://img.shields.io/badge/Backend-.NET%208%20%2F%20C%23-512BD4?style=flat-square&logo=dotnet)](backend/)
[![ML - Python 3.10](https://img.shields.io/badge/ML-Python%203.10%20%2F%20FastAPI-3776AB?style=flat-square&logo=python)](ml/)
[![AI - PyTorch](https://img.shields.io/badge/AI-PyTorch%20%2F%20Transformers-EE4C2C?style=flat-square&logo=pytorch)](ml/)
[![Telephony - Asterisk 20](https://img.shields.io/badge/Telephony-Asterisk%2020%20(PJSIP%2BARI)-F15A24?style=flat-square)](telephony/)
[![Frontend - Next.js 14](https://img.shields.io/badge/Frontend-Next.js%2014%20%2F%20SignalR-000000?style=flat-square&logo=nextdotjs)](frontend/)
[![Database - PostgreSQL 16](https://img.shields.io/badge/Database-PostgreSQL%2016-4169E1?style=flat-square&logo=postgresql)](backend/ECFD.Infrastructure/Persistence/)
[![Build Status](https://img.shields.io/badge/CI-Passing-brightgreen?style=flat-square)]()
[![Version](https://img.shields.io/badge/Version-v0.1.0--alpha-blue?style=flat-square)]()
[![License](https://img.shields.io/badge/License-Academic%20Proprietary-yellow?style=flat-square)](LICENSE)

---

## 1. Executive Summary

Traditional cybersecurity perimeter defenses protect networks, accounts, and endpoints. However, **social engineering bypasses these controls** by manipulating authorized employees into voluntarily sharing credentials, approving malicious payments, or reading out verification codes (OTPs).

**ECFD (Egyptian Conversational Fraud Defense)** is an enterprise-grade real-time security system that monitors authorized enterprise VoIP calls, analyzes conversational behavior, detects acoustic deepfake/voice-cloning artifacts, tracks the attack progression state machine, and computes an **explainable 0–100 risk score** streamed to a live security console before the final harmful action occurs.

```
REAL AUTHORIZED VOIP CALL
        ↓
ASTERISK PBX (PJSIP + ARI)
        ↓
REAL-TIME MEDIA STREAM (RTP / External Media)
        ↓
ASR + NLP + VOICE ANTI-SPOOFING (Python Microservices)
        ↓
STRUCTURED SECURITY EVIDENCE
        ↓
ATTACK PROGRESSION ENGINE (Deterministic FSM)
        ↓
EXPLAINABLE RISK FUSION (0–100 Score)
        ↓
REAL-TIME SIGNALR EVENT STREAM
        ↓
LIVE SECURITY MONITORING DASHBOARD (Next.js / React)
```

---

## 2. Team Composition & Track Ownership Matrix

ECFD is engineered by a balanced **6-person multidisciplinary engineering team**, organized around strict modular boundaries:

| Member / Role | Academic Track | Ownership | Primary Module & Folder | Core Deliverables |
| :--- | :--- | :---: | :--- | :--- |
| **Member 1 (Backend Lead)** | **Software Engineering** | **25%** | `backend/` (`.NET 8`, `EF Core`, `SignalR`) | Call Session Manager, RTP Media Gateway, ARI integration, State Machine, Risk Engine, DB migrations. |
| **Member 2 (Speech AI Engineer)** | **AI / Speech Processing** | **18%** | `ml/asr/` (`FastAPI`, `faster-whisper`) | Audio preprocessing, Silero VAD, streaming Egyptian Arabic ASR, latency & WER optimization ($<700\text{ms}$). |
| **Member 3 (NLP Engineer)** | **AI / Natural Language** | **17%** | `ml/nlp/` (`PyTorch`, `MARBERT`) | 10-tactic taxonomy, rule-based baseline, multi-label Transformer fine-tuning, threshold calibration ($<150\text{ms}$). |
| **Member 4 (Voice Security Engineer)**| **AI / Audio Security** | **15%** | `ml/antispoof/` (`AASIST`, `RawNet2`) | Synthetic voice detection, telephony codec degradation robustness, EER calibration, voice evidence score ($<500\text{ms}$). |
| **Member 5 (Data & Security Engineer)**| **Cybersecurity / Data** | **13%** | `data/`, `docs/evaluation/` | 30+ scenario dialogue scripts, labeled dataset, benchmark test harness, ablation study, CI secret scanning. |
| **Member 6 (Frontend & DevOps)** | **Web Dev & DevSecOps** | **12%** | `frontend/`, `infra/` (`Docker`, `CI/CD`) | Real-time Next.js SignalR console, Attack timeline, Risk gauge, Docker Compose one-command orchestration. |

*Detailed role specifications are available in [`docs/02_ECFD_Team_Organization.md`](docs/02_ECFD_Team_Organization.md).*

---

## 3. High-Level Technical Architecture

```text
┌──────────────────────────────────────────────────────────────────────────────────┐
│                               CONTROLLED VOIP LAB                                │
│                                                                                  │
│   Softphone 1001 (Employee) ──────── SIP/RTP ──────── Softphone 1002 (Attacker)   │
│                                      │                                           │
│                            Asterisk PBX (PJSIP)                                  │
│                                      │ ARI (Call Control) & External Media       │
└──────────────────────────────────────┼───────────────────────────────────────────┘
                                       │ RTP (16 kHz PCM Audio)
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                            ECFD BACKEND (.NET 8)                                 │
│                                                                                  │
│   AsteriskHostedService (ARI) ──► CallSessionManager (In-Memory Concurrent State)│
│                                      │                                           │
│   MediaGatewayHostedService (RTP Normalizer & Window Buffer: 500ms)              │
│                                      │                                           │
│                         ┌────────────┼────────────┐                              │
│                         ▼            ▼            ▼                              │
│                      ASR API      NLP API    Anti-Spoof API                      │
│                     (FastAPI)    (FastAPI)     (FastAPI)                         │
│                         │            │            │                              │
│                         └────────────┼────────────┘                              │
│                                      ▼                                           │
│                               EvidenceService                                    │
│                                      │                                           │
│                       AttackProgressionEngine (FSM)                              │
│                                      │                                           │
│                              RiskEngine (Fusion)                                 │
│                                      │                                           │
│                                 PolicyEngine                                     │
│                                      │                                           │
│                         ┌────────────┴────────────┐                              │
│                         ▼                         ▼                              │
│               PostgreSQL Database            SignalR Hub                         │
│             (Audit & Persistence)                 │                              │
└───────────────────────────────────────────────────┼──────────────────────────────┘
                                                    │ WebSockets
                                                    ▼
                                     React / Next.js Dashboard Console
```

---

## 4. Detection Taxonomy & Attack Progression

### 4.1 Social Engineering Tactic Classes (NLP)
ECFD classifies active conversational utterances against a targeted 10-class taxonomy:
1. `IMPERSONATION` — False identity claim (IT support, bank officer, executive).
2. `AUTHORITY` — Asserting institutional power or policy compliance.
3. `URGENCY` — Imposing artificial time constraints (*"account will close in 10 minutes"*).
4. `OTP_REQUEST` — Direct solicitation of SMS/app verification codes.
5. `CREDENTIAL_REQUEST` — Solicitation of passwords, PINs, or card CVVs.
6. `PAYMENT_REQUEST` — Directing money transfer via InstaPay / Vodafone Cash.
7. `REMOTE_ACCESS` — Requesting AnyDesk/TeamViewer software installation.
8. `SECRECY` — Coercing victim into keeping the call hidden from colleagues.
9. `VERIFICATION_BYPASS` — Convincing victim to skip standard security protocols.
10. `SENSITIVE_ACTION` — Coercing dangerous device actions prior to extraction.

### 4.2 Deterministic Attack Progression (FSM)
```
[ NORMAL CONVERSATION ]
          │
          ▼  (Trigger: IMPERSONATION)
[ IDENTITY_CLAIM ]
          │
          ▼  (Trigger: AUTHORITY / URGENCY)
[ PRESSURE ]
          │
          ▼  (Trigger: REMOTE_ACCESS / SENSITIVE_ACTION)
[ SENSITIVE_ACTION ]
          │
          ▼  (Trigger: OTP_REQUEST / CREDENTIAL_REQUEST / PAYMENT_REQUEST)
[ CREDENTIAL_OR_FINANCIAL_EXTRACTION ]  ──►  [ 🚨 CRITICAL ALERT RAISED ]
```

---

## 5. Explainable Risk Fusion Formula

Risk is computed on a calibrated $0 - 100$ scale and broken down into transparent, explainable contributors:

$$\text{Risk} = 0.45 \cdot R_{\text{content}} + 0.30 \cdot R_{\text{progression}} + 0.15 \cdot R_{\text{voice}} + 0.10 \cdot R_{\text{context}}$$

* **Explainable Output Example:**
  ```text
  RISK SCORE: 91 / 100 (CRITICAL)
  Contributors:
    +35 OTP_REQUEST (Confidence: 0.97)
    +20 IMPERSONATION (Confidence: 0.95)
    +15 URGENCY (Confidence: 0.88)
    +35 ATTACK_PROGRESSION (Stage: CredentialExtraction)
  ```

---

## 6. Monorepo Structure

```text
ecfd/
├── .github/                            # 🚀 CI/CD & Automation
│   ├── workflows/ (backend-ci, ml-ci, security-scan)
│   ├── ISSUE_TEMPLATE/ (bug, feature, contract_change)
│   └── PULL_REQUEST_TEMPLATE.md
├── backend/                            # ⚙️ ASP.NET Core Clean Architecture (.NET 8)
│   ├── ECFD.sln
│   ├── ECFD.Domain/                    # Pure Domain Entities, Enums, ValueObjects
│   ├── ECFD.Application/               # Use Cases, Engines (Risk, FSM), ML Interfaces
│   ├── ECFD.Infrastructure/            # EF Core PostgreSQL, ARI Client, ML Clients, SignalR
│   ├── ECFD.Api/                       # REST Controllers, SignalR Hubs, Hosted Services
│   └── ECFD.Tests/                     # Unit & Engine Test Suites
├── ml/                                 # 🤖 Python ML Microservices
│   ├── asr/                            # Speech AI (FastAPI + faster-whisper)
│   ├── nlp/                            # Intent Classifier (FastAPI + MARBERT)
│   └── antispoof/                      # Voice Authenticity (FastAPI + AASIST)
├── frontend/                           # 💻 React / Next.js 14 Real-Time Monitoring Console
├── telephony/                          # 📞 Asterisk 20 PBX (PJSIP, ARI, Dialplan configs)
├── infra/                              # 🐳 Docker Compose (Full & Lightweight Mock stacks)
├── data/                               # 📊 Labeled Scenario Dataset & JSON Schemas
├── docs/                               # 📚 Complete Engineering & Architecture Documentation Pack
│   ├── 01_ECFD_Project_Scope.md
│   ├── 02_ECFD_Team_Organization.md
│   ├── 03_ECFD_Technical_Architecture.md
│   ├── 04_ECFD_Build_Plan.md
│   ├── 05_ECFD_Discussion_Day_Demo.md
│   ├── 06_ECFD_Team_Workflow_and_Sprints.md
│   ├── ECFD_Engineering_Specification_v1.0.md
│   ├── ECFD_IP_Ownership_and_Protection_Guide.md
│   ├── ECFD_Team_Collaboration_and_GitHub_Handbook.md
│   ├── CONTRIBUTIONS.md                # Team Contribution Register
│   ├── mock-status.md                  # Microservice Readiness Tracker
│   └── adr/                            # Architecture Decision Records (ADRs)
├── .editorconfig                       # Uniform code formatting
├── .gitattributes                      # Line endings normalization
├── .gitignore                          # Strict ignore rules (secrets, weights, raw audio)
├── .env.example                        # Documented environment template
└── LICENSE                             # Academic Research License
```

---

## 7. Quick Start Guide

### Prerequisites
* Docker & Docker Compose
* .NET 8 SDK *(for local backend dev)*
* Python 3.10+ *(for local ML dev)*
* Node.js 20+ *(for local frontend dev)*

### Option A: Run the Lightweight Mock Stack (Zero GPU Needed)
Runs PostgreSQL, Asterisk, and ASP.NET Core with integrated mock ML engines:
```bash
docker compose -f infra/docker-compose.mock.yml up -d
```
* **Swagger UI:** [http://localhost:5000/swagger](http://localhost:5000/swagger)
* **Backend Health Check:** [http://localhost:5000/api/health](http://localhost:5000/api/health)

### Option B: Run Full Production Stack (with Python ML Microservices)
```bash
docker compose -f infra/docker-compose.yml up -d
```

### Option C: Run .NET Unit Tests Locally
```bash
dotnet test backend/ECFD.sln
```

---

## 8. Complete Documentation Index

All core engineering specifications live inside [`docs/`](docs/):

1. **[01 — Project Scope](docs/01_ECFD_Project_Scope.md)**: Problem statement, boundaries, what ECFD is *not*.
2. **[02 — Team Organization](docs/02_ECFD_Team_Organization.md)**: Ownership breakdown percentages and interface boundaries.
3. **[03 — Technical Architecture](docs/03_ECFD_Technical_Architecture.md)**: Telecom stack, ASP.NET Core, database schema, latency budget.
4. **[04 — Build Plan](docs/04_ECFD_Build_Plan.md)**: One-year execution roadmap and milestone checklist.
5. **[05 — Discussion Day Demo](docs/05_ECFD_Discussion_Day_Demo.md)**: Live 2-softphone demo script and professor Q&A defense.
6. **[06 — Team Workflow & Sprints](docs/06_ECFD_Team_Workflow_and_Sprints.md)**: 2-week sprint rhythm, 4 Agile rituals, and parallel execution map.
7. **[Engineering Specification v1.0](docs/ECFD_Engineering_Specification_v1.0.md)**: Detailed C# interfaces, API contracts, and first 4-week sprint specs.
8. **[IP Ownership & Protection Guide](docs/ECFD_IP_Ownership_and_Protection_Guide.md)**: Egyptian IP Law No. 82/2002, university IP clearance, and startup spinout guidance.
9. **[Collaboration & GitHub Handbook](docs/ECFD_Team_Collaboration_and_GitHub_Handbook.md)**: Monorepo rules, branch protection, EF migrations, and security policies.
10. **[Team Contribution Register](docs/CONTRIBUTIONS.md)**: Living attribution ledger.
11. **[Architecture Decision Records](docs/adr/)**: ADR-0001 (External Media), ADR-0002 (Risk Fusion), ADR-0003 (Faster-Whisper).

---

## 9. Graduation Discussion Day Demonstration

The project culminates in a **live, real-time phone call demonstration**:

```text
Attacker Softphone (1002) ──► Asterisk PBX ──► Employee Softphone (1001)
                                   │ (RTP Audio)
                                   ▼
                         ECFD Real-Time Pipeline
                                   │
                                   ▼
                 Live Security Dashboard on Projector
```

* **Scenario 1 (Benign Call):** Legitimate call (*"Are we meeting at five?"*) $\to$ Score remains **LOW Risk (0–10/100)**.
* **Scenario 2 (Social Engineering):** Fake IT support attack unfolds live $\to$ System detects `IMPERSONATION` $\to$ `URGENCY` $\to$ `OTP_REQUEST` $\to$ Progression moves to `CredentialExtraction` $\to$ **CRITICAL Alert (91/100)** raised in $< 1.5\text{s}$.
* **Scenario 3 (Synthetic Voice):** Cloned AI voice detected by acoustic anti-spoofing detector.
* **Backup Replay Mode:** Pre-recorded call media pipeline ready as an instant fallback to protect against demo-room network/hardware failures.

---

<div align="center">
  <sub>ECFD is an academic graduation project developed for the Faculty of Information Technology / Computer Science.</sub>
</div>
