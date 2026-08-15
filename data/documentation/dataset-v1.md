# ECFD Dataset Specification (v1.0)

* **Owner:** Member 5 (Data, Evaluation & Security Engineer)
* **Target Scenarios:** 30 recorded dialogues (approx. 300–400 labeled utterances)

### Split Architecture:
* **Train Set (60%):** 18 scenarios used for fine-tuning the NLP MARBERT classifier.
* **Validation Set (20%):** 6 scenarios used for hyperparameter tuning & threshold calibration.
* **Held-Out Test Set (20%):** 6 scenarios strictly isolated for final benchmark reporting (never seen during training).

### Scenario Distribution:
1. **Benign Calls (30%):** Everyday legitimate workplace & customer conversations.
2. **Social Engineering Attacks (50%):** IT impersonation, bank fraud, OTP extraction, urgent invoice requests.
3. **Synthetic / Replay Attacks (20%):** AI-cloned voices (ElevenLabs / VITS) and compressed replayed recordings.
