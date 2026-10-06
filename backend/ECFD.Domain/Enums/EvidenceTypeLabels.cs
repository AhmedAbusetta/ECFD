using System;

namespace ECFD.Domain.Enums;

/// <summary>
/// Maps between EvidenceType and the wire labels used by the ML services, dataset schemas
/// and SignalR events (e.g. "OTP_REQUEST" &lt;-&gt; EvidenceType.OtpRequest).
/// Enum.TryParse alone cannot do this because of the underscores.
/// </summary>
public static class EvidenceTypeLabels
{
    public static bool TryParse(string? label, out EvidenceType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.Replace("_", string.Empty).Trim();
        // Legacy name: the tactic was called IMPERSONATION before ADR-0004.
        if (normalized.Equals("IMPERSONATION", StringComparison.OrdinalIgnoreCase))
        {
            type = EvidenceType.IdentityClaim;
            return true;
        }
        // Enum.TryParse also accepts numbers ("4" -> OtpRequest); labels must be names.
        return normalized.Length > 0 && char.IsLetter(normalized[0])
               && Enum.TryParse(normalized, ignoreCase: true, out type)
               && Enum.IsDefined(typeof(EvidenceType), type);
    }

    public static string ToLabel(this EvidenceType type) => type switch
    {
        EvidenceType.IdentityClaim => "IDENTITY_CLAIM",
        EvidenceType.Authority => "AUTHORITY",
        EvidenceType.Urgency => "URGENCY",
        EvidenceType.OtpRequest => "OTP_REQUEST",
        EvidenceType.CredentialRequest => "CREDENTIAL_REQUEST",
        EvidenceType.PaymentRequest => "PAYMENT_REQUEST",
        EvidenceType.RemoteAccess => "REMOTE_ACCESS",
        EvidenceType.Secrecy => "SECRECY",
        EvidenceType.VerificationBypass => "VERIFICATION_BYPASS",
        EvidenceType.SensitiveAction => "SENSITIVE_ACTION",
        EvidenceType.VoiceSpoof => "VOICE_SPOOF",
        _ => type.ToString().ToUpperInvariant()
    };
}
