"""Offline tests for the stage tracker and scorer. Run: python -m unittest discover ml/brain/tests"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from ecfd_brain.tracker import CallTracker  # noqa: E402


def t(label, confidence="high"):
    return {"label": label, "confidence": confidence, "quote": "x"}


class TrackerTests(unittest.TestCase):
    def test_genuine_it_intro_stays_quiet(self):
        state = CallTracker().update([t("identity_claim")])
        self.assertEqual(state["stage"], "identity")
        self.assertEqual(state["level"], "NONE")
        self.assertEqual(state["score"], 10)  # 5 claim + 5 stage

    def test_classic_otp_attack_alerts_on_extraction(self):
        tr = CallTracker()
        tr.update([t("identity_claim")])
        state = tr.update([t("urgency"), t("fear_threat")])
        self.assertEqual(state["level"], "WATCH")  # 5+15+15 + 10 stage = 45
        state = tr.update([t("otp_request")])
        self.assertEqual(state["stage"], "extraction")
        self.assertEqual(state["level"], "ALERT")
        self.assertTrue(state["level_changed"])

    def test_direct_otp_request_skips_stages_and_alerts(self):
        state = CallTracker().update([t("otp_request", "medium")])
        self.assertEqual(state["stage"], "extraction")
        self.assertEqual(state["level"], "ALERT")

    def test_each_tactic_counts_once_at_highest_confidence(self):
        tr = CallTracker()
        tr.update([t("urgency", "low")])
        tr.update([t("urgency", "high")])
        state = tr.update([t("urgency", "medium")])
        self.assertIn(["urgency", 15], state["contributors"])
        self.assertEqual(state["score"], 15 + 10)  # urgency once (high) + pressure stage

    def test_stage_never_goes_back_and_level_never_drops(self):
        tr = CallTracker()
        tr.update([t("otp_request")])
        state = tr.update([t("identity_claim")])
        self.assertEqual(state["stage"], "extraction")
        self.assertEqual(state["level"], "ALERT")

    def test_score_capped_at_100(self):
        tr = CallTracker()
        state = tr.update([t(l) for l in ("otp_request", "credential_request", "payment_request",
                                          "remote_access_request", "secrecy")])
        self.assertEqual(state["score"], 100)


if __name__ == "__main__":
    unittest.main()
