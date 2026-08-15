"""
ECFD NLP / Conversational Intelligence Microservice
FastAPI service providing multi-label social engineering tactic classification on transcript utterances.
"""

import os
import time
from typing import List, Optional
from fastapi import FastAPI
from pydantic import BaseModel

app = FastAPI(
    title="ECFD NLP Microservice",
    description="Multi-label conversational fraud tactic classifier",
    version="1.0.0"
)

USE_MOCK = os.getenv("ML_USE_MOCK_MODE", "true").lower() == "true"
MODEL_NAME = os.getenv("NLP_MODEL_NAME", "UBC-NLP/MARBERT")


class TacticMatch(BaseModel):
    type: str
    confidence: float


class NlpRequest(BaseModel):
    sessionId: str
    segmentId: str
    text: str
    language: Optional[str] = "ar"


class NlpResponse(BaseModel):
    segmentId: str
    tactics: List[TacticMatch]
    modelVersion: str
    inferenceDurationMs: float


# Rule-based tactic matching dictionary (serves as immediate robust baseline)
RULES = {
    "IMPERSONATION": ["it", "الدعم الفني", "البنك", "خدمة العملاء", "المرور", "الضرائب", "أنا فلان", "أنا من"],
    "AUTHORITY": ["لازم", "قرار", "إيقاف الحساب", "تعليمات الإدارة", "أمر مباشر", "مخالفة"],
    "URGENCY": ["حالاً", "دلوقتي", "بسرعة", "قبل ما يقفل", "في خلال دقائق", "مشكلة فورية", "عاجل"],
    "OTP_REQUEST": ["كود", "رمز", "otp", "الرسالة اللي جاتلك", "ست أرقام", "ارقام التحقق"],
    "CREDENTIAL_REQUEST": ["الباسورد", "كلمة السر", "الرقم السري", "pin", "بيانات الكارت", "cvv"],
    "PAYMENT_REQUEST": ["تحويل", "فودافون كاش", "instapay", "انستاباي", "فلوس", "رسوم"],
    "REMOTE_ACCESS": ["anydesk", "teamviewer", "برنامج التحكم", "نزل البرنامج", "افتح اللينك"],
    "SECRECY": ["ما تقولش لحد", "بيننا", "سري", "ماتقفلش الخط", "ماتكلمش الفرع"],
    "VERIFICATION_BYPASS": ["تخطي", "مش لازم تروح الفرع", "انا هعملهالك من هنا", "تأكيد يدوي"],
    "SENSITIVE_ACTION": ["اضغط موافق", "وافق على الرسالة", "اكد العملية", "اقبل الطلب"]
}


def rule_match(text: str) -> List[TacticMatch]:
    text_lower = text.lower()
    matches = []
    for tactic, keywords in RULES.items():
        for kw in keywords:
            if kw in text_lower:
                matches.append(TacticMatch(type=tactic, confidence=0.92))
                break
    return matches


@app.get("/health")
def health():
    return {
        "status": "HEALTHY",
        "service": "nlp",
        "mockMode": USE_MOCK,
        "model": MODEL_NAME
    }


@app.post("/v1/nlp/analyze", response_model=NlpResponse)
def analyze_text(req: NlpRequest):
    start_time = time.time()
    
    # Run rule baseline / mock matching
    tactics = rule_match(req.text)
    
    return NlpResponse(
        segmentId=req.segmentId,
        tactics=tactics,
        modelVersion=f"{MODEL_NAME}-rule-baseline",
        inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
    )
