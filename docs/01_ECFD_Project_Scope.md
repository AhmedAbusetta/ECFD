# ECFD — Egyptian Conversational Fraud Defense
## Project Definition, Scope & System Behavior

**Document type:** Team Project Brief  
**Project horizon:** One academic year  
**Target:** Graduation-project MVP / technical prototype  
**Team size:** 5–7 students

---

## 1. What is ECFD?

**ECFD (Egyptian Conversational Fraud Defense)** is a real-time security system designed to detect **social-engineering fraud while an authorized voice call is still happening**.

The system is designed around a simple observation:

> Traditional security controls often protect accounts, devices, networks, and transactions. Social engineering can bypass those controls by convincing a legitimate employee to perform a dangerous action voluntarily.

ECFD therefore monitors an authorized enterprise VoIP conversation and continuously analyzes:

1. **What is being said?** — speech recognition / ASR
2. **What conversational tactics are being used?** — NLP social-engineering classification
3. **Does the voice contain evidence of synthetic/manipulated speech?** — audio anti-spoofing
4. **Where is the conversation in the attack lifecycle?** — attack progression
5. **How dangerous is the combined evidence?** — explainable risk fusion

The MVP is not intended to be a production telecom product. It is a controlled, reproducible prototype demonstrating that these signals can be combined into a live security decision.

---

# 2. The Core Problem

Consider an employee receiving this call:

> "Hello, I'm from IT. We detected a problem with your account. You need to fix it immediately. I just sent you a verification code. Read the code to me."

No endpoint has necessarily been compromised.

The employee may still be authenticated correctly.

The device may be clean.

The network may be normal.

The transaction may not have happened yet.

The attack is occurring **inside the conversation**.

ECFD tries to recognize the attack before the final harmful action.

---

# 3. What ECFD Does

At a high level:

```text
Authorized VoIP Call
        |
        v
   Audio Stream
        |
        +------------------+
        |                  |
        v                  v
       ASR            Voice Anti-Spoof
        |
        v
   Live Transcript
        |
        v
   NLP Tactic Detection
        |
        +------------------+
                           |
                           v
                 Evidence Fusion
                           |
                           v
                 Attack Progression
                           |
                           v
                    Risk Score
                           |
                           v
                    Alert / Policy
                           |
                           v
                  Live Dashboard
```

The system continuously updates its assessment instead of waiting until the call ends.

---

# 4. What the MVP Demonstrates

The MVP should support a controlled VoIP environment with two SIP endpoints:

- **Employee**
- **Caller / attacker**

Asterisk acts as the controlled PBX.

The call media is authorized for analysis and is forwarded to ECFD.

The final demonstration is a real call:

```text
Attacker Softphone
       |
       | SIP
       v
    Asterisk
       |
       | SIP/RTP
       v
Employee Softphone

Asterisk
   |
   | authorized media copy
   v
ECFD
```

ECFD analyzes the live call and displays the result on a dashboard.

---

# 5. What ECFD Does NOT Try to Be

The following are explicitly outside the graduation MVP:

- Intercepting arbitrary cellular calls
- Breaking into telecom networks
- Building a mobile carrier
- Building a proprietary SIP stack
- Training a foundation ASR model from scratch
- Training a deepfake model from scratch
- Supporting every language
- Detecting every possible fraud technique
- Enterprise-scale Kubernetes deployment
- Multi-region high availability
- Full SIEM replacement
- Full call-center product
- Automated financial transaction blocking in real banks

The project is a **controlled technical prototype**.

---

# 6. MVP Attack Scope

The first version should focus on a small, defensible taxonomy.

Recommended tactic classes:

| Code | Tactic |
|---|---|
| IMPERSONATION | Caller claims to be another trusted person/entity |
| AUTHORITY | Uses organizational authority |
| URGENCY | Creates artificial time pressure |
| OTP_REQUEST | Requests one-time password / verification code |
| CREDENTIAL_REQUEST | Requests password, PIN, or secret |
| PAYMENT_REQUEST | Requests financial action |
| REMOTE_ACCESS | Requests remote-control/software installation |
| SECRECY | Tells victim not to consult another person |
| VERIFICATION_BYPASS | Attempts to bypass normal verification |
| SENSITIVE_ACTION | Attempts to make the employee perform a sensitive action |

The NLP model may output multiple tactics for one utterance.

Example:

```json
{
  "tactics": [
    {"type": "OTP_REQUEST", "confidence": 0.97},
    {"type": "URGENCY", "confidence": 0.86}
  ]
}
```

