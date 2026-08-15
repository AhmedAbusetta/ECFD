# ADR 0003 — Selection of faster-whisper (CTranslate2) for Real-Time Egyptian ASR

## Status
**Accepted**

## Context
ECFD requires speech-to-text inference on streaming 16 kHz audio windows within a strict latency budget ($< 700 \text{ ms}$). The target language is Egyptian Arabic, which frequently includes English technical loanwords and code-switching (*"OTP"*, *"IT support"*, *"Verification code"*).

Options evaluated:
1. **Vanilla PyTorch Hugging Face Whisper:** Accurate, but high memory footprint and high latency ($> 1.5\text{s}$ per chunk on standard GPUs, multiple seconds on CPU).
2. **Cloud ASR APIs (Google Speech, Azure Speech):** Fast, but creates external cloud dependencies and breaks self-contained, reproducible offline evaluation.
3. **`faster-whisper` (CTranslate2 implementation of Whisper):** Re-implemented using CTranslate2, delivering up to $4\times$ speedup and $8\times$ memory reduction via int8 quantization.

## Decision
We selected **`faster-whisper`** (Whisper-medium / fine-tuned Arabic variants) with CTranslate2 runtime.

## Rationale & Consequences
* **Positives:**
  * Runs inference in $< 250\text{ ms}$ on NVIDIA GPUs and $< 700\text{ ms}$ on standard CPUs, strictly satisfying the real-time latency budget.
  * Preserves multilingual capability, seamlessly handling Egyptian Arabic and English code-switching.
  * Fully self-contained inside our local Docker environment.
