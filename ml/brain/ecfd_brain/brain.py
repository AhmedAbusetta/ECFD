"""Stage 6 - NLP: an LLM labels the social-engineering tactics in one caller turn.

Input: one cleaned caller turn + up to 2 previous caller turns as context.
Output: tactics [{label, confidence low|medium|high, quote}] and needs_more_context.

Safeguards (from the project brief):
  * forced tool call -> the model must answer in our JSON shape, never free text
  * temperature 0 -> as repeatable as the provider allows
  * every label needs an exact quote from the current turn; invented quotes are dropped
  * three confidence buckets instead of fake percentages
"""

import json
import os
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field

from .normalize import normalize, quote_in_text

TACTIC_LABELS = [
    "identity_claim",            # caller states who they are (context, not proof - ADR-0004)
    "authority",
    "urgency",
    "fear_threat",
    "secrecy",
    "verification_bypass",
    "otp_request",
    "credential_request",
    "payment_request",
    "remote_access_request",
    "sensitive_action_request",
]
CONFIDENCES = ["low", "medium", "high"]

SYSTEM_PROMPT = """You label social-engineering tactics in phone calls to Egyptian bank and company employees.
The text is the CALLER's speech only, transcribed from Egyptian Arabic, often mixed with English words (OTP, CVV, AnyDesk, system).
Transcripts may contain speech-recognition mistakes; judge the meaning, not the spelling.

You are a smoke alarm, not a judge: you never decide whether the call is fraud. You only report which tactics the caller uses in the CURRENT turn.

LABELS (use only these):
- identity_claim: the caller states who they are or whom they represent ("أنا من الدعم الفني", "معاك فلان من البنك", "أنا من خدمة العملاء"). Real employees say this too; label it whenever a role or institution is claimed, true or not.
- authority: invokes power, rank, orders or official rules to make the employee comply ("دي تعليمات الإدارة", "ده أمر مباشر من المدير", "حسب قرار البنك المركزي").
- urgency: pushes to act immediately or within a short deadline ("دلوقتي", "حالاً", "بسرعة", "في خلال عشر دقايق").
- fear_threat: threatens a loss, penalty, closure, legal action or harm ("الحساب هيتقفل", "هتتعرض للمساءلة", "فلوسك هتضيع").
- secrecy: asks the employee to hide the call or not verify with others ("ما تقولش لحد", "خليها بيننا", "ما تكلمش الفرع").
- verification_bypass: tries to skip normal checks, procedures or in-person verification ("مش لازم تروح الفرع", "أنا هخلصهالك من هنا من غير تأكيد").
- otp_request: ASKS for a one-time code / verification code / the digits that arrived by SMS, even without the word "code" ("اقرالي الأرقام اللي جاتلك").
- credential_request: ASKS for a password, PIN, card number, CVV, expiry date, username or similar secret.
- payment_request: ASKS to send, transfer or pay money (InstaPay, Vodafone Cash, bank transfer, fees).
- remote_access_request: ASKS to install or open remote-control software or give screen/device access (AnyDesk, TeamViewer, "نزل البرنامج ده").
- sensitive_action_request: ASKS the employee to perform a risky action on a device or account: approve a prompt, click a link, confirm a transaction, change account settings.

KEY RULES:
1. Request labels (otp_request, credential_request, payment_request, remote_access_request, sensitive_action_request) count ONLY when the caller is ASKING the employee to do or give something. Warnings, advice, refusals, questions about policy and stories do NOT count. "عمرك ما تدي حد الـ OTP" (never give anyone your OTP) -> no label. "ابعتلي الـ OTP" (send me the OTP) -> otp_request.
2. Ordinary work requests are not tactics: asking for a report, a meeting time, or to restart a computer is normal.
3. One turn can have several labels. If nothing applies, return an empty list.
4. Every label needs "quote": the shortest exact words copied from the CURRENT turn that show the tactic. Copy them character by character; never paraphrase, translate, or quote the context turns.
5. Confidence: "high" = explicit and unambiguous; "medium" = likely but somewhat implicit; "low" = possible but you would not bet on it.
6. Use the context turns only to understand the current turn (e.g. what "it" refers to). Do not label tactics that appear only in the context.
7. Set needs_more_context = true when the current turn is too short or ambiguous to label without hearing what comes next (for example "طب ابعتهولي" with no context). Do not guess in that case.

Always answer by calling the report_tactics function."""

