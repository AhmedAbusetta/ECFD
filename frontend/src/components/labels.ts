// Plain-language names for what the backend reports, so the dashboard says what happened instead of a code.

/** What a detected request means, as a phrase that completes "The caller …". */
const REQUESTS: Record<string, string> = {
  OTP_REQUEST: "asked for a verification code",
  CREDENTIAL_REQUEST: "asked for a password or card secret",
  PAYMENT_REQUEST: "asked the employee to move money",
  VERIFICATION_BYPASS: "asked to skip verification or use a “safe account”",
  REMOTE_ACCESS: "asked to install or open remote-control software",
  SENSITIVE_ACTION: "asked the employee to approve or confirm something",
  VOICE_SPOOF: "has a voice that sounds computer-generated",
};

/** Hard signals from the analyst look like "SECRET/OTP: قولي + الكود". */
export function describeHardSignal(signal: string): string {
  const [head] = signal.split(":");
  const [kind, concept = ""] = head.trim().toUpperCase().split("/");
  if (kind === "SECRET") {
    if (concept.includes("OTP")) return REQUESTS.OTP_REQUEST;
    if (concept.includes("PASSWORD") || concept === "PIN" || concept === "USERNAME") return "asked for a password or PIN";
    return "asked for card details";
  }
  if (kind === "PAYMENT") return REQUESTS.PAYMENT_REQUEST;
  if (kind === "BYPASS") return REQUESTS.VERIFICATION_BYPASS;
  if (kind === "REMOTE") return REQUESTS.REMOTE_ACCESS;
  if (kind === "IMPERSONATION") return "says they are inside the organisation, but the call came from outside";
  return head.trim();
}

export function describeTactic(label: string): string {
  return REQUESTS[label] ?? label.replace(/_/g, " ").toLowerCase();
}

/** Names for the lines under "Why this score". */
export function describeContributor(type: string): string {
  const known: Record<string, string> = {
    AI_CALL_ANALYST: "AI analyst's judgement of the whole call",
    FORBIDDEN_REQUEST: "The caller asked for something no honest caller asks for",
    ATTACK_PROGRESSION: "The call reached the request stage",
    VOICE_AUTHENTICITY_SUSPICION: "Voice sounds computer-generated",
  };
  if (known[type]) return known[type];
  return type
    .split(" + ")
    .map((t) => {
      const d = describeTactic(t);
      return d.charAt(0).toUpperCase() + d.slice(1);
    })
    .join(" + ");
}

export type VerdictLevel = "idle" | "safe" | "watch" | "danger";

/** One scale for the whole dashboard, so a number always means the same thing. */
export const RISK_SCALE: { from: number; label: string; level: VerdictLevel }[] = [
  { from: 0, label: "Normal call", level: "safe" },
  { from: 30, label: "Unusual, nothing forbidden asked", level: "watch" },
  { from: 60, label: "Suspicious request", level: "danger" },
  { from: 80, label: "Scam attempt", level: "danger" },
];

export function riskBand(score: number) {
  return [...RISK_SCALE].reverse().find((b) => score >= b.from) ?? RISK_SCALE[0];
}
