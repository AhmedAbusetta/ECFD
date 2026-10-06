"""Stage 5 - Clean: normalise Egyptian Arabic text so the same words compare equal.

Like ironing clothes before comparing them: different spellings of the same letter
(أ / إ / آ / ا) or added vowel marks should not make two identical words look different.
English words are kept as spoken, only lower-cased.
"""

import re

# Short vowel marks (fatha, damma, kasra, sukun, shadda, tanween...) and the superscript alef.
_DIACRITICS = re.compile(r"[ً-ْٰ]")
_TATWEEL = "ـ"  # the stretching character in "كـــود"
_ALEF_FORMS = str.maketrans({"أ": "ا", "إ": "ا", "آ": "ا", "ٱ": "ا"})
_YA_FORMS = str.maketrans({"ى": "ي"})
_ARABIC_DIGITS = str.maketrans("٠١٢٣٤٥٦٧٨٩", "0123456789")
# Punctuation that should not decide whether a quote matches (Arabic and Latin).
_PUNCTUATION = re.compile(r"[\.,!?؟،؛:\"'()\[\]{}«»…\-–—]")
_SPACES = re.compile(r"\s+")


def normalize(text: str) -> str:
    """Normalised form used for display-independent comparison and for the LLM input."""
    text = _DIACRITICS.sub("", text)
    text = text.replace(_TATWEEL, "")
    text = text.translate(_ALEF_FORMS).translate(_YA_FORMS).translate(_ARABIC_DIGITS)
    text = text.lower()  # only affects Latin letters
    return _SPACES.sub(" ", text).strip()


def _for_matching(text: str) -> str:
    return _SPACES.sub(" ", _PUNCTUATION.sub(" ", normalize(text))).strip()


def quote_in_text(quote: str, text: str) -> bool:
    """True if `quote` really appears in `text` (after normalisation, ignoring punctuation).

    This is the safeguard against invented quotes: a label whose evidence is not
    actually in the sentence is dropped.
    """
    q = _for_matching(quote)
    return bool(q) and q in _for_matching(text)
