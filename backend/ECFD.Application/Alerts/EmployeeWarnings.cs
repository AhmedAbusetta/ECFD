using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECFD.Domain.Enums;

namespace ECFD.Application.Alerts;

/// <summary>
/// Speaks a short warning into the employee's ear during a live call; the caller does not hear it.
/// Implemented by the telephony layer; calls that are not on the PBX (simulated calls) are simply skipped.
/// </summary>
public interface IEmployeeWarner
{
    /// <returns>true if a warning started playing; false when the call isn't on the PBX, the kind was
    /// already played on this call, the per-call limit is reached, or warnings are off.</returns>
    Task<bool> WarnEmployeeAsync(Guid sessionId, string kind, CancellationToken ct = default);
}

/// <summary>Picks which recorded warning fits the call (one sound file per kind: ecfd-warn-&lt;kind&gt;).</summary>
public static class EmployeeWarnings
{
    public const string Otp = "otp";                     // verification / one-time code
    public const string Secret = "secret";               // password, PIN, card number, CVV
    public const string Payment = "payment";             // transfer money, "safe account"
    public const string Remote = "remote";               // install a remote-control app
    public const string Impersonation = "impersonation"; // pretends to be the bank / management
    public const string Pressure = "pressure";           // urgency, secrecy
    public const string General = "general";

    public static readonly IReadOnlyList<string> All = new[] { Otp, Secret, Payment, Remote, Impersonation, Pressure, General };

    /// <summary>
    /// The most specific warning for what the caller is doing. The analyst's hard signals
    /// ("SECRET/OTP: ...", "PAYMENT/PAYMENT: ...") name the exact request, so the newest one wins;
    /// otherwise the strongest rules evidence seen on the call decides.
    /// </summary>
    public static string Pick(IEnumerable<string>? hardSignals, IEnumerable<EvidenceType>? evidence)
    {
        foreach (var signal in (hardSignals ?? Array.Empty<string>()).Reverse())
        {
            var kind = FromHardSignal(signal);
            if (kind != null)
            {
                return kind;
            }
        }

        var seen = new HashSet<EvidenceType>(evidence ?? Array.Empty<EvidenceType>());
        if (seen.Contains(EvidenceType.OtpRequest)) return Otp;
        if (seen.Contains(EvidenceType.CredentialRequest)) return Secret;
        if (seen.Contains(EvidenceType.PaymentRequest) || seen.Contains(EvidenceType.VerificationBypass)) return Payment;
        if (seen.Contains(EvidenceType.RemoteAccess)) return Remote;
        if (seen.Contains(EvidenceType.Authority)) return Impersonation;
        if (seen.Contains(EvidenceType.Urgency) || seen.Contains(EvidenceType.Secrecy)) return Pressure;
        return General;
    }

    private static string? FromHardSignal(string signal)
    {
        var head = signal.Split(':')[0].Trim().ToUpperInvariant(); // e.g. SECRET/OTP
        var parts = head.Split('/');
        var kind = parts[0];
        var concept = parts.Length > 1 ? parts[1] : "";
        return kind switch
        {
            "SECRET" when concept.Contains("OTP") => Otp,
            "SECRET" => Secret,
            "PAYMENT" or "BYPASS" => Payment,
            "REMOTE" => Remote,
            _ => null
        };
    }
}

/// <summary>Used when telephony is off: there is no phone line to speak into.</summary>
public sealed class NoEmployeeWarner : IEmployeeWarner
{
    public Task<bool> WarnEmployeeAsync(Guid sessionId, string kind, CancellationToken ct = default) => Task.FromResult(false);
}
