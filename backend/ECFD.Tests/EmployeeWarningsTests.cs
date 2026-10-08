using System;
using ECFD.Application.Alerts;
using ECFD.Domain.Enums;
using Xunit;

namespace ECFD.Tests;

public class EmployeeWarningsTests
{
    [Theory]
    [InlineData("SECRET/OTP: قولي + الكود", EmployeeWarnings.Otp)]
    [InlineData("SECRET/CVV: اقرالي + الcvv", EmployeeWarnings.Secret)]
    [InlineData("SECRET/PASSWORD: ابعتلي + الباسورد", EmployeeWarnings.Secret)]
    [InlineData("PAYMENT/PAYMENT: حول + انستاباي", EmployeeWarnings.Payment)]
    [InlineData("BYPASS/SAFE_ACCOUNT: حساب امان", EmployeeWarnings.Payment)]
    [InlineData("REMOTE/REMOTE_APP: انى ديسك", EmployeeWarnings.Remote)]
    public void Hard_Signal_Names_The_Exact_Request(string signal, string expected)
    {
        Assert.Equal(expected, EmployeeWarnings.Pick(new[] { signal }, null));
    }

    [Fact]
    public void The_Newest_Hard_Signal_Wins_Over_Older_Ones_And_Over_Rules_Evidence()
    {
        var kind = EmployeeWarnings.Pick(
            new[] { "SECRET/CVV: x", "SECRET/OTP: y" },
            new[] { EvidenceType.PaymentRequest });
        Assert.Equal(EmployeeWarnings.Otp, kind);
    }

    [Theory]
    [InlineData(new[] { EvidenceType.Authority, EvidenceType.OtpRequest }, EmployeeWarnings.Otp)]
    [InlineData(new[] { EvidenceType.Urgency, EvidenceType.CredentialRequest }, EmployeeWarnings.Secret)]
    [InlineData(new[] { EvidenceType.VerificationBypass }, EmployeeWarnings.Payment)]
    [InlineData(new[] { EvidenceType.Authority, EvidenceType.Urgency }, EmployeeWarnings.Impersonation)]
    [InlineData(new[] { EvidenceType.Secrecy }, EmployeeWarnings.Pressure)]
    [InlineData(new EvidenceType[0], EmployeeWarnings.General)]
    public void Without_Hard_Signals_The_Strongest_Rules_Evidence_Decides(EvidenceType[] evidence, string expected)
    {
        Assert.Equal(expected, EmployeeWarnings.Pick(Array.Empty<string>(), evidence));
    }
}
