# ADR 0002 — Deterministic Linear Risk Fusion Baseline for v1

## Status
**Accepted**

## Context
ECFD receives multimodal security signals from three independent detectors:
1. NLP conversational tactic classifications (e.g., `OTP_REQUEST`, `IMPERSONATION`).
2. Current stage in the Attack Progression State Machine.
3. Acoustic voice anti-spoofing probability score.

We needed to decide how to combine these signals into an overall risk score ($0 - 100$). Options included:
1. **Opaque Neural Network / Learned Classifier:** Train a multi-layer perceptron on simulated risk scores.
2. **Deterministic Calibrated Weighted Fusion:** A linear combination with explicit contributor weights.

## Decision
We chose **Deterministic Calibrated Weighted Fusion** for the v1 baseline:
$$\text{Risk} = 0.45 \cdot R_{\text{content}} + 0.30 \cdot R_{\text{progression}} + 0.15 \cdot R_{\text{voice}} + 0.10 \cdot R_{\text{context}}$$

## Rationale & Consequences
* **Positives:**
  * **100% Explainable:** Security operators and examiners can inspect exact point additions (e.g., `+31 OTP Request`, `+19 Impersonation`).
  * Provides a verifiable, reproducible benchmark for academic evaluation and ablation studies.
  * No training data bias in the high-level policy decision layer.
* **Future Work:** A learned non-linear fusion model can be compared against this deterministic baseline during Phase 15.
