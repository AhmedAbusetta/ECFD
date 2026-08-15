# ADR 0001 — Using Asterisk External Media Over Raw Packet Sniffing

## Status
**Accepted**

## Context
ECFD requires access to real-time, two-way audio streams from active VoIP calls for AI transcription and voice authenticity analysis. Two primary methods were considered:
1. **Raw Packet Sniffing (Promiscuous PCAP / libpcap / eBPF):** Passively capturing UDP RTP packets off the network interface.
2. **Asterisk External Media (ARI / Bridge Media):** Using Asterisk's native External Media channels to fork authorized call bridge audio directly over RTP to ECFD's Media Gateway.

## Decision
We chose **Asterisk External Media via ARI**.

## Rationale & Consequences
* **Positives:**
  * Clean application-level authorization and explicit call session binding.
  * Avoids fragile raw packet reassembly, NAT traversal complications, and kernel-level sniffing dependencies.
  * Direct synchronization with Asterisk `StasisStart` and `StasisEnd` lifecycle events.
* **Trade-offs:**
  * Requires Asterisk PBX integration rather than arbitrary, unmanaged network tapping (which matches our controlled enterprise lab scope).
