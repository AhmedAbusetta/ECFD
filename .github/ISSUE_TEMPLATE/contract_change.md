---
name: Contract Change Proposal
about: Propose a modification to an API or SignalR event schema
title: "[CONTRACT] "
labels: contract
assignees: ''
---

### Contract Type
- [ ] ML Service HTTP Contract (`/v1/asr/analyze`, `/v1/nlp/analyze`, `/v1/voice/analyze`)
- [ ] SignalR Real-Time Event (`transcript.final.v1`, `risk.updated.v1`, etc.)
- [ ] Database Schema / EF Core Migration

### Producer Role & Consumer Role
- **Producer:** (e.g., M2 - Speech AI)
- **Consumer:** (e.g., M1 - Backend Lead)

### Current JSON Contract Shape
```json
{
  "field": "old_value"
}
```

### Proposed JSON Contract Shape
```json
{
  "field": "new_value",
  "addedField": 123
}
```

### Rationale & Breaking Change Mitigation
Explain why this change is necessary and how backward compatibility is handled (e.g., bumping to version `v2`).
