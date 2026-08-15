# ECFD — Speech AI / ASR Microservice

* **Owner:** Member 2 (Speech AI Engineer)
* **Port:** `8001`
* **Route:** `POST /v1/asr/analyze`

### Running Locally:
```bash
# Set mock mode for fast development
export ML_USE_MOCK_MODE=true
uvicorn app:app --port 8001 --reload
```
