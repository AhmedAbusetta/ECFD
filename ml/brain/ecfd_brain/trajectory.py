"""Combines the LLM's judgment with the deterministic floor into the call's risk curve.

  * fast up, slow down: a higher LLM risk is accepted at once; a lower one moves the curve
    down by at most MAX_FALL_PER_TURN, so one calm sentence cannot erase a request for a secret
  * floor: once a hard signal has been heard, the risk never goes below FLOOR_RISK again
  * final risk = max(floor, smoothed LLM risk)
  * alert level WATCH / WARNING / ALERT never drops during a call; ALERT fires on
      - final risk >= 80, or
      - final risk >= 70 and the LLM says the risk is rising, or
      - the employee is about to disclose / has disclosed and final risk >= 60, or
      - a hard signal
  * if the LLM fails for a turn (assessment None), the curve keeps its last value and the
    floor still works - the call is never left unprotected
"""

from dataclasses import dataclass, field

from .floor import FLOOR_RISK

MAX_FALL_PER_TURN = 10
LEVELS = [(80, "ALERT"), (60, "WARNING"), (30, "WATCH")]
LEVEL_ORDER = ["NONE", "WATCH", "WARNING", "ALERT"]
DISCLOSING = {"ABOUT_TO_DISCLOSE", "DISCLOSED"}


@dataclass
class RiskTrajectory:
    smoothed: int = 0
    floor: int = 0
    level: str = "NONE"
    hard_signals: list = field(default_factory=list)   # (turn, HardSignal)
    history: list = field(default_factory=list)        # final risk per turn

    def update(self, turn: int, assessment=None, hard_signals: list = ()) -> dict:
        reasons = []
        for s in hard_signals:
            self.hard_signals.append((turn, s))
            reasons.append(f"hard signal: {s.kind}/{s.concept} ({' + '.join(s.words)})")
        if hard_signals:
            self.floor = FLOOR_RISK

        llm_risk = None
        if assessment is not None:
            llm_risk = assessment.risk
            if llm_risk >= self.smoothed:
                self.smoothed = llm_risk
            else:
                self.smoothed = max(llm_risk, self.smoothed - MAX_FALL_PER_TURN)
        else:
            reasons.append("LLM unavailable this turn - floor and last value only")

        final = max(self.floor, self.smoothed)
        previous_level = self.level
        level = next((name for threshold, name in LEVELS if final >= threshold), "NONE")
        if assessment is not None:
            if final >= 70 and assessment.trend == "RISING":
                level = "ALERT"
                reasons.append("high and rising")
            if final >= 60 and assessment.employee_state in DISCLOSING:
                level = "ALERT"
                reasons.append(f"employee {assessment.employee_state.lower()}")
        if hard_signals:
            level = "ALERT"
        if LEVEL_ORDER.index(level) > LEVEL_ORDER.index(self.level):
            self.level = level

        self.history.append(final)
        return {
            "turn": turn,
            "llm_risk": llm_risk,
            "smoothed": self.smoothed,
            "floor": self.floor,
            "risk": final,
            "level": self.level,
            "level_changed": self.level != previous_level,
            "reasons": reasons,
        }
