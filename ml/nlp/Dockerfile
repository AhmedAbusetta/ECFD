# Build from the repository root (the rules and their word list live outside ml/nlp):
#   docker build -f ml/nlp/Dockerfile -t ecfd-nlp .
FROM python:3.10-slim

WORKDIR /app

COPY ml/nlp/requirements.txt ml/nlp/requirements.txt
RUN pip install --no-cache-dir -r ml/nlp/requirements.txt

COPY ml/nlp ml/nlp
COPY ml/brain/ecfd_brain ml/brain/ecfd_brain
COPY data/lexicon/egyptian_call_lexicon.csv data/lexicon/egyptian_call_lexicon.csv

WORKDIR /app/ml/nlp
EXPOSE 8002

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8002"]
