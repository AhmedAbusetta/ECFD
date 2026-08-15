# ECFD — Microservice Readiness & Mock Status

**Purpose:** Tracks which microservices are operating with simulated/mock contract data versus integrated, fully-trained models.

| Subsystem | Service Route | Current Status | Integration Target | Owner |
| :--- | :--- | :---: | :--- | :--- |
| **Asterisk PBX** | ARI WebSocket | 🟡 Scaffolding / Ready | Sprint 2 | Member 1 |
| **Media Gateway** | UDP RTP Listener | 🟡 Scaffolding / Ready | Sprint 3 | Member 1 |
| **ASR Service** | `POST /v1/asr/analyze` | 🟡 Mock Mode Ready | Sprint 5 (`faster-whisper`) | Member 2 |
| **NLP Service** | `POST /v1/nlp/analyze` | 🟡 Mock Mode Ready | Sprint 6 (Rule) / Sprint 8 (MARBERT) | Member 3 |
| **Anti-Spoof Service** | `POST /v1/voice/analyze` | 🟡 Mock Mode Ready | Sprint 9 (AASIST) | Member 4 |
| **Progression Engine**| In-Memory FSM | 🟡 Scaffolding / Ready | Sprint 4 (Dummy) / Sprint 10 (Real) | Member 1 |
| **Risk Engine** | Linear Fusion | 🟡 Scaffolding / Ready | Sprint 4 (Dummy) / Sprint 11 (Real) | Member 1 |
| **SignalR Dashboard** | WebSocket Push | 🟡 Scaffolding / Ready | Sprint 4 | Member 6 |

*Legend: 🔴 Not Started | 🟡 Mock/Scaffolding Active | 🟢 Fully Integrated with Real AI/Data*
