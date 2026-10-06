using System;
using Xunit;
using ECFD.Domain.Enums;

namespace ECFD.Tests;

public class EvidenceTypeLabelsTests
{
    [Theory]
    [InlineData("IDENTITY_CLAIM", EvidenceType.IdentityClaim)]
    [InlineData("IMPERSONATION", EvidenceType.IdentityClaim)] // legacy label
    [InlineData("OTP_REQUEST", EvidenceType.OtpRequest)]
    [InlineData("CREDENTIAL_REQUEST", EvidenceType.CredentialRequest)]
    [InlineData("PAYMENT_REQUEST", EvidenceType.PaymentRequest)]
    [InlineData("REMOTE_ACCESS", EvidenceType.RemoteAccess)]
    [InlineData("VERIFICATION_BYPASS", EvidenceType.VerificationBypass)]
    [InlineData("SENSITIVE_ACTION", EvidenceType.SensitiveAction)]
    [InlineData("otp_request", EvidenceType.OtpRequest)]
    [InlineData("OtpRequest", EvidenceType.OtpRequest)]
    public void TryParse_Accepts_Wire_Labels(string label, EvidenceType expected)
    {
        Assert.True(EvidenceTypeLabels.TryParse(label, out var type));
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NOT_A_TACTIC")]
    [InlineData("42")]
    public void TryParse_Rejects_Unknown_Labels(string? label)
    {
        Assert.False(EvidenceTypeLabels.TryParse(label, out _));
    }

    [Fact]
    public void Every_EvidenceType_RoundTrips_Through_Its_Label()
    {
        foreach (var type in Enum.GetValues<EvidenceType>())
        {
            Assert.True(EvidenceTypeLabels.TryParse(type.ToLabel(), out var parsed));
            Assert.Equal(type, parsed);
        }
    }
}
