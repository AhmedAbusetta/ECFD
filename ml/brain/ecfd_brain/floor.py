"""Deterministic risk floor - the hard signals the LLM can never talk its way out of.

The call analyst (analyst.py) judges the whole call; this module only answers one narrow,
explainable question per CALLER turn: did the caller just ask for something no legitimate
caller asks for by phone? If yes, the call's risk can never fall below FLOOR_RISK again.

Hard signals (caller turns only):
  SECRET    a request verb (تديني، ابعتلي، قولّي، اقرالي، محتاج …) + a secret object (الكود، الcvv، الباسورد …)
  PAYMENT   a payment verb (حوّل، ادفع) + a money word (الفلوس، انستاباي، فودافون كاش …)
  REMOTE    a remote-control app (إني ديسك، تيم فيوير) or the ID it shows
  BYPASS    a phrase that skips verification or moves money to a "safe" account

Words come from data/lexicon/egyptian_call_lexicon.csv (one row per stem). Arabic matching
allows attached prefixes/suffixes (ال، و، ب … ي، ك، ها، هولي …) so one entry covers its forms.
"""

import csv
import re
from dataclasses import dataclass
from pathlib import Path

from .normalize import normalize

LEXICON_CSV = Path(__file__).resolve().parents[3] / "data" / "lexicon" / "egyptian_call_lexicon.csv"
FLOOR_RISK = 80

_PREFIXES = "وال|بال|فال|لل|ال|و|ف|ب|ل|ك|هت|بت|ه|ات|ا|ي|ت|ن|م|ما"
_SUFFIXES = "هولي|هالي|هالك|هولك|هاله|لها|لهم|لي|لك|له|ها|هم|نا|كم|كي|ني|وا|ين|ات|ي|ك|ه|ش|ة|و"
_PUNCT = re.compile(r"[\.,!?؟،؛:\"'()\[\]{}«»…\-–—]")
_MIXED = re.compile(r"(?<=[؀-ۿ])(?=[a-z0-9])|(?<=[a-z0-9])(?=[؀-ۿ])")

REQUEST_EXTRA = {"محتاج", "عايز", "عاوز"}  # "محتاج الباسورد" is a request even without a request verb


@dataclass
class HardSignal:
    kind: str       # SECRET | PAYMENT | REMOTE | BYPASS
    concept: str    # e.g. CVV, PASSWORD, SAFE_ACCOUNT
    words: tuple    # the matched words, for the explanation


def _clean(text: str) -> str:
    text = _PUNCT.sub(" ", normalize(text))
    text = _MIXED.sub(" ", text)  # "الcvv" -> "ال cvv"
    return re.sub(r"\s+", " ", text).strip()


def _pattern(variant: str):
    v = _clean(variant)
    if not v:
        return None
    if v.isascii():
        return re.compile(rf"(?<![a-z0-9]){re.escape(v)}(?![a-z0-9])")
    return re.compile(rf"(?:^|(?<=\s))(?:{_PREFIXES})?{re.escape(v)}(?:{_SUFFIXES})?(?=\s|$)")


class _Group:
    def __init__(self):
        self.entries = []  # (concept, word, [patterns])

    def add(self, concept: str, word: str, variants: list):
        patterns = [p for p in (_pattern(v) for v in [word, *variants]) if p]
        if patterns:
            self.entries.append((concept, word, patterns))

    def find(self, text: str) -> list:
        return [(concept, word) for concept, word, patterns in self.entries if any(p.search(text) for p in patterns)]


class HardSignalFloor:
    def __init__(self, lexicon_csv: Path = LEXICON_CSV):
        self.request, self.secret = _Group(), _Group()
        self.pay_verb, self.money = _Group(), _Group()
        self.remote, self.standalone = _Group(), _Group()
        with open(lexicon_csv, encoding="utf-8-sig") as f:
            for row in csv.DictReader(f):
                if row["Status"] == "Rejected":
                    continue
                word, concept, cat, role = row["Word (as spoken)"], row["Concept"], row["Category"], row["Risk role"]
                variants = [v for v in row["Other spellings (| separated)"].split("|") if v.strip()]
                if cat == "Request verb" and role == "HARD":
                    (self.pay_verb if concept == "PAYMENT" else self.request).add(concept, word, variants)
                elif word in REQUEST_EXTRA or concept in ("FORWARD", "SCREENSHOT"):
                    self.request.add(concept, word, variants)
                elif cat == "Secret object":
                    (self.remote if concept == "REMOTE_ID" else self.secret).add(concept, word, variants)
                elif concept == "REMOTE_APP" and role == "HARD":
                    self.remote.add(concept, word, variants)
                elif concept in ("VERIFICATION_BYPASS", "SAFE_ACCOUNT"):
                    self.standalone.add(concept, word, variants)
                if cat == "Money & payment":
                    self.money.add(concept, word, variants)

    def check(self, text: str, speaker: str) -> list:
        """Hard signals in one turn. Only the CALLER's turns can raise them."""
        if speaker.upper() != "CALLER":
            return []
        t = _clean(text)
        signals = []
        requests = self.request.find(t)
        if requests:
            for concept, word in self.secret.find(t):
                signals.append(HardSignal("SECRET", concept, (requests[0][1], word)))
        pay = self.pay_verb.find(t)
        if pay:
            money = self.money.find(t)
            if money:
                signals.append(HardSignal("PAYMENT", "PAYMENT", (pay[0][1], money[0][1])))
        for concept, word in self.remote.find(t):
            signals.append(HardSignal("REMOTE", concept, (word,)))
        for concept, word in self.standalone.find(t):
            signals.append(HardSignal("BYPASS", concept, (word,)))
        return signals