---

# 7. Attack Progression

ECFD should not treat every suspicious phrase as an independent alarm.

The important concept is **progression**.

Example:

```text
Normal conversation
       |
       v
Identity claim
       |
       v
Authority establishment
       |
       v
Pressure / urgency
       |
       v
Sensitive request
       |
       v
Credential / OTP request
       |
       v
Verification bypass / secrecy
```

The progression engine stores the current stage and the evidence that caused each transition.

Example:

```text
Stage: SENSITIVE_ACTION_REQUEST

Evidence:
- IMPERSONATION: 0.93
- AUTHORITY: 0.88
- URGENCY: 0.91
- OTP_REQUEST: 0.97
```

This is more meaningful than:

> "The word OTP appeared."

---

# 8. Risk Score

The MVP uses an explainable score from 0–100.

A conceptual fusion function is:

```text
Risk =
    Content Risk
  + Progression Risk
  + Voice Authenticity Risk
  + Context Risk
  - Confidence / uncertainty adjustments
```

The exact formula is an engineering decision to be validated experimentally.

The system must expose **why** risk increased.

Example:

```text
Risk: 91 / 100

Contributors:
+31 OTP request
+19 impersonation
+14 urgency
+12 attack progression
+15 suspicious voice evidence
```

The project should avoid claiming that this score is a universal probability of fraud. It is a calibrated **prototype risk score**.

---

# 9. Normal Call Behavior

A successful system must also demonstrate benign calls.

Example:

> "Are we still meeting at five?"

Expected result:

```text
Risk: LOW

No suspicious tactics detected.
No attack progression.
```

This is important because a fraud detector that simply flags all calls is useless.

---

# 10. System Output

The dashboard should show:

### Call

- Call ID
- Caller
- Employee
- Start time
- Duration
- Current state

### Live transcript

Partial and final transcript segments.

### Detected tactics

Each with:

- tactic type
- confidence
- timestamp

### Voice authenticity

- spoof/suspicion score
- model confidence
- analyzed windows

### Attack progression

- current stage
- previous stages
- transition evidence

### Risk

- current score
- score history
- top contributing evidence

### Alert

Example:

```text
CRITICAL

Possible credential-extraction attack.

Detected:
- Identity impersonation
- Urgency
- OTP request
- Verification bypass
```

---

# 11. The Graduation-Day Success Condition

The project is considered successful if the team can reliably demonstrate:

### Scenario A — Benign

Real VoIP call -> ECFD receives media -> transcript -> low risk.

### Scenario B — Social engineering

Real VoIP call -> transcript -> tactics -> progression -> high risk.

### Scenario C — Synthetic/manipulated voice

Controlled synthetic/replayed voice -> anti-spoof evidence -> combined risk.

The important point is that the system works **live**, not from a prerecorded dashboard simulation.

---

# 12. Project Boundaries

## Input

Authorized voice media from a controlled SIP/VoIP environment.

## Processing

- Audio preprocessing
- ASR
- NLP tactic classification
- Voice anti-spoofing
- Context lookup
- Attack progression
- Risk fusion

## Output

- Real-time risk score
- Explainable evidence
- Attack stage
- Dashboard alert
- Persisted investigation record

---

# 13. What Makes the Project Technically Interesting?

ECFD combines several disciplines into one real-time security pipeline:

```text
Telecommunications
      +
Real-time backend systems
      +
Speech AI
      +
NLP
      +
Audio security
      +
Cybersecurity
      +
Data engineering
      +
Visualization
```

The graduation contribution is not a claim that every individual component is novel.

The engineering contribution is the **real-time multimodal security architecture and evaluation of conversational attack progression**.

---

# 14. MVP Definition of Done

The MVP is complete when:

- Two SIP endpoints can establish a call.
- Asterisk routes the call.
- Authorized media reaches ECFD.
- ECFD creates a call session.
- Audio reaches the ASR service.
- Live transcript events reach ASP.NET.
- NLP identifies the selected tactics.
- Anti-spoofing produces voice evidence.
- Evidence is persisted.
- Attack progression updates.
- Risk is recalculated.
- SignalR pushes updates to the dashboard.
- A benign scenario remains low risk.
- At least several controlled fraud scenarios reach high risk.
- Evaluation metrics are calculated on held-out test data.
- The entire demonstration can be reproduced from a documented deployment.

That is the target.
