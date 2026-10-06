"""ECFD brain: labels social-engineering tactics in one caller turn using an LLM."""

from .brain import TACTIC_LABELS, Brain, BrainResult, Tactic
from .normalize import normalize, quote_in_text

__all__ = ["TACTIC_LABELS", "Brain", "BrainResult", "Tactic", "normalize", "quote_in_text"]