TOOL_NAME = "report_tactics"
TOOL_DESCRIPTION = "Report the social-engineering tactics in the current caller turn."
TOOL_PARAMETERS = {
    "type": "object",
    "properties": {
        "tactics": {
            "type": "array",
            "description": "Tactics used by the caller in the CURRENT turn. Empty if none.",
            "items": {
                "type": "object",
                "properties": {
                    "label": {"type": "string", "enum": TACTIC_LABELS},
                    "confidence": {"type": "string", "enum": CONFIDENCES},
                    "quote": {"type": "string", "description": "Exact words copied from the CURRENT turn."},
                },
                "required": ["label", "confidence", "quote"],
            },
        },
        "needs_more_context": {
            "type": "boolean",
            "description": "True if the turn cannot be labelled without hearing more of the call.",
        },
    },
    "required": ["tactics", "needs_more_context"],
}


@dataclass
class Tactic:
    label: str
    confidence: str
    quote: str


@dataclass
class BrainResult:
    tactics: list = field(default_factory=list)            # accepted Tactic objects
    dropped: list = field(default_factory=list)            # (Tactic, reason) rejected by safeguards
    needs_more_context: bool = False
    model: str = ""
    latency_ms: int = 0
    raw: dict = field(default_factory=dict)

    @property
    def labels(self) -> set:
        return {t.label for t in self.tactics}


def build_user_message(turn: str, context: list) -> str:
    ctx = "\n".join(f"- {normalize(c)}" for c in context[-2:]) or "(none)"
    return f"PREVIOUS CALLER TURNS (context only, do not label):\n{ctx}\n\nCURRENT CALLER TURN (label this):\n{normalize(turn)}"


def validate(args: dict, turn: str) -> tuple:
    """Apply the safeguards to the model's raw answer. Returns (accepted, dropped)."""
    accepted, dropped = [], []
    seen = set()
    for item in args.get("tactics") or []:
        t = Tactic(str(item.get("label", "")), str(item.get("confidence", "")), str(item.get("quote", "")))
        if t.label not in TACTIC_LABELS:
            dropped.append((t, "unknown label"))
        elif t.confidence not in CONFIDENCES:
            dropped.append((t, "invalid confidence"))
        elif not quote_in_text(t.quote, turn):
            dropped.append((t, "quote not found in the turn"))
        elif t.label in seen:
            continue  # same label twice in one turn: keep the first
        else:
            seen.add(t.label)
            accepted.append(t)
    return accepted, dropped


