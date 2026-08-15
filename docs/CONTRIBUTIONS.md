# ECFD — Team Contribution & Ownership Register

**Document Type:** Living Team Attribution Ledger  
**Academic Year:** 2026–2027  
**Project:** Egyptian Conversational Fraud Defense (ECFD)  

This document serves as the single source of truth for individual and joint ownership, milestone deliverables, and dated contributions across all team members.

---

## 1. Project Leadership & Originator

* **Originator / Project Lead:** Ahmed
* **Core Conception:** Conceived the real-time multimodal conversational fraud defense model fusing ASR, NLP, and Voice Anti-Spoofing with deterministic Attack Progression FSM.
* **Architecture & Scoping:** Authored and directed the core system architecture, engineering specification, and graduation defense strategy.

---

## 2. Component Ownership Ledger

| Subsystem / Area | Primary Owner | Module & Scope | Sprint Checkpoints |
| :--- | :--- | :--- | :--- |
| **Backend & Real-Time Orchestration** | **Member 1 (Backend Lead)** | `backend/` (ASP.NET Core, ARI integration, Call Session Manager, Risk Engine, FSM, SignalR Hub, EF Core) | Sprints 1–18 |
| **Speech AI / ASR** | **Member 2 (Speech AI Engineer)** | `ml/asr/` (Audio preprocessing, Silero VAD, `faster-whisper` Egyptian Arabic inference, WER/latency benchmarks) | Sprints 1–18 |
| **NLP & Intent Intelligence** | **Member 3 (NLP Engineer)** | `ml/nlp/` (Tactic taxonomy, rule-based baseline, MARBERT classifier fine-tuning, multi-label evaluation) | Sprints 1–18 |
| **Voice Security & Anti-Spoofing** | **Member 4 (Voice Security Engineer)** | `ml/antispoof/` (Synthetic speech detection, AASIST/RawNet2 audio analysis, codec robustness, EER calibration) | Sprints 1–18 |
| **Data, Evaluation & Security** | **Member 5 (Data & Security Engineer)** | `data/`, `docs/evaluation/` (Attack scenario scripts, labeled dataset, benchmark test harness, ablation study, CI security) | Sprints 1–18 |
| **Frontend & DevSecOps** | **Member 6 (Frontend & DevOps)** | `frontend/`, `infra/` (Next.js/React SignalR monitoring console, Docker Compose orchestration, GitHub Actions CI) | Sprints 1–18 |

---

## 3. Sprint Changelog & Contribution Log

### Phase 0: Project Inception & Scope Freeze (Sprint 1)
* **Architecture & Repository Initialization:** Ahmed / Team
* **Event & API Contracts Frozen:** All Team Members
* **CI/CD Pipeline & Docker Skeleton:** Member 6
* **Telephony PJSIP & ARI Environment Plan:** Member 1

*(Updated bi-weekly following each Sprint Review & Retro)*
