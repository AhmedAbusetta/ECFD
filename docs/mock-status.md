# ECFD — Microservice Readiness & Mock Status

**Purpose:** Tracks which microservices are operating with simulated/mock contract data versus integrated, fully-trained models.

| Subsystem | Service Route | Current Status | Integration Target | Owner |
| :--- | :--- | :---: | :--- | :--- |
| **Asterisk PBX** | ARI WebSocket | 🔴 Config only — no ARI client in backend | Sprint 2 | Member 1 |
| **Media Gateway** | UDP RTP Listener | 🔴 Stub hosted service — no listener | Sprint 3 | Member 1 |
| **ASR Service** | `POST /v1/asr/analyze` | 🟢 Cohere Transcribe Arabic on Modal GPU (`ml/asr/modal_app.py`, ~1 s warm); falls back to local faster-whisper `small` after 8 s (cold start / outage). URL in gitignored `appsettings.Local.json` | Sprint 5 | Member 2 |
| **NLP Service** | `POST /v1/nlp/analyze` | 🟡 Keyword rules, called by backend when `MlServices:UseMocks=false` | Sprint 6 (Rule) / Sprint 8 (MARBERT) | Member 3 |
| **Anti-Spoof Service** | `POST /v1/voice/analyze` | 🟡 Constant mock (backend does not call it yet) | Sprint 9 (AASIST) | Member 4 |
| **Progression Engine**| In-Memory FSM | 🟡 v0 FSM working, unit + integration tested | Sprint 4 (Dummy) / Sprint 10 (Real) | Member 1 |
| **Risk Engine** | Linear Fusion | 🟡 v0 additive points — weighted fusion (ADR-0002) not implemented | Sprint 4 (Dummy) / Sprint 11 (Real) | Member 1 |
| **SignalR Dashboard** | WebSocket Push | 🟢 Live end-to-end in simulated demo (verified 2026-10-03) | Sprint 4 | Member 6 |

*Legend: 🔴 Not Started | 🟡 Mock/Scaffolding Active | 🟢 Fully Integrated with Real AI/Data*
