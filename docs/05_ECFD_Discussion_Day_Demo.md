# ECFD — Graduation Discussion Day
## Live Demonstration Plan

The goal of the presentation is to make the examiner understand ECFD in the first few minutes:

> A real VoIP call is happening. ECFD receives the authorized media, analyzes the conversation in real time, recognizes social-engineering behavior, combines multiple signals, and warns the operator before the attack reaches its final action.

---

# 1. Physical Setup

Recommended:

```text
                  PROJECTOR / LARGE DISPLAY
                           |
                           v
                    ECFD DASHBOARD


     ATTACKER DEVICE             EMPLOYEE DEVICE
       SIP 1002                     SIP 1001
            \                         /
             \                       /
              \                     /
                    Asterisk
                       |
                       | authorized
                       | media
                       v
                ECFD Backend
                       |
          +------------+------------+
          |            |            |
         ASR          NLP       Anti-Spoof
          |            |            |
          +------------+------------+
                       |
                  Risk Engine
                       |
                    SignalR
                       |
                   Dashboard
```

The exact number of physical machines can be reduced by running several services on one machine.

---

# 2. What the Audience Should See

The audience should see:

1. two people actually talking
2. the dashboard receiving the call
3. transcript appearing while they talk
4. tactics appearing
5. risk increasing
6. attack stage changing
7. an alert appearing

The demonstration should not depend on pre-populated database records.

---

# 3. Demo Preparation

Before the presentation:

### Telephony

- Verify endpoints register.
- Verify call works.
- Verify media forwarding.
- Verify call termination.

### AI

- Warm up models.
- Verify GPU memory.
- Verify model versions.
- Verify inference latency.

### Backend

- Verify database.
- Verify SignalR.
- Verify health endpoints.
- Clear old demo sessions.

### Dashboard

- Open the live-monitoring screen.
- Confirm browser connection.
- Confirm no stale sessions.

---

# 4. Demo Scenario 1 — Benign Call

The first demonstration establishes that ECFD does not automatically classify every call as fraud.

Caller:

> "Hey, are we still meeting at five?"

Employee:

> "Yes, I'll be there."

Dashboard should show approximately:

```text
CALL ACTIVE

Risk: 4 / 100
Severity: LOW

Detected tactics:
None
```

The exact score will depend on the trained system.

The important behavior is:

**normal conversation remains low risk.**

---

# 5. Demo Scenario 2 — Human Social Engineering

Start a second call.

Attacker:

> "Hello, I'm Ahmed from the IT department."

Expected dashboard:

```text
Risk increasing

IDENTITY_IMPERSONATION
```

Continue:

> "We detected a problem with your account and we need to fix it immediately."

Expected:

```text
IDENTITY_IMPERSONATION
AUTHORITY
URGENCY
```

Risk increases.

Continue:

> "I just sent a verification code to your phone. Read it to me."

Expected:

```text
OTP_REQUEST
CREDENTIAL_REQUEST
SENSITIVE_ACTION
```

Risk should move into HIGH/CRITICAL territory.

---

# 6. Show Attack Progression

Do not only show the number.

Show:

```text
ATTACK PROGRESSION

✓ Identity Claim
       |
       v
✓ Authority
       |
       v
✓ Urgency
       |
       v
✓ Sensitive Action
       |
       v
🔴 Credential Extraction
```

The examiner should immediately understand why the system considers the conversation dangerous.

---

# 7. Explainable Risk

Show a panel such as:

```text
RISK: 91 / 100

TOP CONTRIBUTORS

OTP_REQUEST          +31
IMPERSONATION        +19
URGENCY              +14
ATTACK PROGRESSION   +12
VOICE EVIDENCE       +15
```

The actual contributions must come from the implemented model.

Do not hardcode a fake explanation for the final demonstration.

---

# 8. Demo Scenario 3 — Synthetic / Replayed Voice

If the anti-spoof model is reliable enough, run a controlled synthetic or replayed voice scenario.

The caller uses a prepared voice sample.

The system should show:

```text
VOICE AUTHENTICITY

Suspicion: HIGH

Spoof probability: 0.87
```

Then combine it with conversational evidence.

Example:

```text
Conversation evidence: HIGH
Voice evidence: HIGH
Final risk: CRITICAL
```

This demonstrates multimodal fusion.

---

# 9. What NOT to Claim

Do not say:

> "ECFD knows this is definitely an AI voice."

Say:

> "The anti-spoof model found evidence consistent with synthetic or manipulated speech."

Do not say:

> "91 means 91% probability of fraud."

Say:

> "91 is the prototype's calibrated risk score."

Do not say:

> "ECFD detects all social-engineering attacks."

Say:

> "The MVP targets the defined attack taxonomy and was evaluated on the corresponding test scenarios."

Scientific honesty makes the project stronger.

