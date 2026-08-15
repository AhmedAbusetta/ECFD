using System;
using ECFD.Application.Interfaces;
using ECFD.Domain.Entities;
using ECFD.Domain.Enums;

namespace ECFD.Application.Progression;

public class AttackProgressionEngine : IAttackProgressionEngine
{
    public (AttackStage NewStage, bool Transitioned, string Trigger) ProcessEvidence(AttackStage currentStage, Evidence newEvidence)
    {
        // High confidence threshold for stage transition
        if (newEvidence.Confidence < 0.60f)
        {
            return (currentStage, false, string.Empty);
        }

        switch (currentStage)
        {
            case AttackStage.Normal:
                if (newEvidence.Type == EvidenceType.Impersonation)
                {
                    return (AttackStage.IdentityClaim, true, "IMPERSONATION_DETECTED");
                }
                break;

            case AttackStage.IdentityClaim:
                if (newEvidence.Type == EvidenceType.Authority || newEvidence.Type == EvidenceType.Urgency)
                {
                    return (AttackStage.Pressure, true, "PRESSURE_APPLIED");
                }
                if (newEvidence.Type == EvidenceType.OtpRequest || newEvidence.Type == EvidenceType.CredentialRequest)
                {
                    return (AttackStage.CredentialExtraction, true, "DIRECT_EXTRACTION_ATTEMPT");
                }
                break;

            case AttackStage.Pressure:
                if (newEvidence.Type == EvidenceType.RemoteAccess || newEvidence.Type == EvidenceType.SensitiveAction)
                {
                    return (AttackStage.SensitiveAction, true, "SENSITIVE_ACTION_REQUESTED");
                }
                if (newEvidence.Type == EvidenceType.OtpRequest || newEvidence.Type == EvidenceType.CredentialRequest || newEvidence.Type == EvidenceType.PaymentRequest)
                {
                    return (AttackStage.CredentialExtraction, true, "EXTRACTION_ATTEMPT");
                }
                break;

            case AttackStage.SensitiveAction:
                if (newEvidence.Type == EvidenceType.OtpRequest || newEvidence.Type == EvidenceType.CredentialRequest || newEvidence.Type == EvidenceType.VerificationBypass)
                {
                    return (AttackStage.CredentialExtraction, true, "FINAL_CREDENTIAL_EXTRACTION");
                }
                break;

            case AttackStage.CredentialExtraction:
                // Terminal state in the attack lifecycle
                return (AttackStage.CredentialExtraction, false, string.Empty);
        }

        return (currentStage, false, string.Empty);
    }
}
