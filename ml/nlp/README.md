# ECFD — Instant request check (rules)

* **Port:** `8002`
* **Route:** `POST /v1/nlp/analyze`

Deterministic check on one caller sentence: did the caller ask for something no legitimate caller
asks for by phone (a code, a password, a payment to a "safe" account, a remote-control app)? It runs
the same hard-signal rules as the AI call analyst (`ml/brain/ecfd_brain/floor.py`, word list in
`data/lexicon/`). Single words never count on their own; context is judged by the analyst.

### Running locally (repo root):
```bash
uvicorn app:app --app-dir ml/nlp --port 8002
```