class GeminiProvider:
    """Google Gemini via REST (no SDK needed). Free tier: no billing on the project."""

    URL = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"

    def __init__(self, api_key: str, model: str, max_retries: int = 4):
        self.api_key = api_key
        self.model = model
        self.max_retries = max_retries

    def call(self, user_message: str, system: str = SYSTEM_PROMPT, tool: tuple = None) -> dict:
        """tool = (name, description, JSON-schema parameters); defaults to report_tactics."""
        tool_name, tool_description, tool_parameters = tool or (TOOL_NAME, TOOL_DESCRIPTION, TOOL_PARAMETERS)
        body = {
            "systemInstruction": {"parts": [{"text": system}]},
            "contents": [{"role": "user", "parts": [{"text": user_message}]}],
            "tools": [{"functionDeclarations": [{
                "name": tool_name,
                "description": tool_description,
                "parameters": tool_parameters,
            }]}],
            # mode ANY + one allowed function = the model is forced to call the tool
            "toolConfig": {"functionCallingConfig": {"mode": "ANY", "allowedFunctionNames": [tool_name]}},
            "generationConfig": {"temperature": 0},
        }
        data = json.dumps(body).encode("utf-8")
        for attempt in range(self.max_retries + 1):
            req = urllib.request.Request(
                self.URL.format(model=self.model), data=data,
                headers={"x-goog-api-key": self.api_key, "content-type": "application/json"},
            )
            try:
                with urllib.request.urlopen(req, timeout=60) as resp:
                    payload = json.load(resp)
                break
            except urllib.error.HTTPError as e:
                detail = e.read().decode("utf-8", "replace")[:300]
                # 429 = free-tier rate limit, 5xx = temporary; wait and retry
                if e.code in (429, 500, 503) and attempt < self.max_retries:
                    time.sleep(10 * (attempt + 1))
                    continue
                raise RuntimeError(f"Gemini HTTP {e.code}: {detail}") from None
        for part in payload["candidates"][0]["content"].get("parts", []):
            if "functionCall" in part:
                return part["functionCall"].get("args", {})
        raise RuntimeError(f"Gemini did not call {tool_name}: " + json.dumps(payload)[:300])


class GroqProvider:
    """Groq (OpenAI-compatible API). Free tier with much higher daily limits than Gemini's."""

    URL = "https://api.groq.com/openai/v1/chat/completions"

    def __init__(self, api_key: str, model: str, max_retries: int = 4):
        self.api_key = api_key
        self.model = model
        self.max_retries = max_retries

    def call(self, user_message: str, system: str = SYSTEM_PROMPT, tool: tuple = None) -> dict:
        """tool = (name, description, JSON-schema parameters); defaults to report_tactics."""
        tool_name, tool_description, tool_parameters = tool or (TOOL_NAME, TOOL_DESCRIPTION, TOOL_PARAMETERS)
        body = {
            "model": self.model,
            "temperature": 0,
            "messages": [
                {"role": "system", "content": system},
                {"role": "user", "content": user_message},
            ],
            "tools": [{"type": "function", "function": {
                "name": tool_name,
                "description": tool_description,
                "parameters": tool_parameters,
            }}],
            # naming the function forces the model to call it
            "tool_choice": {"type": "function", "function": {"name": tool_name}},
        }
        data = json.dumps(body).encode("utf-8")
        for attempt in range(self.max_retries + 1):
            req = urllib.request.Request(self.URL, data=data, headers={
                "Authorization": f"Bearer {self.api_key}",
                "content-type": "application/json",
                # Groq's edge rejects urllib's default user agent
                "user-agent": "ecfd-brain/0.1",
            })
            try:
                with urllib.request.urlopen(req, timeout=60) as resp:
                    payload = json.load(resp)
                break
            except urllib.error.HTTPError as e:
                detail = e.read().decode("utf-8", "replace")[:300]
                if e.code in (429, 500, 503) and attempt < self.max_retries:
                    time.sleep(10 * (attempt + 1))
                    continue
                raise RuntimeError(f"Groq HTTP {e.code}: {detail}") from None
        calls = payload["choices"][0]["message"].get("tool_calls") or []
        for call in calls:
            if call["function"]["name"] == tool_name:
                return json.loads(call["function"]["arguments"] or "{}")
        raise RuntimeError(f"Groq model did not call {tool_name}: " + json.dumps(payload)[:300])


def _json_schema(schema: dict) -> dict:
    """A tool's parameter schema as a structured-output schema: every object closed
    (additionalProperties: false) and numeric bounds dropped (the validators clamp them anyway)."""
    if isinstance(schema, list):
        return [_json_schema(s) for s in schema]
    if not isinstance(schema, dict):
        return schema
    out = {k: _json_schema(v) for k, v in schema.items() if k not in ("minimum", "maximum")}
    if out.get("type") == "object":
        out["additionalProperties"] = False
    return out


