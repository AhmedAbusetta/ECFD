# ADR 0004 — Identity Claims Are Context, Not Evidence of Fraud

## Status
**Accepted** (October 2026)

## Context
The original taxonomy labelled phrases such as «أنا من الدعم الفني» ("I'm from IT support") or «أنا من البنك» as `IMPERSONATION` and scored them as fraud evidence (+20 points, plus progression points for the `IdentityClaim` stage).

This is wrong in an enterprise setting: genuine IT staff, bank officers and managers introduce themselves this way every day. From the words alone ECFD cannot know whether a claimed identity is true. Scoring the claim itself:

* produces false positives on legitimate calls (a real IT call repeating its introduction reached CRITICAL in testing),
* labels a person as an impersonator before there is any evidence of deceit, and
* misses the real signal, which is the **combination** of a claimed identity with a request that a genuine holder of that identity would not make.

## Decision
1. Rename the tactic to **`IDENTITY_CLAIM`** (wire label `IDENTITY_CLAIM`; legacy `IMPERSONATION` is still accepted).
2. Treat signals by role:
   * **Context** — `IDENTITY_CLAIM`: near-zero standalone weight.
   * **Pressure** — `URGENCY`, `AUTHORITY`, `SECRECY`: small standalone weight.
   * **Requests** — `OTP_REQUEST`, `CREDENTIAL_REQUEST`, `PAYMENT_REQUEST`, `VERIFICATION_BYPASS` (never legitimate by phone) and `REMOTE_ACCESS`, `SENSITIVE_ACTION` (sometimes legitimate).
3. Put most of the risk into **explained combinations**:
   * `IDENTITY_CLAIM + <never-legitimate request>` — e.g. "IT" asking for an OTP.
   * `<pressure cue> + <sensitive request>` — e.g. secrecy + AnyDesk.
   Each combination appears as its own contributor on the dashboard.
4. The `IdentityClaim` stage adds no progression points; it only records that an identity was claimed. A never-legitimate request escalates to `CredentialExtraction` from **any** stage, including directly from `Normal`.

## Consequences
* Legitimate IT/bank calls stay Low unless they turn into forbidden requests or pressure + sensitive requests (verified by scenario tests in `ECFD.Tests/ProgressionEngineTests.cs`).
* The explanation shown to an analyst reads as a reason ("claimed IT identity **and** asked for OTP") rather than an accusation.
* The dataset annotation guideline changes: annotators mark `IDENTITY_CLAIM` whenever a caller states a role, regardless of whether the scenario is an attack. Whether the call is fraudulent is the scenario-level label (`attackType`), not an utterance tactic.
* All weights (`RiskEngine.cs`) are **initial engineering values** chosen to satisfy the scenario tests; they must be calibrated on the ECFD-30 validation split. Known gap: "fake IT + secrecy + remote access" currently scores Medium (57) and probably belongs in High.
* Future work: verify claims against context the system can actually check (caller extension vs. the real IT directory, internal vs. external trunk). A claim from an external number to be "internal IT" is strong evidence; the same claim from the IT department's extension is not.
