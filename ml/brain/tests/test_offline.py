"""Offline tests: no network, no API key. Run: python -m unittest discover ml/brain/tests"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from ecfd_brain import normalize, quote_in_text  # noqa: E402
from ecfd_brain.brain import Brain, build_user_message, validate  # noqa: E402


class NormalizeTests(unittest.TestCase):
    def test_alef_forms_and_ya_unify(self):
        self.assertEqual(normalize("أإآا"), "اااا")
        self.assertEqual(normalize("على"), "علي")

    def test_diacritics_and_tatweel_removed(self):
        self.assertEqual(normalize("كـــودٌ"), "كود")

    def test_latin_lowercased_and_kept(self):
        self.assertEqual(normalize("نزل AnyDesk"), "نزل anydesk")

    def test_arabic_digits(self):
        self.assertEqual(normalize("١٢٣"), "123")


class QuoteTests(unittest.TestCase):
    TURN = "ابعتلي الكود اللي جالك في الرسالة دلوقتي!"

    def test_exact_quote_accepted(self):
        self.assertTrue(quote_in_text("ابعتلي الكود", self.TURN))

    def test_spelling_variants_and_punctuation_accepted(self):
        self.assertTrue(quote_in_text("أبعتلي الكود", self.TURN))
        self.assertTrue(quote_in_text("دلوقتي", self.TURN))

    def test_invented_quote_rejected(self):
        self.assertFalse(quote_in_text("قولي الباسورد", self.TURN))
        self.assertFalse(quote_in_text("", self.TURN))


class ValidateTests(unittest.TestCase):
    TURN = "ابعتلي الكود اللي جالك دلوقتي"

    def test_safeguards(self):
        raw = {"tactics": [
            {"label": "otp_request", "confidence": "high", "quote": "ابعتلي الكود"},
            {"label": "urgency", "confidence": "medium", "quote": "حالا"},           # not in the turn
            {"label": "impersonation", "confidence": "high", "quote": "الكود"},       # not a label
            {"label": "otp_request", "confidence": "low", "quote": "الكود"},         # duplicate
            {"label": "secrecy", "confidence": "92%", "quote": "الكود"},             # bad confidence
        ]}
        accepted, dropped = validate(raw, self.TURN)
        self.assertEqual([t.label for t in accepted], ["otp_request"])
        self.assertEqual(len(dropped), 3)

    def test_empty_answer(self):
        self.assertEqual(validate({"tactics": []}, self.TURN), ([], []))


class FakeProvider:
    model = "fake"

    def __init__(self, args):
        self.args = args
        self.seen = None

    def call(self, user_message):
        self.seen = user_message
        return self.args


class BrainTests(unittest.TestCase):
    def test_classify_uses_context_and_validates(self):
        provider = FakeProvider({"tactics": [{"label": "otp_request", "confidence": "high", "quote": "ابعتهولي"}],
                                 "needs_more_context": False})
        result = Brain(provider).classify("ابعتهولي بسرعة", ["أ", "ب", "هيوصلك كود"])
        self.assertEqual(result.labels, {"otp_request"})
        # only the last 2 context turns are sent
        self.assertIn("هيوصلك كود", provider.seen)
        self.assertNotIn("- ا\n", provider.seen)

    def test_user_message_shape(self):
        msg = build_user_message("نص", [])
        self.assertIn("(none)", msg)
        self.assertIn("CURRENT CALLER TURN", msg)


if __name__ == "__main__":
    unittest.main()
