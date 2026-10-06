using System;
using ECFD.Application.Interfaces;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Application.Progression;

/// <summary>
/// Deterministic attack-stage tracker. Stages describe how far a conversation has moved toward
/// extraction; they are not a verdict. Reaching IdentityClaim only records that the caller said
/// who they are (ADR-0004) - it is the later requests that make a stage risky.
/// </summary>
public class AttackProgressionEngine : IAttackProgressionEngine
{
    private const float MinConfidence = 0.60f;

    public (AttackStage NewStage, bool Transitioned, string Trigger) ProcessEvidence(AttackStage currentStage, Evidence newEvidence)
    {
        if (newEvidence.Confidence < MinConfidence)
        {
            return (currentStage, false, string.Empty);
        }

        var type = newEvidence.Type;
        bool isExtraction = type is EvidenceType.OtpRequest or EvidenceType.CredentialRequest
            or EvidenceType.PaymentRequest or EvidenceType.VerificationBypass;
        bool isSensitive = type is EvidenceType.RemoteAccess or EvidenceType.SensitiveAction;
        bool isPressure = type is EvidenceType.Authority or EvidenceType.Urgency or EvidenceType.Secrecy;

        // Asking for an OTP, password, payment or a verification skip is the extraction step
        // from any earlier stage - a caller who skips the introduction is not less dangerous.
        if (isExtraction && currentStage != AttackStage.CredentialExtraction)
        {
            var trigger = currentStage switch
            {
                AttackStage.Normal => "DIRECT_EXTRACTION_ATTEMPT",
                AttackStage.SensitiveAction => "FINAL_CREDENTIAL_EXTRACTION",
                _ => "EXTRACTION_ATTEMPT"
            };
            return (AttackStage.CredentialExtraction, true, trigger);
        }

        switch (currentStage)
        {
            case AttackStage.Normal:
                if (type == EvidenceType.IdentityClaim)
                {
                    return (AttackStage.IdentityClaim, true, "IDENTITY_CLAIMED");
                }
                if (isPressure)
                {
                    return (AttackStage.Pressure, true, "PRESSURE_APPLIED");
                }
                if (isSensitive)
                {
                    return (AttackStage.SensitiveAction, true, "SENSITIVE_ACTION_REQUESTED");
                }
                break;

            case AttackStage.IdentityClaim:
                if (isPressure)
                {
                    return (AttackStage.Pressure, true, "PRESSURE_APPLIED");
                }
                if (isSensitive)
                {
                    return (AttackStage.SensitiveAction, true, "SENSITIVE_ACTION_REQUESTED");
                }
                break;

            case AttackStage.Pressure:
                if (isSensitive)
                {
                    return (AttackStage.SensitiveAction, true, "SENSITIVE_ACTION_REQUESTED");
                }
                break;
        }

        return (currentStage, false, string.Empty);
    }
}
