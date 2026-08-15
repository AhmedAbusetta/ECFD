namespace ECFD.Domain.Enums;

public enum CallStatus
{
    Ringing = 1,
    Answered = 2,
    Analyzing = 3,
    Ended = 4
}

public enum AttackStage
{
    Normal = 0,
    IdentityClaim = 1,
    Pressure = 2,
    SensitiveAction = 3,
    CredentialExtraction = 4
}

public enum EvidenceType
{
    Impersonation,
    Authority,
    Urgency,
    OtpRequest,
    CredentialRequest,
    PaymentRequest,
    RemoteAccess,
    Secrecy,
    VerificationBypass,
    SensitiveAction,
    VoiceSpoof
}

public enum RiskSeverity
{
    Low,
    Medium,
    High,
    Critical
}
