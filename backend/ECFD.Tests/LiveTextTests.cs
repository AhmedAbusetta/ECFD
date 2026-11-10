using System.Linq;
using ECFD.Application.Audio;
using Xunit;

namespace ECFD.Tests;

public class LiveTextStabilizerTests
{
    private static string Show(System.Collections.Generic.IReadOnlyList<LiveTextPart> parts) =>
        string.Join(" ", parts.Select(p => p.Stable ? $"[{p.Text}]" : p.Text));

    [Fact]
    public void First_Pass_Is_All_Guesses()
    {
        var s = new LiveTextStabilizer();
        Assert.Equal("ممكن تديني", Show(s.Update("ممكن تديني")));
    }

    [Fact]
    public void A_Word_Is_Confirmed_When_Two_Passes_Agree_And_Then_Never_Changes()
    {
        var s = new LiveTextStabilizer();
        s.Update("ممكن تديني ال");
        Assert.Equal("[ممكن تديني] الكود", Show(s.Update("ممكن تديني الكود")));
        // a later pass spells the confirmed words differently: the screen keeps the confirmed ones
        Assert.Equal("[ممكن تديني الكود] بسرعة", Show(s.Update("يمكن تدينا الكود بسرعة")));
    }

    [Fact]
    public void An_Unstable_First_Word_Does_Not_Block_The_Rest()
    {
        var s = new LiveTextStabilizer();
        s.Update("استقراري رقم الكارت كامل");
        Assert.Equal("استقرأ [رقم الكارت كامل]", Show(s.Update("استقرأ رقم الكارت كامل")));
    }

    [Fact]
    public void Spelling_Variants_And_Punctuation_Count_As_Agreement()
    {
        var s = new LiveTextStabilizer();
        s.Update("أهلا، معاك");
        Assert.Equal("[اهلا معاك] احمد", Show(s.Update("اهلا معاك احمد")));
    }

    [Fact]
    public void Confirmed_Words_Stay_When_A_Pass_Comes_Back_Shorter()
    {
        var s = new LiveTextStabilizer();
        s.Update("قولي الكود اللي");
        s.Update("قولي الكود اللي وصلك");
        Assert.Equal("[قولي الكود اللي]", Show(s.Update("قولي")));
    }
}

public class TranscriptGuardTests
{
    [Theory]
    [InlineData("", 0.9f, 2000)]
    [InlineData("   ", 0.9f, 2000)]
    [InlineData("شكرا", 0.30f, 300)]                                        // short sound, unsure model
    [InlineData("شكرا شكرا شكرا شكرا شكرا", 0.9f, 3000)]                    // one word on repeat
    [InlineData("اشتركوا في القناة وفعلوا زر الجرس عشان يوصلكم كل جديد", 0.9f, 400)] // too much text for 0.4 s
    public void Invented_Text_Is_Dropped(string text, float confidence, long audioMs)
    {
        Assert.True(TranscriptGuard.LooksInvented(text, confidence, audioMs));
    }

    [Theory]
    [InlineData("لأ", 0.80f, 300)]                                         // a short, sure answer matters
    [InlineData("تمام", 0f, 350)]                                          // service reported no confidence
    [InlineData("ممكن تديني الكود اللي وصلك دلوقتي", 0.85f, 2500)]
    [InlineData("لأ لأ لأ مش هينفع", 0.85f, 1500)]
    public void Real_Speech_Is_Kept(string text, float confidence, long audioMs)
    {
        Assert.False(TranscriptGuard.LooksInvented(text, confidence, audioMs));
    }
}
