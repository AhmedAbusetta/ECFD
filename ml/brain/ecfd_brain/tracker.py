"""Stages 7 and 9 - Track the attack stage and score the call. Rules in code, no AI.

All numbers are starting values to tune on DEV calls (project brief).

Stage: normal -> identity -> pressure -> sensitive -> extraction. Only moves forward, can skip.
Score: each tactic counts once per call at its highest confidence (low 0.4, medium 0.7, high 1.0),
       plus 5 points per stage reached, capped at 100.
Level: WATCH >= 30, WARNING >= 60, ALERT >= 80, and ALERT immediately on reaching extraction.
       The level never drops during a call.
"""

from dataclasses import dataclass, field

STAGES = ["normal", "identity", "pressure", "sensitive", "extraction"]

# Which stage each tactic proves the caller has reached.
TACTIC_STAGE = {
    "identity_claim": "identity",
    "authority": "pressure",
    "urgency": "pressure",
    "fear_threat": "pressure",
    "secrecy": "pressure",
    "verification_bypass": "sensitive",
    "remote_access_request": "sensitive",
    "sensitive_action_request": "sensitive",
    "otp_request": "extraction",
    "credential_request": "extraction",
    "payment_request": "extraction",
}

# Points from the project brief. identity_claim is 5, not 20: a claimed role is context, not proof
# of fraud - real staff say "I'm from IT" every day (ADR-0004). It still moves the stage forward.
POINTS = {
    "otp_request": 35,
    "credential_request": 30,
    "payment_request": 25,
    "remote_access_request": 25,
    "verification_bypass": 20,
    "authority": 15,
    "urgency": 15,
    "secrecy": 15,
    "fear_threat": 15,
    "sensitive_action_request": 10,
    "identity_claim": 5,
}
CONFIDENCE_SCALE = {"low": 0.4, "medium": 0.7, "high": 1.0}
POINTS_PER_STAGE = 5
LEVELS = [(80, "ALERT"), (60, "WARNING"), (30, "WATCH")]
LEVEL_ORDER = ["NONE", "WATCH", "WARNING", "ALERT"]


@dataclass
class CallTracker:
    stage: str = "normal"
    level: str = "NONE"
    best: dict = field(default_factory=dict)  # tactic label -> highest confidence scale seen

    def update(self, tactics: list) -> dict:
        """tactics: [{label, confidence, ...}] from one turn. Returns the call state after the turn."""
        previous_stage, previous_level = self.stage, self.level
        for t in tactics:
            label, scale = t["label"], CONFIDENCE_SCALE.get(t["confidence"], 0.4)
            if label not in POINTS:
                continue
            self.best[label] = max(self.best.get(label, 0.0), scale)
            reached = TACTIC_STAGE[label]
            if STAGES.index(reached) > STAGES.index(self.stage):
                self.stage = reached  # forward only, may skip stages

        contributors = sorted(
            ([label, round(POINTS[label] * scale)] for label, scale in self.best.items()),
            key=lambda c: -c[1],
        )
        stage_points = POINTS_PER_STAGE * STAGES.index(self.stage)
        if stage_points:
            contributors.append([f"stage:{self.stage}", stage_points])
        score = min(100, sum(c[1] for c in contributors))

        level = next((name for threshold, name in LEVELS if score >= threshold), "NONE")
        if self.stage == "extraction":
            level = "ALERT"  # alert immediately when the caller asks for the secret/money
        if LEVEL_ORDER.index(level) > LEVEL_ORDER.index(self.level):
            self.level = level  # never drops

        return {
            "stage": self.stage,
            "stage_changed": self.stage != previous_stage,
            "score": score,
            "level": self.level,
            "level_changed": self.level != previous_level,
            "contributors": contributors,
        }