---

# 10. Suggested Presentation Sequence

## Minute 0–2

Explain the problem.

```text
Endpoint security can be perfectly healthy
while an employee is socially engineered into
performing a dangerous action.
```

## Minute 2–4

Show architecture.

```text
VoIP
 -> audio
 -> ASR
 -> NLP
 -> anti-spoof
 -> progression
 -> risk
 -> dashboard
```

## Minute 4–7

Perform benign call.

## Minute 7–12

Perform fraud call.

Let the dashboard update live.

## Minute 12–15

Explain the architecture.

## Minute 15–18

Show evaluation results.

## Minute 18–20

Discuss limitations and future work.

---

# 11. The "Wow" Moment

Do not start with slides.

Start with:

> "We are going to make a real phone call."

Make the call.

Let the examiner watch the transcript appear.

Then say:

> "At this point the system has not been told that this is a fraud scenario."

Continue the attack.

When the OTP request is detected:

```text
CRITICAL
OTP / CREDENTIAL EXTRACTION
```

Then explain:

> "The score did not come from a keyword rule. It increased because multiple pieces of evidence accumulated and the conversation progressed through our attack state model."

That is the core of the demonstration.

---

# 12. Backup Demonstration

A live demo can fail.

Always maintain a second mode:

```text
LIVE MODE
   |
   v
REAL SIP CALL

BACKUP MODE
   |
   v
PRE-RECORDED AUTHORIZED CALL MEDIA
   |
   v
Same ECFD analysis pipeline
```

The backup must still run through the real backend, models, risk engine, and dashboard.

Do not make a fake video of the dashboard.

The purpose is to protect against:

- Wi-Fi failure
- microphone failure
- softphone registration failure
- GPU failure
- unexpected latency

---

# 13. Demo Architecture on Screen

Keep one architecture diagram available.

```text
                  SIP CALL
                     |
                     v
                ASTERISK PBX
                     |
              Authorized Media
                     |
                     v
              MEDIA GATEWAY
                     |
          +----------+----------+
          |          |          |
         ASR        NLP      ANTI-SPOOF
          |          |          |
          +----------+----------+
                     |
                 EVIDENCE
                     |
                     v
            ATTACK PROGRESSION
                     |
                     v
                RISK FUSION
                     |
                     v
              POLICY / ALERT
                     |
                     v
                 SIGNALR
                     |
                     v
               LIVE DASHBOARD
```

---

# 14. Questions the Professor May Ask

## "How do you receive the call?"

Answer:

> "For the prototype, we use a controlled SIP environment with Asterisk as the PBX. The authorized call media is exposed to our analysis gateway using Asterisk's media integration mechanisms."

## "Are you intercepting real cellular calls?"

Answer:

> "No. The graduation prototype intentionally uses a controlled VoIP environment. This makes the system reproducible and avoids dependence on carrier infrastructure."

## "Why not analyze the call after it ends?"

Answer:

> "The security objective is intervention before the sensitive action. Therefore the system processes the conversation continuously."

## "Why use Python?"

Answer:

> "The application and real-time orchestration layer is ASP.NET Core. Python is isolated to the components where the ML ecosystem is strongest: model training, GPU inference, speech processing, NLP, and anti-spoofing."

## "What if one ML service fails?"

Answer:

> "Evidence is treated as independent. The backend degrades gracefully and records unavailable signals rather than treating a failed detector as evidence of safety."

## "How do you know it works?"

Answer:

> "We evaluate individual models and the end-to-end system on held-out benign and attack scenarios, including precision, recall, F1, false positives, detection lead time, and latency."

---

# 15. Final Demonstration Definition

The ideal final moment is:

```text
REAL CALL

Attacker:
"أنا من الـIT..."

              ↓

ECFD:
Identity Impersonation

              ↓

Attacker:
"لازم نحل المشكلة حالاً..."

              ↓

ECFD:
Urgency
Authority

              ↓

Attacker:
"قولّي الـOTP..."

              ↓

ECFD:

╔══════════════════════════╗
║      🔴 CRITICAL         ║
║                          ║
║ Risk: 91 / 100           ║
║                          ║
║ OTP REQUEST              ║
║ IMPERSONATION            ║
║ URGENCY                  ║
║                          ║
║ ATTACK STAGE:            ║
║ CREDENTIAL EXTRACTION    ║
╚══════════════════════════╝
```

The examiner has just watched an attack become visible **while it was happening**.

That is the demonstration ECFD is being built toward.

---

# 16. Final Rule for Demo Day

The system should demonstrate **the actual architecture**.

Do not fake:

- transcripts
- risk scores
- tactic detections
- attack stages
- model outputs

A small, honest working system is much stronger than a huge fake one.

The goal is:

> **Real call → real media → real inference → real evidence → real risk → real dashboard.**