class AnthropicProvider:
    """Claude via the Anthropic SDK. The answer is constrained to the tool's JSON schema with structured
    outputs (current models don't accept a forced tool call); the long, fixed system prompt is cached."""

    def __init__(self, api_key: str, model: str, effort: str = "low"):
        import anthropic

        self.client = anthropic.Anthropic(api_key=api_key, max_retries=4)
        self.model = model
        self.effort = effort
        self.last_usage = {}

    def call(self, user_message: str, system: str = SYSTEM_PROMPT, tool: tuple = None) -> dict:
        tool_name, tool_description, tool_parameters = tool or (TOOL_NAME, TOOL_DESCRIPTION, TOOL_PARAMETERS)
        request = dict(
            model=self.model,
            max_tokens=16000,
            # the fixed instructions carry the cache marker; the per-turn message after it is never cached
            system=[{"type": "text", "cache_control": {"type": "ephemeral"},
                     "text": system + f"\n\nReturn your answer as the JSON object for {tool_name}: {tool_description}"}],
            messages=[{"role": "user", "content": user_message}],
            output_config={"effort": self.effort,
                           "format": {"type": "json_schema", "schema": _json_schema(tool_parameters)}},
        )
        if self.model.startswith("claude-sonnet-5-5"):
            # if a safety classifier declines, the API retries on a suitable model instead of failing
            response = self.client.beta.messages.create(
                **request, betas=["server-side-fallback-2026-07-01"], fallbacks="default")
        else:
            response = self.client.messages.create(**request)
        u = response.usage
        self.last_usage = {"input": u.input_tokens, "output": u.output_tokens,
                           "cache_read": getattr(u, "cache_read_input_tokens", 0) or 0,
                           "cache_write": getattr(u, "cache_creation_input_tokens", 0) or 0}
        if response.stop_reason == "refusal":
            raise RuntimeError(f"Claude declined the request ({getattr(response, 'stop_details', None)})")
        text = next((b.text for b in response.content if b.type == "text"), "")
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            raise RuntimeError(f"Claude returned invalid JSON (stop_reason={response.stop_reason}): {text[:300]}") from None


class Brain:
    def __init__(self, provider):
        self.provider = provider

    @classmethod
    def from_env(cls) -> "Brain":
        provider = os.getenv("LLM_PROVIDER", "groq").lower()  # groq: passed Test 0 (18/20), ~1,000 free requests/day
        if provider == "gemini":
            key = os.getenv("GEMINI_API_KEY")
            if not key:
                raise RuntimeError("GEMINI_API_KEY is missing from .env")
            # 3.5-flash: accepted every free-tier request in testing (2026-10-03); 3.8-flash was
            # frequently 503/403 "overloaded" on the free tier. Override with GEMINI_MODEL.
            return cls(GeminiProvider(key, os.getenv("GEMINI_MODEL", "gemini-3.5-flash")))
        if provider == "groq":
            key = os.getenv("GROQ_API_KEY")
            if not key:
                raise RuntimeError("GROQ_API_KEY is missing from .env")
            return cls(GroqProvider(key, os.getenv("GROQ_MODEL", "openai/gpt-oss-120b")))
        if provider == "anthropic":
            key = os.getenv("ANTHROPIC_API_KEY")
            if not key:
                raise RuntimeError("ANTHROPIC_API_KEY is missing from .env")
            return cls(AnthropicProvider(key, os.getenv("ANTHROPIC_MODEL", "claude-sonnet-5-5"),
                                         os.getenv("ANTHROPIC_EFFORT", "low")))
        raise RuntimeError(f"LLM_PROVIDER '{provider}' is not implemented yet (available: anthropic, gemini, groq)")

    def classify(self, turn: str, context: list = None) -> BrainResult:
        start = time.time()
        args = self.provider.call(build_user_message(turn, context or []))
        latency = int((time.time() - start) * 1000)
        accepted, dropped = validate(args, turn)
        return BrainResult(
            tactics=accepted, dropped=dropped,
            needs_more_context=bool(args.get("needs_more_context", False)),
            model=self.provider.model, latency_ms=latency, raw=args,
        )
